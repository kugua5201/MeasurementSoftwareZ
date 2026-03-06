using MeasurementSoftware.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MeasurementSoftware.Converters
{
    /// <summary>
    /// 标注形状转 Geometry 转换器
    /// 使用归一化坐标(0~1)，配合 Path.Stretch="Fill" 自动缩放
    /// </summary>
    public class AnnotationShapeToGeometryConverter : IValueConverter
    {
        private static readonly Geometry CircleGeometry;
        private static readonly Geometry SquareGeometry;
        private static readonly Geometry DiamondGeometry;

        static AnnotationShapeToGeometryConverter()
        {
            var circle = new EllipseGeometry(new Point(0.5, 0.5), 0.5, 0.5);
            circle.Freeze();
            CircleGeometry = circle;

            var square = new RectangleGeometry(new Rect(0, 0, 1, 1), 0.05, 0.05);
            square.Freeze();
            SquareGeometry = square;

            var diamondFigure = new PathFigure(new Point(0.5, 0),
            [
                new LineSegment(new Point(1, 0.5), true),
                new LineSegment(new Point(0.5, 1), true),
                new LineSegment(new Point(0, 0.5), true),
            ], true);
            var diamond = new PathGeometry([diamondFigure]);
            diamond.Freeze();
            DiamondGeometry = diamond;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AnnotationShape shape)
            {
                return shape switch
                {
                    AnnotationShape.圆形 => CircleGeometry,
                    AnnotationShape.方形 => SquareGeometry,
                    AnnotationShape.菱形 => DiamondGeometry,
                    _ => CircleGeometry,
                };
            }
            return CircleGeometry;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
