using MeasurementSoftware.Models;
using MeasurementSoftware.ViewModels;
using ScottPlot;
using System.ComponentModel;
using System.Windows.Controls;

namespace MeasurementSoftware.UserControls
{
    public partial class SpcUserControl : UserControl
    {
        public SpcUserControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            TrendChart.Plot.Font.Set("宋体");
            XbarChart.Plot.Font.Set("宋体");
            RChart.Plot.Font.Set("宋体");
            HistogramChart.Plot.Font.Set("宋体");
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= ViewModel_PropertyChanged;

            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not SpcViewModel vm) return;

            switch (e.PropertyName)
            {
                case nameof(SpcViewModel.XbarRData):
                    Dispatcher.Invoke(() => UpdateXbarChart(vm));
                    Dispatcher.Invoke(() => UpdateRChart(vm));
                    break;
                case nameof(SpcViewModel.HistogramFrequencies):
                    Dispatcher.Invoke(() => UpdateHistogramChart(vm));
                    break;
                case nameof(SpcViewModel.RawData):
                    Dispatcher.Invoke(() => UpdateTrendChart(vm));
                    break;
            }
        }

        private void UpdateXbarChart(SpcViewModel vm)
        {
            XbarChart.Plot.Clear();

            var data = vm.XbarRData;
            if (data == null || data.Points.Count == 0) { XbarChart.Refresh(); return; }

            var xs = data.Points.Select(p => (double)p.SubgroupIndex).ToArray();
            var ys = data.Points.Select(p => p.XbarValue).ToArray();

            XbarChart.Plot.Add.ScatterLine(xs, ys);

            // 控制限
            XbarChart.Plot.Add.HorizontalLine(data.Limits.XbarCL, color: ScottPlot.Color.FromHex("#2196F3"));
            XbarChart.Plot.Add.HorizontalLine(data.Limits.XbarUCL, color: ScottPlot.Color.FromHex("#F44336"));
            XbarChart.Plot.Add.HorizontalLine(data.Limits.XbarLCL, color: ScottPlot.Color.FromHex("#F44336"));

            XbarChart.Plot.Title("Xbar 控制图");
            XbarChart.Plot.XLabel("子组编号");
            XbarChart.Plot.YLabel("Xbar");
            XbarChart.Plot.Axes.AutoScale();
            XbarChart.Refresh();
        }

        private void UpdateRChart(SpcViewModel vm)
        {
            RChart.Plot.Clear();

            var data = vm.XbarRData;
            if (data == null || data.Points.Count == 0) { RChart.Refresh(); return; }

            var xs = data.Points.Select(p => (double)p.SubgroupIndex).ToArray();
            var ys = data.Points.Select(p => p.RangeValue).ToArray();

            RChart.Plot.Add.ScatterLine(xs, ys);

            RChart.Plot.Add.HorizontalLine(data.Limits.RCL, color: ScottPlot.Color.FromHex("#2196F3"));
            RChart.Plot.Add.HorizontalLine(data.Limits.RUCL, color: ScottPlot.Color.FromHex("#F44336"));
            if (data.Limits.RLCL > 0)
                RChart.Plot.Add.HorizontalLine(data.Limits.RLCL, color: ScottPlot.Color.FromHex("#F44336"));

            RChart.Plot.Title("R 控制图");
            RChart.Plot.XLabel("子组编号");
            RChart.Plot.YLabel("极差 R");
            RChart.Plot.Axes.AutoScale();
            RChart.Refresh();
        }

        private void UpdateHistogramChart(SpcViewModel vm)
        {
            HistogramChart.Plot.Clear();

            var centers = vm.HistogramBinCenters;
            var freqs = vm.HistogramFrequencies;
            if (centers.Length == 0) { HistogramChart.Refresh(); return; }

            var values = freqs.Select(f => (double)f).ToArray();
            var bars = new List<ScottPlot.Bar>();
            for (int i = 0; i < centers.Length; i++)
            {
                bars.Add(new ScottPlot.Bar
                {
                    Position = centers[i],
                    Value = values[i],
                    Size = centers.Length > 1 ? (centers[1] - centers[0]) * 0.9 : 1
                });
            }
            XbarChart.Plot.Add.Bars(bars);

            // 规格限线
            if (vm.CurrentSpcResult != null)
            {
                HistogramChart.Plot.Add.VerticalLine(vm.CurrentSpcResult.USL, color: ScottPlot.Color.FromHex("#F44336"));
                HistogramChart.Plot.Add.VerticalLine(vm.CurrentSpcResult.LSL, color: ScottPlot.Color.FromHex("#F44336"));
                HistogramChart.Plot.Add.VerticalLine(vm.CurrentSpcResult.Nominal, color: ScottPlot.Color.FromHex("#4CAF50"));
            }

            HistogramChart.Plot.Title("分布直方图");
            HistogramChart.Plot.XLabel("测量值");
            HistogramChart.Plot.YLabel("频次");
            HistogramChart.Plot.Axes.AutoScale();
            HistogramChart.Refresh();
        }

        private void UpdateTrendChart(SpcViewModel vm)
        {
            TrendChart.Plot.Clear();

            if (vm.RawData.Count == 0) { TrendChart.Refresh(); return; }

            var ys = vm.RawData.ToArray();
            var xs = Enumerable.Range(1, ys.Length).Select(i => (double)i).ToArray();

            TrendChart.Plot.Add.ScatterLine(xs, ys);

            if (vm.CurrentSpcResult != null)
            {
                TrendChart.Plot.Add.HorizontalLine(vm.CurrentSpcResult.USL, color: ScottPlot.Color.FromHex("#F44336"));
                TrendChart.Plot.Add.HorizontalLine(vm.CurrentSpcResult.LSL, color: ScottPlot.Color.FromHex("#F44336"));
                TrendChart.Plot.Add.HorizontalLine(vm.CurrentSpcResult.Nominal, color: ScottPlot.Color.FromHex("#4CAF50"));
            }

            TrendChart.Plot.Title("数据趋势图");
            TrendChart.Plot.XLabel("样本序号");
            TrendChart.Plot.YLabel("测量值");
            TrendChart.Plot.Axes.AutoScale();
            TrendChart.Refresh();
        }
    }
}
