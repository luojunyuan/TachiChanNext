using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using System;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;

namespace TouchChan;

public sealed partial class TouchControl : UserControl
{
    private readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard TranslationStoryboard = new();
    private readonly DoubleAnimation TranslateXAnimation;
    private readonly DoubleAnimation TranslateYAnimation;

    // WAS Shit 5: Xaml Code-Behind 中 require 或 prop init 会生成错误的代码 #8723
    public Action<Size>? ResetWindowObservable { get; set; }

    public Action<Rectangle>? SetWindowObservable { get; set; }

    public TouchControl()
    {
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
        var smoothMoveCompletedStream = TranslationStoryboard.RxCompleted();

        var raisePointerReleasedSubject = new Subject<PointerRoutedEventArgs>();

        var pointerPressedStream = Touch.RxPointerPressed();
        var pointerMovedStream = Touch.RxPointerMoved();
        var pointerReleasedStream = Touch.RxPointerReleased().Merge(raisePointerReleasedSubject);

        // FIXME: 连续点击两下按住，第一下的Release时间在200ms之后（也就是第二次按住后）触发了
        // 要确定释放过程中能不能被点击抓住
        pointerPressedStream
            .Select(_ => container.ActualSizeXDpi())
            .Subscribe(clientArea => ResetWindowObservable?.Invoke(clientArea));

        smoothMoveCompletedStream
            .Select(_ => GetTouchRect().XDpi())
            .Prepend(GetTouchRect().XDpi())
            .Subscribe(rect => SetWindowObservable?.Invoke(rect));

        // Touch 释放时的移动动画
        pointerReleasedStream
            .Select(pointer =>
            {
                var distanceToOrigin = pointer.GetCurrentPoint(container).Position.ToWarp();
                var distanceToElement = pointer.GetCurrentPoint(Touch).Position;
                var touchPos = distanceToOrigin - distanceToElement;
                return CalculateTouchFinalPosition(container.ActualSize.ToSize(), touchPos, (int)Touch.Width);
            })
            .Subscribe(stopPos =>
            {
                (TranslateXAnimation.To, TranslateYAnimation.To) = (stopPos.X, stopPos.Y);
                TranslationStoryboard.Begin();
            });

        var draggingStream =
            pointerPressedStream
            .Do(e => Touch.CapturePointer(e.Pointer))
            .SelectMany(pressedEvent =>
            {
                // Origin   Element
                // *--------*--*------
                //             Pointer 
                var distanceToElement = pressedEvent.GetCurrentPoint(Touch).Position;

                return
                    pointerMovedStream
                    .TakeUntil(pointerReleasedStream
                               .Do(e => Touch.ReleasePointerCapture(e.Pointer)))
                    .Select(movedEvent =>
                    {
                        var distanceToOrigin = movedEvent.GetCurrentPoint(container).Position.ToWarp();
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

        // Touch 拖动边界释放检测
        var boundaryExceededStream =
        draggingStream
            .Where(item => IsBeyondBoundary(item.Delta, Touch.Width, container.ActualSize.ToSize()))
            .Select(item => item.MovedEvent);

        boundaryExceededStream
            .Subscribe(raisePointerReleasedSubject.OnNext);
    }

    private Rectangle GetTouchRect() =>
        new((int)((TranslateTransform)Touch.RenderTransform).X,
            (int)((TranslateTransform)Touch.RenderTransform).Y,
            (int)Touch.Width,
            (int)Touch.Height);

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
            HCloseTo(left)  && VCloseTo(top) ? new Point(TouchSpace, TouchSpace) :
            HCloseTo(right) && VCloseTo(top) ? new Point(AlignToRight(), TouchSpace) :
            HCloseTo(left)  && VCloseTo(bottom) ? new Point(TouchSpace, AlignToBottom()) :
            HCloseTo(right) && VCloseTo(bottom) ? new Point(AlignToRight(), AlignToBottom()) :
                               VCloseTo(top) ? new Point(left, TouchSpace) :
                               VCloseTo(bottom) ? new Point(left, AlignToBottom()) :
            centerToLeft < xMidline ? new Point(TouchSpace, top) :
         /* centerToLeft >= xMidline */           new Point(AlignToRight(), top);
    }
}