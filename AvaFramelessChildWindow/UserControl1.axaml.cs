using System;
using System.Drawing;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Remote.Protocol.Input;
using Avalonia.Styling;
using R3;
using Size = System.Drawing.Size;
using Avalonia.Media;
using Avalonia.VisualTree;
using System.Diagnostics.Contracts;
using System.ComponentModel;
using System.Xml.Linq;
using Avalonia.LogicalTree;
using TouchChan;
using Avalonia.Animation.Easings;

namespace AvaFramelessChildWindow;

public partial class UserControl1 : UserControl
{
    private readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);

    private readonly Animation SmoothMoveStoryboard;

    private readonly TranslateTransform TouchTransform = new();

    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rectangle>? SetWindowObservable { get; set; }

    public UserControl1()
    {
        InitializeComponent();

        TouchTransform.X = 2;
        TouchTransform.Y = 2;
        Touch.RenderTransform = TouchTransform;
        SmoothMoveStoryboard = new()
        {
            Duration = ReleaseToEdgeDuration,
            FillMode = FillMode.Forward,
            Children =
            {
                new () 
                {
                    Cue = new Cue(0d),
                    Setters = { new Setter(TranslateTransform.XProperty, default), new Setter(TranslateTransform.YProperty, default) }
                },
                new () 
                {
                    Cue = new Cue(1d),
                    Setters = { new Setter(TranslateTransform.XProperty, default), new Setter(TranslateTransform.YProperty, default) }
                },
            },
        };

        this.RxLoaded()
            .Subscribe(InitializeTouch);
    }

    private static Size DotNetSize(Avalonia.Size size) => new((int)size.Width, (int)size.Height);
    private static Point DotPoint(Avalonia.Point point) => new((int)point.X, (int)point.Y);
    private static Rectangle XDpi(Avalonia.Rect rect) => 
        new((int)(rect.X * DpiScale), (int)(rect.Y * DpiScale), (int)(rect.Width * DpiScale), (int)(rect.Height * DpiScale));
    private static double DpiScale => ServiceLocator.GameWindowService.DpiScale;

    private void UpdateSmoothMoveStoryboard(Avalonia.Point start, Avalonia.Point end)
    {
        ((Setter)SmoothMoveStoryboard.Children[0].Setters[0]).Value = start.X;
        ((Setter)SmoothMoveStoryboard.Children[0].Setters[1]).Value = start.Y;
        ((Setter)SmoothMoveStoryboard.Children[1].Setters[0]).Value = end.X;
        ((Setter)SmoothMoveStoryboard.Children[1].Setters[1]).Value = end.Y;
    }

    private void InitializeTouch(RoutedEventArgs args)
    {
        var parent = this.GetLogicalParent<Grid>() ?? throw new InvalidOperationException();

        var smoothMoveCompletedSubject = new Subject<Unit>();

        // todo?
        var raisePointerReleasedSubject = new Subject<PointerReleasedEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed();
        var pointerMovedStream = Touch.RxPointerMoved();
        var pointerReleasedStream = Touch.RxPointerReleased().Merge(raisePointerReleasedSubject);

        pointerPressedStream
            .Select(_ => XDpi(parent.Bounds).Size)
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        Avalonia.Rect TouchRect() => 
            new Avalonia.Rect(new(TouchTransform.X, TouchTransform.Y), Touch.Bounds.Size);
        smoothMoveCompletedSubject
            .Select(_ => XDpi(TouchRect()))
            .Prepend(XDpi(TouchRect()))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));
        
        pointerReleasedStream
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetPosition(parent);
                var distanceToElement = pointer.GetPosition(Touch);
                var touchPos = distanceToOrigin - distanceToElement;
                return (touchPos, CalculateTouchFinalPosition(DotNetSize(parent.Bounds.Size), DotPoint(touchPos), (int)Touch.Width));
            })
            .Subscribe(async pair =>
            {
                var (startPos, stopPos) = pair;
                UpdateSmoothMoveStoryboard(startPos, new (stopPos.X, stopPos.Y));
                await SmoothMoveStoryboard.RunAsync(Touch);
                smoothMoveCompletedSubject.OnNext(Unit.Default);
            });

        var draggingStream =
            pointerPressedStream
            .SelectMany(pressedEvent =>
            {
                // Origin   Element
                // *--------*--*------
                //             Pointer 
                var distanceToElement = pressedEvent.GetPosition(Touch);

                return
                    pointerMovedStream
                    .TakeUntil(pointerReleasedStream)
                    .Select(movedEvent =>
                    {
                        var distanceToOrigin = movedEvent.GetPosition(parent);
                        var delta = distanceToOrigin - distanceToElement;
                        return new { Delta = delta, MovedEvent = movedEvent };
                    });
            });

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos => (TouchTransform.X, TouchTransform.Y) = newPos);

        // Touch ÍÏ¶¯±ß½çÊÍ·Å¼ì²â
        var boundaryExceededStream =
        draggingStream
            .Where(item => IsBeyondBoundary(DotPoint(item.Delta), Touch.Width, DotNetSize(parent.Bounds.Size)))
            .Select(item => item.MovedEvent);

        //boundaryExceededStream
        //    .Subscribe(raisePointerReleasedSubject.OnNext);

        //this.RxLoaded()
        //    .Select(_ => this.FindParent<Panel>() ?? throw new InvalidOperationException())
        //    .Subscribe(InitializeTouchControl);

        //Touch.RenderTransform = TouchTransform;

        //TouchTransform.X = 200;
        //TouchTransform.Y = 200;
        //var TouchTransition = new ThicknessTransition() { Duration = ReleaseToEdgeDuration, Property = MarginProperty };

        //// Add ThicknessTransition to the Button
        //Touch.Transitions = new Transitions
        //    {
        //        TouchTransition
        //    };

        //var a = 10;
        //Touch.PointerPressed += (s, e) =>
        //{
        //    Touch.Margin = new Avalonia.Thickness(a);
        //    a += 10;
        //};
    }

    private static bool IsBeyondBoundary(Point newPos, double touchSize, Size container)
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
    private static Point CalculateTouchFinalPosition(Size container, Point initPos, int touchSize)
    {
        const int TouchSpace = 2;

        var xMidline = container.Width / 2;
        var right = container.Width - initPos.X - touchSize;
        var bottom = container.Height - initPos.Y - touchSize;

        var hSnapLimit = touchSize / 2;
        var vSnapLimit = touchSize / 3 * 2;

        var centerToLeft = initPos.X + hSnapLimit;

        bool VCloseTo(int distance) => distance < vSnapLimit;
        bool HCloseTo(int distance) => distance < hSnapLimit;

        int AlignToRight() => container.Width - touchSize - TouchSpace;
        int AlignToBottom() => container.Height - touchSize - TouchSpace;

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

static partial class ObservableEventsExtensions
{
    public static Observable<PointerPressedEventArgs> RxPointerPressed(this Control data) =>
        Observable.FromEvent<EventHandler<PointerPressedEventArgs>, PointerPressedEventArgs>(
            h => (sender, e) => h(e),
            e => data.PointerPressed += e,
            e => data.PointerPressed -= e);

    public static Observable<PointerEventArgs> RxPointerMoved(this Control data) =>
        Observable.FromEvent<EventHandler<PointerEventArgs>, PointerEventArgs>(
            h => (sender, e) => h(e),
            e => data.PointerMoved += e,
            e => data.PointerMoved -= e);

    public static Observable<PointerReleasedEventArgs> RxPointerReleased(this Control data) =>
        Observable.FromEvent<EventHandler<PointerReleasedEventArgs>, PointerReleasedEventArgs>(
            h => (sender, e) => h(e),
            e => data.PointerReleased += e,
            e => data.PointerReleased -= e);

    public static Observable<RoutedEventArgs> RxLoaded(this UserControl data) =>
        Observable.FromEvent<EventHandler<RoutedEventArgs>, RoutedEventArgs>(
            h => (sender, e) => h(e),
            e => data.Loaded += e,
            e => data.Loaded -= e);
}

//static class VisualTreeHelperExtensions
//{
//    public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
//    {
//        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
//        if (parentObject == null)
//            return null;

//        return parentObject is T parent ? parent : FindParent<T>(parentObject);
//    }
//}
