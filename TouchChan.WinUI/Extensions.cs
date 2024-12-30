using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using System;
using System.Numerics;
using Windows.Foundation;

namespace TouchChan.WinUI
{
    readonly struct PointWarp(Point point)
    {
        public double X { get; } = point.X;
        public double Y { get; } = point.Y;

        public static Point operator -(PointWarp point1, Point point2)
        {
            return new Point((int)(point1.X - point2.X), (int)(point1.Y - point2.Y));
        }
    }

    // 用于扩展 Windows.Foundation.Point 之间的减法运算操作符，并返回 System.Drawing.Point
    static class MyPointExtensions
    {
        public static PointWarp Warp(this Point point) => new(point);
    }

    // 几何类型转换的扩展方法，项目中统一使用 System.Drawing 命名空间下的 int 类型
    static class GeometryExtensions
    {
        public static Size ToSize(this Vector2 size) => new((int)size.X, (int)size.Y);
    }

    static partial class ObservableEventsExtensions
    {
        public static Observable<object> RxCompleted(this Storyboard data) =>
            Observable.FromEvent<EventHandler<object>, object>(
                h => (sender, e) => h(e),
                e => data.Completed += e,
                e => data.Completed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerPressed(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerPressed += e,
                e => data.PointerPressed -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerMoved(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerMoved += e,
                e => data.PointerMoved -= e);

        public static Observable<PointerRoutedEventArgs> RxPointerReleased(this FrameworkElement data) =>
            Observable.FromEvent<PointerEventHandler, PointerRoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.PointerReleased += e,
                e => data.PointerReleased -= e);

        public static Observable<RoutedEventArgs> RxLoaded(this UserControl data) =>
            Observable.FromEvent<RoutedEventHandler, RoutedEventArgs>(
                h => (sender, e) => h(e),
                e => data.Loaded += e,
                e => data.Loaded -= e);
    }

    static class EventsExtensions
    {
        public static Point GetPosition(this PointerRoutedEventArgs pointerEvent, UIElement visual) =>
            pointerEvent.GetCurrentPoint(visual).Position;
    }

    static class VisualTreeHelperExtensions
    {
        public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null)
                return null;

            return parentObject is T parent ? parent : FindParent<T>(parentObject);
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
