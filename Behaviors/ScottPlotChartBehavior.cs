using MeasurementSoftware.Models;
using Microsoft.Xaml.Behaviors;
using ScottPlot;
using ScottPlot.WPF;
using System.Windows;

namespace MeasurementSoftware.Behaviors
{
    public class ScottPlotChartBehavior : Behavior<WpfPlot>
    {
        public static readonly DependencyProperty ChartModelProperty = DependencyProperty.Register(
            nameof(ChartModel),
            typeof(SpcChartModel),
            typeof(ScottPlotChartBehavior),
            new PropertyMetadata(null, OnChartModelChanged));

        public SpcChartModel? ChartModel
        {
            get => (SpcChartModel?)GetValue(ChartModelProperty);
            set => SetValue(ChartModelProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Plot.Font.Set("宋体");
            RenderChart();
        }

        private static void OnChartModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScottPlotChartBehavior behavior)
            {
                behavior.RenderChart();
            }
        }

        private void RenderChart()
        {
            if (AssociatedObject == null)
            {
                return;
            }

            AssociatedObject.Plot.Clear();

            if (ChartModel == null)
            {
                AssociatedObject.Refresh();
                return;
            }

            foreach (var series in ChartModel.Series)
            {
                if (series.SeriesType == SpcChartSeriesType.Bar)
                {
                    var bars = new List<Bar>();
                    for (int i = 0; i < Math.Min(series.XValues.Length, series.YValues.Length); i++)
                    {
                        bars.Add(new Bar
                        {
                            Position = series.XValues[i],
                            Value = series.YValues[i],
                            Size = series.Size
                        });
                    }

                    AssociatedObject.Plot.Add.Bars(bars);
                    continue;
                }

                if (series.XValues.Length > 0 && series.YValues.Length > 0)
                {
                    AssociatedObject.Plot.Add.ScatterLine(
                        series.XValues,
                        series.YValues,
                        color: Color.FromHex(series.ColorHex));
                }
            }

            foreach (var line in ChartModel.ReferenceLines)
            {
                var color = Color.FromHex(line.ColorHex);
                if (line.LineType == SpcChartReferenceLineType.Vertical)
                {
                    AssociatedObject.Plot.Add.VerticalLine(line.Value, color: color);
                }
                else
                {
                    AssociatedObject.Plot.Add.HorizontalLine(line.Value, color: color);
                }
            }

            AssociatedObject.Plot.Title(ChartModel.Title);
            AssociatedObject.Plot.XLabel(ChartModel.XLabel);
            AssociatedObject.Plot.YLabel(ChartModel.YLabel);
            AssociatedObject.Plot.Axes.AutoScale();
            AssociatedObject.Refresh();
        }
    }
}
