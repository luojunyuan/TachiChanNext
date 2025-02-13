using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.AppLifecycle;
using R3;
using System;
using System.Diagnostics;

namespace TouchChan.WinUI;

static class R3Extensions
{
    public static Observable<AppActivationArguments> RxActivated(this AppInstance data) =>
        Observable.FromEvent<EventHandler<AppActivationArguments>, AppActivationArguments>(
            h => (sender, e) => h(e),
            e => data.Activated += e,
            e => data.Activated -= e);

    public static Observable<EventArgs> RxExited(this Process data) =>
        Observable.FromEvent<EventHandler, EventArgs>(
            h => (sender, e) => h(e),
            e => data.Exited += e,
            e => data.Exited -= e);

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

    public static Observable<SizeChangedEventArgs> RxSizeChanged(this FrameworkElement data) =>
        Observable.FromEvent<SizeChangedEventHandler, SizeChangedEventArgs>(
            h => (sender, e) => h(e),
            e => data.SizeChanged += e,
            e => data.SizeChanged -= e);
}
