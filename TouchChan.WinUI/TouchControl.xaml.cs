using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using System;
using Windows.Foundation;

namespace TouchChan.WinUI;

public sealed partial class TouchControl : UserControl
{
    private readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard TranslationStoryboard = new();
    private readonly DoubleAnimation TranslateXAnimation;
    private readonly DoubleAnimation TranslateYAnimation;

    // WAS Shit 5: Xaml Code-Behind �� require �� prop init �����ɴ���Ĵ��� #8723
    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rect>? SetWindowObservable { get; set; }

    private readonly GameWindowService GameService;

    public TouchControl()
    {
        GameService = ServiceLocator.GameWindowService;

        this.InitializeComponent();

        this.RxLoaded()
            .Select(_ => this.FindParent<Panel>() ?? throw new InvalidOperationException())
            .Subscribe(InitializeTouchControl);

        TouchTransform.X = 2;
        TouchTransform.Y = 2;

        TranslateXAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        TranslateYAnimation = new DoubleAnimation { Duration = ReleaseToEdgeDuration };
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateXAnimation, TouchTransform, AnimationTool.XProperty);
        AnimationTool.BindingAnimation(TranslationStoryboard, TranslateYAnimation, TouchTransform, AnimationTool.YProperty);
    }

    private void InitializeTouchControl(Panel container)
    {
        var moveAnimationEndedStream = TranslationStoryboard.RxCompleted();

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed().Do(e => Touch.CapturePointer(e.Pointer));
        var pointerMovedStream = Touch.RxPointerMoved();
        var pointerReleasedStream =
            Touch.RxPointerReleased()
            .Merge(raisePointerReleasedSubject)
            .Do(e => Touch.ReleasePointerCapture(e.Pointer));

        var dragStartedStream =
            pointerPressedStream
            .SelectMany(_ =>
                pointerMovedStream
                .Take(1)
                .TakeUntil(pointerReleasedStream));

        // NOTE: ���� drag end ʱ�����������·ſ�ʱ�����λ�þ�����
        var dragEndedStream =
            pointerReleasedStream
            .WithLatestFrom(pointerPressedStream, (releaseEvent, pressedEvent) =>
            {
                var releasePosition = releaseEvent.GetPosition(container);
                var pressedPosition = pressedEvent.GetPosition(container);
                return new { DragReleased = releaseEvent, EndPoint = releasePosition, StartPoint = pressedPosition, };
            })
            .Where(x => x.EndPoint != x.StartPoint)
            .Select(x => x.DragReleased);

        // Time -->
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
                        var distanceToOrigin = movedEvent.GetPosition(container).Warp();
                        var delta = distanceToOrigin - distanceToElement;
                        return new { Delta = delta, MovedEvent = movedEvent };
                    });
            });

        draggingStream
            .Select(item => item.Delta)
            .Subscribe(newPos =>
            {
                TouchTransform.X = newPos.X;
                TouchTransform.Y = newPos.Y;
            });

        // Touch �϶��߽���
        var boundaryExceededStream =
            draggingStream
            .Where(item => PositionCalculator.IsBeyondBoundary(
                item.Delta, Touch.Width, container.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);

        var moveAnimationStartedStream = dragEndedStream;

        // Touch ��Եͣ������
        moveAnimationStartedStream
            .Do(_ => Touch.IsHitTestVisible = false)
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetPosition(container).Warp();
                var distanceToElement = pointer.GetPosition(Touch);
                var touchPos = distanceToOrigin - distanceToElement;
                return PositionCalculator.CalculateTouchFinalPosition(
                    container.ActualSize.ToSize(), touchPos, (int)Touch.Width);
            })
            .Subscribe(stopPos =>
            {
                (TranslateXAnimation.To, TranslateYAnimation.To) = (stopPos.X, stopPos.Y);
                TranslationStoryboard.Begin();
            });

        // �ص������������ڵĿɹ۲�����
        dragStartedStream
            .Select(_ => container.ActualSizeXDpi(GameService.DpiScale))
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        moveAnimationEndedStream
            .Do(_ => Touch.IsHitTestVisible = true)
            .Select(_ => GetTouchRect().XDpi(GameService.DpiScale))
            .Prepend(GetTouchRect().XDpi(GameService.DpiScale))
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));
    }

    private Rect GetTouchRect() =>
        new((int)((TranslateTransform)Touch.RenderTransform).X,
            (int)((TranslateTransform)Touch.RenderTransform).Y,
            (int)Touch.Width,
            (int)Touch.Height);
}