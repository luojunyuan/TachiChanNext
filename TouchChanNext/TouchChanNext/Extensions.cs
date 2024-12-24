using Windows.Foundation;
using Windows.Win32.Foundation;

namespace TouchChan
{
    static class CSWin32Extensions
    {
        public static HWND ToHwnd(this nint handle) => new(handle);
    }

    static class MyPointExtensions
    {
        public static PointWarp ToWarp(this Point point) => new(point);
    }

    readonly struct PointWarp
    {
        public double X { get; }
        public double Y { get; }

        public PointWarp(double x, double y)
        {
            X = x;
            Y = y;
        }

        public PointWarp(Point point)
        {
            X = point.X;
            Y = point.Y;
        }

        public static PointWarp operator -(PointWarp point1, PointWarp point2)
        {
            return new PointWarp(point1.X - point2.X, point1.Y - point2.Y);
        }

        public static implicit operator Point(PointWarp myPoint)
        {
            return new Point(myPoint.X, myPoint.Y);
        }

        public static implicit operator PointWarp(Point point)
        {
            return new PointWarp(point);
        }
    }
}
