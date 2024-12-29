using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using R3;

namespace TouchChan.Ava;

public partial class TouchControl : UserControl
{
    private readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);

    private readonly Animation SmoothMoveStoryboard;

    private readonly TranslateTransform TouchTransform = new();

    private readonly GameWindowService GameService;

    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rect>? SetWindowObservable { get; set; }

    public TouchControl()
    {
        GameService = ServiceLocator.GameWindowService;

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

    private void UpdateSmoothMoveStoryboard(Point start, Point end)
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
        var raiseRelesedInCodeSubject = new Subject<PointerEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed();
        var pointerMovedStream = Touch.RxPointerMoved();
        var pointerReleasedStream =
            Touch.RxPointerReleased()
            .Select(releasedEvent => releasedEvent as PointerEventArgs)
            .Merge(raiseRelesedInCodeSubject);

        pointerPressedStream
            .Select(_ => parent.Bounds.XDpi(GameService.DpiScale).Size)
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        Rect TouchRect() =>
            new(new(TouchTransform.X, TouchTransform.Y), Touch.Bounds.Size);
        smoothMoveCompletedSubject
            .Select(_ => TouchRect().XDpi(GameService.DpiScale))
            .Prepend(TouchRect().XDpi(GameService.DpiScale))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));

        pointerReleasedStream
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetPosition(parent);
                var distanceToElement = pointer.GetPosition(Touch);
                var touchPos = distanceToOrigin - distanceToElement;
                return (touchPos, PositionCalculator.CalculateTouchFinalPosition(parent.Bounds.Size, touchPos, (int)Touch.Width));
            })
            .Subscribe(async pair =>
            {
                var (startPos, stopPos) = pair;
                UpdateSmoothMoveStoryboard(startPos, new(stopPos.X, stopPos.Y));
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
            .Where(item => PositionCalculator.IsBeyondBoundary(item.Delta, Touch.Width, parent.Bounds.Size))
            .Select(item => item.MovedEvent);

        // FIXME: ×²Ïò±ßÔµÍÏ×§ÊÍ·Å£¬¿É¹Û²âÇøÓòÉÁË¸
        boundaryExceededStream
            .Subscribe(raiseRelesedInCodeSubject.OnNext);
    }
}

static partial class ObservableEventsExtensions
{
    public static Rect XDpi(this Rect rect, double factor) =>
        new(rect.X * factor, rect.Y * factor, rect.Width * factor, rect.Height * factor);

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
