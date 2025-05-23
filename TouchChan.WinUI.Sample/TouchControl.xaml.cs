using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

public sealed partial class TouchControl // State
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;
    private readonly ObservableAsPropertyHelper<bool> _isTouchDockedHelper;

    /// <summary>
    /// ָʾ������ť��ǰ��ͣ��λ�û��߼���ͣ����λ�á�
    /// </summary>
    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;

    /// <summary>
    /// ָʾ������ť�Ƿ�ͣ���ڴ��ڱ�Ե��
    /// </summary>
    public bool IsTouchDocked => _isTouchDockedHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    public TouchControl()
    {
        InitializeComponent();

        TouchDockSubscribe();
        TouchDraggingSubscribe(
            out Observable<PointerRoutedEventArgs> whenDragStarted,
            out Observable<PointerRoutedEventArgs> whenDragEnded);
        TouchOpacitySubscribe();

        _currentDockHelper = Observable.Merge(
            whenDragEnded.Select(pointer => PositionCalculator.CalculateTouchFinalDockAnchor(
                this.ActualSize.ToSize(), GetCurrentTouchRectByPointer(pointer))),
            this.Events().SizeChanged.Select(
                x => PositionCalculator.TouchDockCornerRedirect(x.NewSize, CurrentDock, Touch.Width)))
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));

        _isTouchDockedHelper = Observable.Merge(
            whenDragStarted.Select(_ => false),
            TouchReleaseStoryboard.Events().Completed.Select(_ => true))
            .ToProperty(initialValue: true);

        // TODO: Ŀǰ�뵽������˼·�����϶��ͷ���������ʱ�򣬴��ڴ�С�ı��ˣ��ڶ�������ʱ���������� Touch λ��
        // �����ڶ��������У��ı䶯���� To ֵ

        // NOTE: �����ĵ�Ϊ����ϵԭ��ʱ��ԭ���Ǵ�����ť�����ĵ㣬�����Ǵ�����ť�����Ͻ�
        //var offset = new Point((this.ActualWidth - Touch.Width) / 2, (this.ActualHeight - Touch.Width) / 2);
        // �����õĴ���
        //.Do(_ => _isAnimating.OnNext(true))
        //Touch.Events().PointerPressed.Where(p => p.GetCurrentPoint(this).Properties.IsLeftButtonPressed).Subscribe(_ => StartTouchFadeInAnimation(Touch));
    }

    /// <summary>
    /// ������ťͣ���ڴ��ڱ�Եʱ�����ڴ�С�ı�ʱ�Զ�ˢ�´�����ťλ�á�
    /// </summary>
    private void TouchDockSubscribe()
    {
        // QUES: Ϊʲô�� 600
        const int windowWidthThreshold = 600;
        Observable.Merge(
            this.Events().SizeChanged.Where(_ => IsTouchDocked == true).Select(x => x.NewSize),
            TouchReleaseStoryboard.Events().Completed.Select(_ => this.ActualSize.ToSize()))
            .Select(window => PositionCalculator.CalculateTouchDockRect(
                window, CurrentDock, window.Width < windowWidthThreshold ? 60 : 80))
            .Subscribe(SetTouchDockRect);
    }

    private void TouchDraggingSubscribe(
        out Observable<PointerRoutedEventArgs> dragStartedStream,
        out Observable<PointerRoutedEventArgs> dragEndedStream)
    {
        // Note: Ĭ��������קʱ���ڴ�С���ᷢ���ı�

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream =
            Touch.Events().PointerPressed
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            .Do(e => Touch.CapturePointer(e.Pointer));
        var pointerMovedStream =
            Touch.Events().PointerMoved
            .Where(e => e.GetCurrentPoint(null).Properties.IsLeftButtonPressed);
        var pointerReleasedStream =
            Touch.Events().PointerReleased
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer));

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
                .TakeUntil(pointerReleasedStream));

        dragEndedStream =
            dragStartedStream
            .SelectMany(_ =>
                pointerReleasedStream
                .Take(1));

        // Timeline -->
        // |
        // |    Pressed suddenly released
        // | x -*|----->
        // |
        // |    Dragging
        // | x -*--*---------*------->
        // |       Released  Released
        // |      (by raise)
        // |                 ��
        // |                 DragEnded
        // |                -*---------------------*|-->
        // |                 Start    Animation    End

        // Touch ����ק�߼�
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
            });

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos =>
                (TouchTransform.X, TouchTransform.Y) = (newPos.X, newPos.Y));

        // Touch �϶��߽���
        var boundaryExceededStream =
            draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(
                item.Delta, Touch.Width, this.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch ��ק�ͷ�ͣ������Ե�Ķ���
        BindingDraggingReleaseAnimations();
        moveAnimationStartedStream
            .Select(pointer => PositionCalculator.CalculateTouchFinalPosition(
                this.ActualSize.ToSize(), GetCurrentTouchRectByPointer(pointer)))
            .Subscribe(StartTranslateAnimation);
    }

    private void TouchOpacitySubscribe()
    {
        BindingOpacityAnimations(Touch);

        GameContext.WindowAttached
            .Select(_ => Observable.Timer(OpacityFadeDelay))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(StartTouchFadeOutAnimation);
    }

    private Rect GetCurrentTouchRectByPointer(PointerRoutedEventArgs pointer)
    {
        var distanceToOrigin = pointer.GetPosition();
        var distanceToElement = pointer.GetPosition(Touch);
        var touchPos = distanceToOrigin.Subtract(distanceToElement);

        return new(touchPos, Touch.ActualSize.ToSize());
    }

    /// <summary>
    /// ���ô�����ťͣ����ԵʱӦ���ڵ�λ�á�
    /// </summary>
    /// <param name="touchRect">���� Touch ������λ��</param>
    private void SetTouchDockRect(Rect touchRect) =>
        (TouchTransform.X, TouchTransform.Y, Touch.Width)
            = (touchRect.X, touchRect.Y, touchRect.Width);
}

public sealed partial class TouchControl // Animation Dragging Release
{
    private readonly static TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(2000);
    private readonly Storyboard TouchReleaseStoryboard = new();
    private readonly DoubleAnimation ReleaseXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation ReleaseYAnimation = new() { Duration = ReleaseToEdgeDuration };

    private void BindingDraggingReleaseAnimations()
    {
        TouchReleaseStoryboard.BindingAnimation(ReleaseXAnimation, TouchTransform, nameof(TranslateTransform.X));
        TouchReleaseStoryboard.BindingAnimation(ReleaseYAnimation, TouchTransform, nameof(TranslateTransform.Y));
        TouchReleaseStoryboard.Events().Completed.Subscribe(_ => AnimationTool.InputBlocked.OnNext(false));
    }

    private void StartTranslateAnimation(Point destination)
    {
        ReleaseXAnimation.To = destination.X;
        ReleaseYAnimation.To = destination.Y;
        AnimationTool.InputBlocked.OnNext(true);
        TouchReleaseStoryboard.Begin();
    }
}

public sealed partial class TouchControl // Animation Opacity
{
    private static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private readonly DoubleAnimation FadeInAnimation = new()
    {
        // From = {current}, 
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private readonly DoubleAnimation FadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    private readonly Storyboard FadeInOpacityStoryboard = new();
    private readonly Storyboard FadeOutOpacityStoryboard = new();

    private void BindingOpacityAnimations(UIElement element)
    {
        FadeInOpacityStoryboard.BindingAnimation(FadeInAnimation, element, nameof(Opacity));
        FadeOutOpacityStoryboard.BindingAnimation(FadeOutAnimation, element, nameof(Opacity));

        FadeInOpacityStoryboard.Events().Completed.Subscribe(_ => element.IsHitTestVisible = true);
    }

    /// <summary>
    /// ������ť���붯����ʵ���˶�̬�ӵ�ǰ͸���ȿ�ʼ�仯��
    /// </summary>
    /// <remarks>FadeIn ���������л�����������ť���ɵ��</remarks>
    private void StartTouchFadeInAnimation(UIElement element)
    {
        element.IsHitTestVisible = false;

        var currentOpacity = element.Opacity;

        var distance = OpacityFull - currentOpacity;
        if (distance <= 0) return;

        FadeInAnimation.From = currentOpacity;
        FadeInAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(
            OpacityFadeInDuration.TotalMilliseconds * (distance / (OpacityFull - OpacityHalf))
        ));

        FadeInOpacityStoryboard.Begin();
    }

    private void StartTouchFadeOutAnimation(Unit _) => FadeOutOpacityStoryboard.Begin();
}

public static partial class AnimationTool
{
    /// <summary>
    /// ָʾ�Ƿ���Ҫ��ֹ�������ڵ��û����롣
    /// </summary>
    public static Subject<bool> InputBlocked { get; } = new();

    /// <summary>
    /// �󶨶�������������ԡ�
    /// </summary>
    public static void BindingAnimation(
        this Storyboard storyboard,
        Timeline animation,
        DependencyObject target,
        string path)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        storyboard.Children.Add(animation);
    }
}
