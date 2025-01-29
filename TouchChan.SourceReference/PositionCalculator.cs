using System.Diagnostics.Contracts;
#if WinUI
using Point = Windows.Foundation.Point;
using Size = Windows.Foundation.Size;
using Rect = Windows.Foundation.Rect;
#elif Avalonia
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using Rect = Avalonia.Rect;
#endif

namespace TouchChan;

public enum TouchCorner
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

public record struct TouchDockAnchor(TouchCorner Corner, double Scale = default);

public static class PositionCalculator
{
    [Pure]
    public static TouchDockAnchor GetLastTouchDockAnchor(Size oldWindowSize, Rect touch)
    {
        const int TouchSpace = 2;

        var touchSize = touch.Width;
        var touchPos = (touch.X, touch.Y);

        var oldRight = oldWindowSize.Width - TouchSpace - touchSize;
        var oldBottom = oldWindowSize.Height - TouchSpace - touchSize;
        return touchPos switch
        {
            { X: TouchSpace, Y: TouchSpace } => new(TouchCorner.TopLeft, default),
            { X: TouchSpace, Y: var y } when y == oldBottom => new(TouchCorner.BottomLeft, default),
            { X: var x, Y: TouchSpace } when x == oldRight => new(TouchCorner.TopRight, default),
            { X: var x, Y: var y } when x == oldRight && y == oldBottom => new(TouchCorner.BottomRight, default),
            { X: TouchSpace, Y: var y } => new(TouchCorner.Left, (y + TouchSpace + (touchSize / 2)) / oldWindowSize.Height),
            { X: var x, Y: TouchSpace } => new(TouchCorner.Top, (x + TouchSpace + (touchSize / 2)) / oldWindowSize.Width),
            { X: var x, Y: var y } when x == oldRight => new(TouchCorner.Right, (y + TouchSpace + (touchSize / 2)) / oldWindowSize.Height),
            { X: var x, Y: var y } when y == oldBottom => new(TouchCorner.Bottom, (x + TouchSpace + (touchSize / 2)) / oldWindowSize.Width),
            _ => default,
        };
    }

    [Pure]
    public static Rect CaculateTouchDockRect(Size window, TouchDockAnchor touchDock, double touchSize)
    {
        const int TouchSpace = 2;
        var newRight = window.Width - TouchSpace - touchSize;
        var newBottom = window.Height - TouchSpace - touchSize;
        Point pos = touchDock switch
        {
            { Corner: TouchCorner.TopLeft } => new(TouchSpace, TouchSpace),
            { Corner: TouchCorner.TopRight } => new(newRight, TouchSpace),
            { Corner: TouchCorner.BottomLeft } => new(TouchSpace, newBottom),
            { Corner: TouchCorner.BottomRight } => new(newRight, newBottom),
            { Corner: TouchCorner.Left, Scale: var posScale } => new(TouchSpace, window.Height * posScale - TouchSpace - (touchSize / 2)),
            { Corner: TouchCorner.Top, Scale: var posScale } => new(window.Width * posScale - TouchSpace - (touchSize / 2), TouchSpace),
            { Corner: TouchCorner.Right, Scale: var posScale } => new(newRight, window.Height * posScale - TouchSpace - (touchSize / 2)),
            { Corner: TouchCorner.Bottom, Scale: var posScale } => new(window.Width * posScale - TouchSpace - (touchSize / 2), newBottom),
            _ => default,
        };

        return new(pos.X, pos.Y, touchSize, touchSize);
    }

    [Pure]
    public static bool IsBeyondBoundary(Point newPos, double touchSize, Size container)
    {
        var oneThirdDistance = touchSize / 3;
        var twoThirdDistance = oneThirdDistance * 2;

        if (newPos.X < -oneThirdDistance ||
            newPos.Y < -oneThirdDistance ||
            newPos.X > container.Width - twoThirdDistance ||
            newPos.Y > container.Height - twoThirdDistance)
        {
            return true;
        }
        return false;
    }

    [Pure]
    public static Point CalculateTouchFinalPosition(Size container, Point initPos, int touchSize)
    {
        const int TouchSpace = 2;

        var xMidline = container.Width / 2;
        var right = container.Width - initPos.X - touchSize;
        var bottom = container.Height - initPos.Y - touchSize;

        var hSnapLimit = touchSize / 2;
        var vSnapLimit = touchSize / 3 * 2;

        var centerToLeft = initPos.X + hSnapLimit;

        bool VCloseTo(double distance) => distance < vSnapLimit;
        bool HCloseTo(double distance) => distance < hSnapLimit;

        double AlignToRight() => container.Width - touchSize - TouchSpace;
        double AlignToBottom() => container.Height - touchSize - TouchSpace;

        var left = initPos.X;
        var top = initPos.Y;

        return
            HCloseTo(left) && VCloseTo(top) ? new Point(TouchSpace, TouchSpace) :
            HCloseTo(right) && VCloseTo(top) ? new Point(AlignToRight(), TouchSpace) :
            HCloseTo(left) && VCloseTo(bottom) ? new Point(TouchSpace, AlignToBottom()) :
            HCloseTo(right) && VCloseTo(bottom) ? new Point(AlignToRight(), AlignToBottom()) :
                               VCloseTo(top) ? new Point(left, TouchSpace) :
                               VCloseTo(bottom) ? new Point(left, AlignToBottom()) :
            centerToLeft < xMidline ? new Point(TouchSpace, top) :
         /* centerToLeft >= xMidline */           new Point(AlignToRight(), top);
    }
}
