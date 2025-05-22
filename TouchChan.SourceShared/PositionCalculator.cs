using System.Diagnostics.Contracts;
using System.Diagnostics;

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

public static class PositionCalculator
{
    private const int TouchSpace = Constants.TouchSpace;

    [Pure]
    public static TouchDockAnchor TouchDockTransform(TouchDockAnchor currentDock, Size windowSize, double touchSize)
    {
        var touchVerticalMiddlePoint = currentDock.Scale * windowSize.Height;
        var touchHorizontalMiddlePoint = currentDock.Scale * windowSize.Width;
        var limitDistance = Constants.TouchSpace * 2 + touchSize / 2;

        var (touchMiddlePoint, size) = currentDock.Corner is TouchCorner.Left or TouchCorner.Right
            ? (touchVerticalMiddlePoint, windowSize.Height)
            : (touchHorizontalMiddlePoint, windowSize.Width);

        var reverseTouchMiddlePoint = size - touchMiddlePoint + Constants.TouchSpace * 2;

        var dockCorner = currentDock.Corner switch
        {
            TouchCorner.Left => touchMiddlePoint <= limitDistance ? TouchCorner.TopLeft
                : reverseTouchMiddlePoint <= limitDistance ? TouchCorner.BottomLeft
                : currentDock.Corner,

            TouchCorner.Right => touchMiddlePoint <= limitDistance ? TouchCorner.TopRight
                : reverseTouchMiddlePoint <= limitDistance ? TouchCorner.BottomRight
                : currentDock.Corner,

            TouchCorner.Top => touchMiddlePoint <= limitDistance ? TouchCorner.TopLeft
                : reverseTouchMiddlePoint <= limitDistance ? TouchCorner.TopRight
                : currentDock.Corner,

            TouchCorner.Bottom => touchMiddlePoint <= limitDistance ? TouchCorner.BottomLeft
                : reverseTouchMiddlePoint <= limitDistance ? TouchCorner.BottomRight
                : currentDock.Corner,

            _ => currentDock.Corner,
        };

        return dockCorner == currentDock.Corner
            ? currentDock
            : new TouchDockAnchor(dockCorner, default);
    }

    [Pure]
    public static TouchDockAnchor GetLastTouchDockAnchor(Size oldWindowSize, Rect touch)
    {
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
            _ => throw new UnreachableException(),
        };
    }

    [Pure]
    public static Rect CalculateTouchDockRect(Size window, TouchDockAnchor touchDock, double touchSize)
    {
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
            _ => throw new UnreachableException(),
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
