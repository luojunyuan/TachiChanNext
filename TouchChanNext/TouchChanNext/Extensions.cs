using Windows.Foundation;
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
    // 类型转换便于与 CSWin32 的互操
    static class CSWin32Extensions
    {
        public static HWND ToHwnd(this nint windowHandle) => new(windowHandle);
    }

    // 用于 Windows.Foundation.Point 之间的减法运算，个人感觉另起一个 struct 的方式不太好
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
            return new Point(point1.X - point2.X, point1.Y - point2.Y);
        }
    }
}
