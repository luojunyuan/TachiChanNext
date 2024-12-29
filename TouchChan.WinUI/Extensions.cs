using System.Numerics;
using Windows.Foundation;

namespace TouchChan.WinUI
{
    // 几何类型转换的扩展方法，项目中统一使用 System.Drawing 命名空间下的 int 类型
    static class GeometryExtensions
    {
        public static Size ToSize(this Vector2 size) => new((int)size.X, (int)size.Y);
    }

    // 用于扩展 Windows.Foundation.Point 之间的减法运算操作符，并返回 System.Drawing.Point
    static class MyPointExtensions
    {
        public static PointWarp ToWarp(this Point point) => new(point);
    }

    readonly struct PointWarp(Point point)
    {
        public double X { get; } = point.X;
        public double Y { get; } = point.Y;

        public static Point operator -(PointWarp point1, Point point2)
        {
            return new Point((int)(point1.X - point2.X), (int)(point1.Y - point2.Y));
        }
    }

    // NOTE: 移动到 ViewModel 将来
    record TouchDockAnchor(TouchCorner Corner, double Scale);

    enum TouchCorner
    {
        Left,
        Top,
        Right,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
}
