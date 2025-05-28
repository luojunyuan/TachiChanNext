using System.Diagnostics.Contracts;
using System.Reflection;


#if WinUI
using Point = Windows.Foundation.Point;
using Size = Windows.Foundation.Size;
using Rect = Windows.Foundation.Rect;
#elif Avalonia
using System.Diagnostics;
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using Rect = Avalonia.Rect;
#endif

namespace TouchChan;

public static class PositionCalculator
{
    private const int TouchSpace = Constants.TouchSpace;

    /// <summary>
    /// 根据窗口大小、触摸按钮停靠锚点和触摸按钮大小计算触摸按钮的停靠区域矩形。
    /// </summary>
    /// <param name="window">窗口大小</param>
    /// <param name="touchDock">触摸按钮停靠位置</param>
    /// <param name="touchSize">触摸按钮大小</param>
    /// <returns>触摸按钮的停靠位置矩形</returns>
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
            _ => default,
        };

        return new(pos.X, pos.Y, touchSize, touchSize);
    }

    /// <summary>
    /// 重新计算定位触摸按钮的停靠位置，将离角落近的四边停靠重定位到角落。
    /// </summary>
    /// <param name="Window">窗口大小</param>
    /// <param name="currentDock">触摸按钮当前的停靠位置</param>
    /// <param name="touchSize">触摸按钮大小</param>
    /// <returns>触摸按钮新的停靠位置</returns>
    [Pure]
    public static TouchDockAnchor TouchDockCornerRedirect(Size Window, TouchDockAnchor currentDock, double touchSize)
    {
        var touchVerticalMiddlePoint = currentDock.Scale * Window.Height;
        var touchHorizontalMiddlePoint = currentDock.Scale * Window.Width;
        var limitDistance = Constants.TouchSpace * 2 + touchSize / 2;

        var (touchMiddlePoint, size) = currentDock.Corner is TouchCorner.Left or TouchCorner.Right
            ? (touchVerticalMiddlePoint, Window.Height)
            : (touchHorizontalMiddlePoint, Window.Width);

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

    /// <summary>
    /// 按钮处于任意位置（不论窗口外还是窗口内），计算最终的停靠位置
    /// </summary>
    /// <param name="container">窗口大小</param>
    /// <param name="touch">处于任意位置的触摸按钮矩形</param>
    /// <returns>处于停靠边缘的触摸按钮矩形</returns>
    public static TouchDockAnchor CalculateTouchFinalDockAnchor(Size container, Rect touch)
    {
        var finalPoint = CalculateTouchFinalPosition(container, touch);

        var dockRect = new Rect(finalPoint.X, finalPoint.Y, touch.Width, touch.Width);

        return GetLastTouchDockAnchor(container, dockRect);
    }

    /// <summary>
    /// 根据旧窗口大小和触摸按钮位置，计算触摸按钮的最后停靠锚点。
    /// </summary>
    /// <param name="oldWindowSize"></param>
    /// <param name="touch"></param>
    /// <remarks>touch 必须在 window 合法停靠位置上，如果超出了会报错</remarks>
    /// <returns></returns>
    /// <exception cref="UnreachableException"></exception>
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
    public static Point CalculateTouchFinalPosition(Size container, Rect touch)
    {
        var initPos = new Point(touch.X, touch.Y);
        var touchSize = touch.Width;
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
