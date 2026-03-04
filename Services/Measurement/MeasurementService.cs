using MeasurementSoftware.Models;
using MeasurementSoftware.Services.Config;
using MeasurementSoftware.Services.Logs;

namespace MeasurementSoftware.Services
{
    /// <summary>
    /// 测量服务实现 — 从PLC读取数据、根据通道类型计算、判定合格
    /// </summary>
    public class MeasurementService : IMeasurementService
    {
        private readonly ILog _log;
        private readonly IDeviceConfigService _deviceConfigService;
        private readonly ICalibrationService _calibrationService;

        private CancellationTokenSource? _cts;
        private bool _isAcquiring;

        /// <summary>
        /// 连续采集采样次数（用于统计类通道）
        /// </summary>
        private const int ContinuousSampleCount = 50;

        /// <summary>
        /// 连续采集每次采样间隔(ms)
        /// </summary>
        private const int SampleIntervalMs = 100;

        public bool IsAcquiring => _isAcquiring;

        public event EventHandler<MeasurementCompletedEventArgs>? MeasurementCompleted;
        public event EventHandler<RealTimeDataEventArgs>? RealTimeDataUpdated;

        public MeasurementService(ILog log, IDeviceConfigService deviceConfigService, ICalibrationService calibrationService)
        {
            _log = log;
            _deviceConfigService = deviceConfigService;
            _calibrationService = calibrationService;
        }

        public async Task<MeasurementSessionResult> StartMeasurementAsync(MeasurementRecipe recipe, int stepNumber = 0)
        {
            if (_isAcquiring)
                return new MeasurementSessionResult { Success = false, Message = "正在采集中，请等待" };

            _isAcquiring = true;
            _cts = new CancellationTokenSource();

            try
            {
                var channels = stepNumber > 0
                    ? recipe.GetChannelsByStep(stepNumber)
                    : recipe.GetEnabledChannels();

                if (channels.Count == 0)
                    return new MeasurementSessionResult { Success = false, Message = "没有启用的测量通道" };

                _log.Info($"开始测量，共 {channels.Count} 个通道");

                var channelResults = new List<ChannelMeasurementData>();

                foreach (var channel in channels)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    double measuredValue;

                    if (channel.ChannelType == ChannelType.结果值)
                    {
                        // 结果值通道：单次读取
                        var raw = await ReadChannelValueAsync(channel);
                        if (raw == null)
                        {
                            _log.Warn($"通道 {channel.ChannelName} 读取失败，使用0");
                            raw = 0;
                        }
                        measuredValue = _calibrationService.ApplyCalibration(channel, raw.Value);
                    }
                    else
                    {
                        // 统计类通道：连续采集后计算
                        channel.HistoricalData.Clear();

                        for (int i = 0; i < ContinuousSampleCount; i++)
                        {
                            if (_cts.Token.IsCancellationRequested) break;

                            var raw = await ReadChannelValueAsync(channel);
                            if (raw != null)
                            {
                                var calibrated = _calibrationService.ApplyCalibration(channel, raw.Value);
                                channel.HistoricalData.Add(calibrated);

                                RealTimeDataUpdated?.Invoke(this, new RealTimeDataEventArgs
                                {
                                    ChannelNumber = channel.ChannelNumber,
                                    Value = calibrated,
                                    Timestamp = DateTime.Now
                                });
                            }

                            await Task.Delay(SampleIntervalMs, _cts.Token);
                        }

                        var stats = CalculateStatistics(channel);
                        measuredValue = channel.ChannelType switch
                        {
                            ChannelType.最大值 => stats.Max,
                            ChannelType.最小值 => stats.Min,
                            ChannelType.平均值 => stats.Average,
                            ChannelType.跳动值 => stats.Runout,
                            ChannelType.齿跳动值 => stats.Runout, // 齿跳动值使用相同计算
                            _ => stats.Average
                        };
                    }

                    channel.MeasuredValue = measuredValue;
                    channel.CheckResult();

                    channelResults.Add(new ChannelMeasurementData
                    {
                        ChannelNumber = channel.ChannelNumber,
                        ChannelName = channel.ChannelName,
                        StandardValue = channel.StandardValue,
                        UpperTolerance = channel.UpperTolerance,
                        LowerTolerance = channel.LowerTolerance,
                        MeasuredValue = measuredValue,
                        Result = channel.Result
                    });
                }

                var overallResult = channelResults.All(c => c.Result == MeasurementResult.Pass)
                    ? MeasurementResult.Pass
                    : MeasurementResult.Fail;

                var result = new MeasurementSessionResult
                {
                    Success = true,
                    OverallResult = overallResult,
                    ChannelResults = channelResults,
                    MeasurementTime = DateTime.Now,
                    Message = overallResult == MeasurementResult.Pass ? "测量合格" : "测量不合格"
                };

                MeasurementCompleted?.Invoke(this, new MeasurementCompletedEventArgs
                {
                    OverallResult = overallResult,
                    ChannelResults = channelResults
                });

                _log.Info($"测量完成: {result.Message}");
                return result;
            }
            catch (OperationCanceledException)
            {
                _log.Info("测量已取消");
                return new MeasurementSessionResult { Success = false, Message = "测量已取消" };
            }
            catch (Exception ex)
            {
                _log.Error($"测量异常: {ex.Message}");
                return new MeasurementSessionResult { Success = false, Message = $"测量异常: {ex.Message}" };
            }
            finally
            {
                _isAcquiring = false;
            }
        }

        public void StopMeasurement()
        {
            _cts?.Cancel();
            _isAcquiring = false;
            _log.Info("停止采集");
        }

        public async Task<double?> ReadChannelValueAsync(MeasurementChannel channel)
        {
            try
            {
                if (channel.PlcDeviceId == 0 || string.IsNullOrEmpty(channel.DataPointId))
                {
                    return null;
                }

                var device = _deviceConfigService.Devices
                    .FirstOrDefault(d => d.DeviceId == channel.PlcDeviceId);

                if (device?.protocol == null || !device.IsConnected)
                {
                    _log.Warn($"通道 {channel.ChannelName} 关联的PLC设备未连接");
                    return null;
                }

                // 从设备的数据点中查找对应点位
                var dataPoint = device.DataPoints.FirstOrDefault(dp => dp.PointId == channel.DataPointId);
                if (dataPoint == null)
                {
                    _log.Warn($"通道 {channel.ChannelName} 的数据点 {channel.DataPointId} 未找到");
                    return null;
                }

                // 如果设备启用了轮询，DataPoint已经有最新值
                if (dataPoint.CurrentValue != null && dataPoint.IsSuccess)
                {
                    return Convert.ToDouble(dataPoint.CurrentValue);
                }

                // 设备未启用轮询时主动读取一次
                var fieldInfo = new MultiProtocol.Model.FieldInfo(dataPoint.Address, dataPoint.DataType, dataPoint.ByteOrder);
                var results = await device.protocol.ReadDataAsync(device.DeviceId, [fieldInfo]);
                var readResult = results.FirstOrDefault();

                if (readResult != null && readResult.IsSuccess && readResult.Value != null)
                {
                    return Convert.ToDouble(readResult.Value);
                }

                _log.Warn($"通道 {channel.ChannelName} 读取失败");
                return null;
            }
            catch (Exception ex)
            {
                _log.Error($"读取通道 {channel.ChannelName} 异常: {ex.Message}");
                return null;
            }
        }

        public ChannelStatistics CalculateStatistics(MeasurementChannel channel)
        {
            var data = channel.HistoricalData;
            if (data.Count == 0)
            {
                return new ChannelStatistics();
            }

            var max = data.Max();
            var min = data.Min();

            return new ChannelStatistics
            {
                Max = max,
                Min = min,
                Average = data.Average(),
                Runout = max - min,
                SampleCount = data.Count
            };
        }
    }
}
