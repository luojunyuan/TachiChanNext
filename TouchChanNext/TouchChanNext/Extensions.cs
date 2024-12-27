using System.Drawing;
using System.Numerics;
using Windows.Win32.Foundation;

// CSWin32 的包装代码，本来应该是 CSWin32 负责生成的工作
namespace Windows.Win32
{
    partial class PInvoke
    {
        internal unsafe static uint GetWindowThreadProcessId(HWND hwnd, out uint lpdwProcessId)
        {
            fixed (uint* _lpdwProcessId = &lpdwProcessId)
                return GetWindowThreadProcessId(hwnd, _lpdwProcessId);
        }
    }
}

namespace TouchChan
{
    // 类型转换便于与 CSWin32 互操
    static class CSWin32Extensions
    {
        public static HWND ToHwnd(this nint windowHandle) => new(windowHandle);
    }

    // 几何类型转换的扩展方法，项目中统一使用 System.Drawing 命名空间下的 int 类型
    static class GeometryExtensions
    {
        public static Rectangle Multiply(this Rectangle rect, double factor) =>
            new()
            {
                X = (int)(rect.X * factor),
                Y = (int)(rect.Y * factor),
                Width = (int)(rect.Width * factor),
                Height = (int)(rect.Height * factor)
            };

        public static Size ToSize(this Vector2 size) => new((int)size.X, (int)size.Y);

        private const int BottomRightOffset = 2;

        public static Size HackOffset(this Size window)
        {
            window.Width -= BottomRightOffset;
            window.Height -= BottomRightOffset;
            return window;
        }
    }

    // 用于扩展 Windows.Foundation.Point 之间的减法运算操作符，并返回 System.Drawing.Point
    static class MyPointExtensions
    {
        public static PointWarp ToWarp(this Windows.Foundation.Point point) => new(point);
    }

    readonly struct PointWarp(Windows.Foundation.Point point)
    {
        public double X { get; } = point.X;
        public double Y { get; } = point.Y;

        public static Point operator -(PointWarp point1, Windows.Foundation.Point point2)
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
