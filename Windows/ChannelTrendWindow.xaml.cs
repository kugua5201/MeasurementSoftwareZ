using MeasurementSoftware.Models;
using ScottPlot;
using ScottPlot.Plottables;
using System.Windows;
using System.Windows.Input;

namespace MeasurementSoftware.Windows
{
    public partial class ChannelTrendWindow : Window
    {
        private VerticalLine? _cursorLine;

        private readonly Dictionary<double, double> _rawValueMap = new();
        private readonly Dictionary<double, double> _zeroedValueMap = new();

        public ChannelTrendWindow(MeasurementChannel channel)
        {
            InitializeComponent();
            LoadChannel(channel);
        }

        private void LoadChannel(MeasurementChannel channel)
        {
            Title = $"趋势图 - {channel.ChannelName}";
            ChannelNameTextBlock.Text = channel.ChannelName;
            PlcValueTextBlock.Text = channel.DisplayMeasuredValue;
            ZeroedValueTextBlock.Text = channel.HasZeroOffsetReferenceValue ? channel.DisplayZeroOffsetValue : "----";
            HoverInfoTextBlock.Text = "--";

            TrendPlot.Plot.Clear();
            TrendPlot.Plot.Title("");
            TrendPlot.Plot.XLabel("采样序号");
            TrendPlot.Plot.YLabel(string.IsNullOrWhiteSpace(channel.Unit) ? "值" : channel.Unit);

            AddSeries(channel.PlcRawHistoricalRecords, "原始值", "#1E88E5", isRaw: true);

            if (channel.HasZeroOffsetReferenceValue)
            {
                AddSeries(channel.ZeroedHistoricalRecords, "置零后值", "#FB8C00", isRaw: false);
            }

            _cursorLine = TrendPlot.Plot.Add.VerticalLine(0);
            _cursorLine.IsVisible = false;
            _cursorLine.Color = Colors.Red;
            _cursorLine.LineWidth = 1;

            TrendPlot.Plot.Legend.IsVisible = true;
            TrendPlot.Plot.Axes.AutoScale();
            TrendPlot.Refresh();
        }

        private void AddSeries(
            IReadOnlyList<HistoryRecordModel> records,
            string legendText,
            string colorHex,
            bool isRaw)
        {
            if (records.Count == 0)
            {
                return;
            }

            var ys = records.SelectMany(r => r.YValues).ToList();
            if (ys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < ys.Count; i++)
            {
                if (isRaw)
                {
                    _rawValueMap[i] = ys[i];
                }
                else
                {
                    _zeroedValueMap[i] = ys[i];
                }
            }

            var xs = Enumerable.Range(0, ys.Count).Select(i => (double)i).ToArray();

            var plot = TrendPlot.Plot.Add.Scatter(xs, ys.ToArray());
            plot.LegendText = legendText;
            plot.Color = Color.FromHex(colorHex);
            plot.LineWidth = 2;
            plot.MarkerSize = 0;
        }

        private void TrendPlot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_cursorLine is null || _rawValueMap.Count == 0)
            {
                return;
            }

            var pt = e.GetPosition(TrendPlot);
            var coord = TrendPlot.Plot.GetCoordinates((float)pt.X, (float)pt.Y);

            // 横轴是采样序号，直接吸附到最近的整数点
            double x = Math.Round(coord.X);

            if (!_rawValueMap.TryGetValue(x, out double rawY))
            {
                return;
            }

            _cursorLine.X = x;
            _cursorLine.IsVisible = true;

            string text = $"序号={x:0}, 原始={rawY:0.###}";

            if (_zeroedValueMap.TryGetValue(x, out double zeroedY))
            {
                text += $", 置零={zeroedY:0.###}";
            }

            HoverInfoTextBlock.Text = text;
            TrendPlot.Refresh();
        }

        private void TrendPlot_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_cursorLine is null)
            {
                return;
            }

            _cursorLine.IsVisible = false;
            HoverInfoTextBlock.Text = "--";
            TrendPlot.Refresh();
        }
    }
}