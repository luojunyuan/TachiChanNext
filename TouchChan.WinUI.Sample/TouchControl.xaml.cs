using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

public sealed partial class TouchControl // State
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;
    private readonly ObservableAsPropertyHelper<bool> _isTouchDockedHelper;

    /// <summary>
    /// 指示触摸按钮当前的停靠位置或者即将停靠的位置。
    /// </summary>
    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;

    /// <summary>
    /// 指示触摸按钮是否停靠在窗口边缘。
    /// </summary>
    public bool IsTouchDocked => _isTouchDockedHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    public TouchControl()
    {
        InitializeComponent();

        TouchDockSubscribe();
        TouchDraggingSubscribe(out var whenDragStarted, out var whenDragEnded);
        TouchOpacitySubscribe();

        _currentDockHelper = Observable.Merge(
            whenDragEnded.Select(_ => PositionCalculator.CalculateTouchFinalDockAnchor(
                this.Size(), TouchRect)),
            this.Events().SizeChanged.Select(
                x => PositionCalculator.TouchDockCornerRedirect(x.NewSize, CurrentDock, TouchRect.Width)))
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));

        _isTouchDockedHelper = Observable.Merge(
            whenDragStarted.Select(_ => false),
            _touchAnimationsController.TouchDockCompleted.Select(_ => true))
            .ToProperty(initialValue: true);

        // * 单独建立 Menu，全新构建这个控件，保留 scale 和 width 两套动画，保证可替换的代码质量
        // * Menu 动画窗口大小改变后的 from 不对。老生话题了
        // * 测试触控上的 Dragging 以及各个交互的触发 log 情况
        // TODO: 
        // Perf
        // * 复原 ScaleTransform 的动画以备不需
        // B 测试空白 uwp 动画执行过程中改变 To 怎么做并应用到 TouchControl
        // - 性能测试空项目 double 转 int，分别看 aot 和 jit，x32dbg跑起来或者IDA静态分析
    }

    /// <summary>
    /// 触摸按钮停靠在窗口边缘时，窗口大小改变时自动刷新触摸按钮位置。
    /// </summary>
    private void TouchDockSubscribe()
    {
        // QUES: 为什么选择 600
        const int windowSizeThreshold = 600;
        Observable.Merge(
            // 小白点停靠时
            this.Events().SizeChanged.Where(_ => IsTouchDocked == true).Select(x => x.NewSize),
            // 小白点释放后
            _touchAnimationsController.TouchDockCompleted.Select(_ => this.Size()))
            .Select(window => PositionCalculator.CalculateTouchDockRect(window, CurrentDock,
                window.Width < windowSizeThreshold || window.Height < windowSizeThreshold ? 60 : 80))
            .Subscribe(touchRect => TouchRect = touchRect);
    }

    private readonly TouchAnimations _touchAnimationsController = new();

    /// <summary>
    /// 订阅触摸按钮的拖拽事件，处理拖拽开始、拖拽结束和拖拽动画。
    /// </summary>
    private void TouchDraggingSubscribe(
        out Observable<PointerRoutedEventArgs> dragStartedStream,
        out Observable<PointerRoutedEventArgs> dragEndedStream)
    {
        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream =
            Touch.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Do(e => Touch.CapturePointer(e.Pointer))
            .Share();
        var pointerMovedStream =
            Touch.Events().PointerMoved
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Share();
        var pointerReleasedStream =
            Touch.Events().PointerReleased
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer))
            .Share();

        dragStartedStream =
            pointerPressedStream
            .SelectMany(pressEvent =>
                pointerMovedStream
                .Skip(1)
                .Where(moveEvent =>
                {
                    var pressPos = pressEvent.GetPosition();
                    var movePos = moveEvent.GetPosition();
                    return pressPos != movePos;
                })
                .Take(1)
                .TakeUntil(pointerReleasedStream))
            .Share();

        dragEndedStream =
            dragStartedStream
            .SelectMany(_ =>
                pointerReleasedStream
                .Take(1))
            .Share();

        // Timeline -->
        // |
        // |    Pressed suddenly released
        // | x -*|----->
        // |
        // |    Dragging
        // | x -*--*---------*------->
        // |       Released  Released
        // |      (by raise)
        // |                 ↓
        // |                 DragEnded
        // |                -*---------------------*|-->
        // |                 Start    Animation    End

        // Touch 的拖拽逻辑
        var draggingStream =
            dragStartedStream
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
                        var distanceToOrigin = movedEvent.GetPosition();
                        var delta = distanceToOrigin.Subtract(distanceToElement);

                        return new { Delta = delta, MovedEvent = movedEvent };
                    });
            })
            .Share();

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos => (TouchTransform.X, TouchTransform.Y) = (newPos.X, newPos.Y));

        // Touch 拖动边界检测
        var boundaryExceededStream =
            draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(
                item.Delta, TouchRect.Width, this.Size()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch 拖拽释放停靠到边缘的动画
        _touchAnimationsController.BindingDraggingReleaseAnimations(TouchTransform);
        moveAnimationStartedStream
            .Select(_ => PositionCalculator.CalculateTouchFinalPosition(
                this.Size(), TouchRect))
            .Subscribe(_touchAnimationsController.StartTouchTranslateAnimation);

        // Touch 透明动画触发支持
        pointerPressedStream.Select(_ => Unit.Default).Subscribe(_touchPreviewPressedSubject.OnNext);

        // Menu 相关定义动画
        MenuSubscribe(dragStartedStream, pointerPressedStream, pointerReleasedStream);
    }

    private readonly Subject<Unit> _touchPreviewPressedSubject = new();
    private readonly Subject<Unit> _touchHoldReleaseSubject = new();

    /// <summary>
    /// 订阅触摸按钮的透明度变化动画逻辑，处理触摸按钮按下、释放和淡入淡出动画。
    /// </summary>
    private void TouchOpacitySubscribe()
    {
        var touchOpacityController = new TouchOpacityAnimation();

        touchOpacityController.BindingOpacityAnimations(Touch);

        _touchPreviewPressedSubject.Subscribe(_ => touchOpacityController.StartTouchFadeInAnimation(Touch.Opacity));

        Observable.Merge(
            GameContext.WindowAttached,
            _touchAnimationsController.TouchDockCompleted,
            _menuAnimationsController.MenuClosed,
            _touchHoldReleaseSubject)
            .Select(_ =>
                Observable.Timer(TouchOpacityAnimation.OpacityFadeDelay)
                .TakeUntil(_touchPreviewPressedSubject))
            .Switch()
            .Where(_ => IsTouchDocked == true)
            .ObserveOn(App.UISyncContext)
            .Subscribe(touchOpacityController.StartTouchFadeOutAnimation);

        // System.ExecutionException
        //程序“[10356] TouchChan.WinUI.Sample.exe”已退出，返回值为 3221225477(0xc0000005) 'Access violation'。
    }

    private Rect TouchRect
    {
        get => new(TouchTransform.X, TouchTransform.Y, Touch.Width, Touch.Width);
        set => (TouchTransform.X, TouchTransform.Y, Touch.Width) = (value.X, value.Y, value.Width);
    }

    private Rect TouchRectToCenterCoordinate(Size container)
    {
        var touchRect = TouchRect;
        return new(
            touchRect.X - (container.Width - touchRect.Width) / 2,
            touchRect.Y - (container.Height - touchRect.Height) / 2,
            touchRect.Width,
            touchRect.Width);
    }
}

public sealed partial class TouchControl // Menu Definition
{
    public const double MenuTouchSizeRatio = 5.0;

    private readonly MenuScaleTransitsAnimations _menuAnimationsController = new();

    /// <summary>
    /// 订阅菜单的动画逻辑，包括点击、拖拽开始和释放事件。
    /// </summary>
    private void MenuSubscribe(
        Observable<PointerRoutedEventArgs> dragStartedStream,
        Observable<PointerRoutedEventArgs> pointerPressedStream,
        Observable<PointerRoutedEventArgs> pointerReleasedStream)
    {
        _menuAnimationsController.BindingMenuTransitionAnimations(FakeTouch,
            MenuBackground, ScaleTransform, MenuTransform);

        const int holdTimeThreshold = 500;

        // 记录小白点按住时间的流，用于区分点击和长按事件
        var pressedHoldTimeStream =
            pointerPressedStream
            .SelectMany(pressEvent =>
            {
                var pressTime = DateTimeOffset.Now;

                return pointerReleasedStream
                    .Take(1)
                    .TakeUntil(dragStartedStream)
                    .Select(_ => (DateTimeOffset.Now - pressTime).TotalMilliseconds);
            })
            .Share();

        var clickStream =
            pressedHoldTimeStream
            .Where(holdTime => holdTime < holdTimeThreshold);

        var holdReleaseStream =
            pressedHoldTimeStream
            .Where(holdTime => holdTime >= holdTimeThreshold);

        holdReleaseStream.Select(_ => Unit.Default)
            .Subscribe(_touchHoldReleaseSubject.OnNext);

        clickStream.ObserveOn(App.UISyncContext).Subscribe(_ =>
        {
            Touch.Visibility = Visibility.Collapsed;
            _menuAnimationsController.StartMenuTransitionAnimation(
                TouchRectToCenterCoordinate(this.Size()));
        });

        Menu.Events().PointerPressed.Subscribe(_ =>
            _menuAnimationsController.StartMenuTransitionAnimationReverse(
                TouchRectToCenterCoordinate(this.Size())));

        _menuAnimationsController.MenuClosed
            .Subscribe(_ => Touch.Visibility = Visibility.Visible);
    }
}

public static partial class XamlConverter
{
    public static Visibility VisibleInverse(Visibility value) =>
        value == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;

    public static CornerRadius CircleCorner(double value, double factor) =>
        double.IsNaN(value) ? default : new(value * factor);

    public static Thickness TouchLayerMargin(double value, string fractionFactor)
    {
        if (value is double.NaN || TryParseFraction(fractionFactor, out double factor) == false)
            return default;

        return new(value * factor);
    }

    public static double SizeMultiply(double value, double factor) => value * factor;

    private static bool TryParseFraction(string fraction, out double factor)
    {
        string[] parts = fraction.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], out double numerator) &&
            double.TryParse(parts[1], out double denominator))
        {
            factor = numerator / denominator;
            return true;
        }

        factor = default;
        return false;
    }
}