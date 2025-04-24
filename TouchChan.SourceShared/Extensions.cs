using R3;

#if WinUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using R3.ObservableEvents;
using TouchChan.Interop;
using Windows.Foundation;
using Rect = Windows.Foundation.Rect;
using Size = Windows.Foundation.Size;
#elif Avalonia
using Size = Avalonia.Size;
using Rect = Avalonia.Rect;
#endif

namespace TouchChan;

static class BaseExtensions
{
    public static System.Drawing.Size ToGdiSize(this Size size) => new((int)size.Width, (int)size.Height);

    public static System.Drawing.Rectangle ToGdiRect(this Rect size) => new((int)size.X, (int)size.Y, (int)size.Width, (int)size.Height);
}

static class ReactiveExtensions
{
    public static ObservableAsPropertyHelper<T> ToProperty<T>(
        this Observable<T> source,
        T initialValue) => new(source, initialValue);

    public static void DisposeWith(this IDisposable disposable, CompositeDisposable compositeDisposable) =>
        compositeDisposable.Add(disposable);
}

#if WinUI

static class FluentWinUIExtensions
{
    public static T WithActivate<T>(this T window) where T : Window
    {
        window.Activate();
        return window;
    }

    private static readonly Dictionary<int, nint> Cache = [];

    private static nint GetWindowHandle(this Window window)
    {
        var cacheKey = window.GetHashCode();

        if (Cache.TryGetValue(cacheKey, out nint windowHandle))
            return windowHandle;

        return Cache[cacheKey] = WinRT.Interop.WindowNative.GetWindowHandle(window);
    }

    public static bool IsClosed(this Window window) => Win32.IsWindow(window.GetWindowHandle());

    public static void NativeActivate(this Window window) => Win32.ActivateWindow(window.GetWindowHandle());

    public static void NativeHide(this Window window) => Win32.HideWindow(window.GetWindowHandle());

    public static void NativeResize(this Window window, System.Drawing.Size size) =>
        Win32.ResizeWindow(window.GetWindowHandle(), size);

    public static Task SetParentAsync(this Window window, nint parent) =>
        Win32.SetParentWindowAsync(window.GetWindowHandle(), parent);

    public static void ToggleWindowStyle(this Window window, bool enable, WindowStyle style)
        => Win32.ToggleWindowStyle(window.GetWindowHandle(), enable, style);

    public static void ToggleWindowExStyle(this Window window, bool enable, ExtendedWindowStyle exStyle)
        => Win32.ToggleWindowExStyle(window.GetWindowHandle(), enable, exStyle);

    public static void SetWindowObservableRegion(this Window window, Rect rect) =>
        Win32.SetWindowObservableRegion(window.GetWindowHandle(), rect.ToGdiRect());

    public static void ResetWindowOriginalObservableRegion(this Window window, Size size) =>
        Win32.ResetWindowOriginalObservableRegion(window.GetWindowHandle(), size.ToGdiSize());
}

static class TouchControlExtensions
{
    public static Observable<Unit> Clicked(this Border border)
    {
        const double clickThreshold = 0;

        return border.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .SelectMany(pressEvent =>
            {
                var pressPosition = pressEvent.GetPosition();

                return border.Events().PointerReleased
                    .Take(1)
                    .Where(releaseEvent =>
                    {
                        var releasePosition = releaseEvent.GetPosition();
                        var distance = Math.Sqrt(
                            Math.Pow(releasePosition.X - pressPosition.X, 2) +
                            Math.Pow(releasePosition.Y - pressPosition.Y, 2));

                        return distance <= clickThreshold;
                    })
                    .Select(_ => Unit.Default);
            });
    }

    // 简化 pointerEvent.GetCurrentPoint(visual).Position -> pointerEvent.GetPosition(visual)
    public static Point GetPosition(this PointerRoutedEventArgs pointerEvent, UIElement? visual = null) =>
        pointerEvent.GetCurrentPoint(visual).Position;

    // 用于扩展 Windows.Foundation.Point 之间的减法运算操作符
    public static Point Subtract(this Point point, Point subPoint) => new(point.X - subPoint.X, point.Y - subPoint.Y);

    // 几何类型转换的扩展方法
    public static Size ToSize(this Vector2 size) => new((int)size.X, (int)size.Y);

    // XDpi 意味着将框架内部任何元素产生的点或面的值还原回真实的物理像素大小

    public static Rect XDpi(this Rect rect, double factor)
    {
        rect.X *= factor;
        rect.Y *= factor;
        rect.Width *= factor;
        rect.Height *= factor;
        return rect;
    }

    public static Size XDpi(this Vector2 vector, double factor)
    {
        var size = vector.ToSize();
        size.Width *= factor;
        size.Height *= factor;
        return size;
    }
}

#endif