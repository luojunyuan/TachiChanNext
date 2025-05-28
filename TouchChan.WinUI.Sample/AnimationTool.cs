using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

/// <summary>
/// 触摸按钮的动画逻辑，包含拖拽、释放等动画。
/// </summary>
public class TouchAnimations
{
    public Observable<Unit> TouchDockCompleted => _touchReleaseStoryboard.Events().Completed
        .Select(_ => Unit.Default)
        .Share();

    private static readonly TimeSpan ReleaseToEdgeDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard _touchReleaseStoryboard = new();
    private readonly DoubleAnimation _releaseXAnimation = new() { Duration = ReleaseToEdgeDuration };
    private readonly DoubleAnimation _releaseYAnimation = new() { Duration = ReleaseToEdgeDuration };

    public void BindingDraggingReleaseAnimations(TranslateTransform transform)
    {
        _touchReleaseStoryboard.BindingAnimation(_releaseXAnimation, transform, nameof(TranslateTransform.X));
        _touchReleaseStoryboard.BindingAnimation(_releaseYAnimation, transform, nameof(TranslateTransform.Y));
        _touchReleaseStoryboard.Events().Completed.Subscribe(_ => AnimationTool.InputBlocked.OnNext(false));
        //this.Events().SizeChanged.Where(_ => IsTouchDocked == false).Subscribe(_ =>
        //{
        //    var after = PositionCalculator.CalculateTouchFinalPosition(this.Size(), TouchRect);
        //    (_releaseXAnimation.To, _releaseYAnimation.To) = (after.X, after.Y);
        //});
    }

    public void StartTouchTranslateAnimation(Point destination)
    {
        AnimationTool.InputBlocked.OnNext(true);
        (_releaseXAnimation.To, _releaseYAnimation.To) = (destination.X, destination.Y);
        _touchReleaseStoryboard.Begin();
    }
}

public class MenuTransitsAnimations
{
    public Observable<Unit> MenuOpened => _menuOpenedSubject
        .Where(isOpened => isOpened == true)
        .Select(_ => Unit.Default)
        .Share();

    public Observable<Unit> MenuClosed => _menuOpenedSubject
        .Where(isOpened => isOpened == false)
        .Select(_ => Unit.Default)
        .Share();

    // NOTE: 因为 EnableDependentAnimation 导致菜单大小变化可能无法和位移动画完全同步

    private readonly static TimeSpan MenuTransitsDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard _menuTransitionStoryboard = new();
    private readonly DoubleAnimation _menuWidthAnimation = new()
    {
        EnableDependentAnimation = true,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _menuHeightAnimation = new()
    {
        EnableDependentAnimation = true,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _menuTransXAnimation = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _menuTransYAnimation = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _fakeTouchOpacityAnimation = new()
    {
        From = 1,
        To = 0,
        Duration = MenuTransitsDuration,
    };

    private readonly BehaviorSubject<bool> _menuOpenedSubject = new(false);

    public void BindingMenuTransitionAnimations(
        FrameworkElement menu,
        TranslateTransform transform,
        UIElement fakeTouch,
        Observable<Rect> touchRectOnWindowResize)
    {
        _menuTransitionStoryboard.BindingAnimation(_menuWidthAnimation, menu, nameof(FrameworkElement.Width));
        _menuTransitionStoryboard.BindingAnimation(_menuHeightAnimation, menu, nameof(FrameworkElement.Height));
        _menuTransitionStoryboard.BindingAnimation(_menuTransXAnimation, transform, nameof(TranslateTransform.X));
        _menuTransitionStoryboard.BindingAnimation(_menuTransYAnimation, transform, nameof(TranslateTransform.Y));
        _menuTransitionStoryboard.BindingAnimation(_fakeTouchOpacityAnimation, fakeTouch, nameof(UIElement.Opacity));
        _menuTransitionStoryboard.Events().Completed.Subscribe(_ =>
        {
            AnimationTool.InputBlocked.OnNext(false);

            SwapAnimation(_menuWidthAnimation);
            SwapAnimation(_menuHeightAnimation);
            SwapAnimation(_menuTransXAnimation);
            SwapAnimation(_menuTransYAnimation);
            SwapAnimation(_fakeTouchOpacityAnimation);

            _menuOpenedSubject.OnNext(!_menuOpenedSubject.Value);

            static void SwapAnimation(DoubleAnimation animation) =>
                (animation.To, animation.From) = (animation.From, animation.To);
        });
        touchRectOnWindowResize
            .Where(_ => _menuOpenedSubject.Value == true)
            .Subscribe(touchRectCenter =>
            {
                _menuWidthAnimation.To = _menuHeightAnimation.To = touchRectCenter.Width;
                (_menuTransXAnimation.To, _menuTransYAnimation.To) = (touchRectCenter.X, touchRectCenter.Y);
            });
    }

    public void StartMenuTransitionAnimation(Rect touchRectCenter)
    {
        AnimationTool.InputBlocked.OnNext(true);
        _menuWidthAnimation.To = _menuHeightAnimation.To = touchRectCenter.Width * TouchControl.MenuTouchSizeRatio;
        _menuWidthAnimation.From = _menuHeightAnimation.From = touchRectCenter.Width;
        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        (_menuTransXAnimation.From, _menuTransYAnimation.From) = (touchRectCenter.X, touchRectCenter.Y);
        _menuTransitionStoryboard.Begin();
    }

    public void StartMenuTransitionAnimation()
    {
        AnimationTool.InputBlocked.OnNext(true);
        _menuTransitionStoryboard.Begin();
    }
}

public class TouchOpacityAnimation
{
    public static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private readonly DoubleAnimation _fadeInAnimation = new()
    {
        // From = {current}, 
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private readonly DoubleAnimation _fadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    private readonly Storyboard _fadeInOpacityStoryboard = new();
    private readonly Storyboard _fadeOutOpacityStoryboard = new();

    public void BindingOpacityAnimations(UIElement element)
    {
        _fadeInOpacityStoryboard.BindingAnimation(_fadeInAnimation, element, nameof(UIElement.Opacity));
        _fadeOutOpacityStoryboard.BindingAnimation(_fadeOutAnimation, element, nameof(UIElement.Opacity));
    }

    /// <summary>
    /// 触摸按钮淡入动画，实现了动态从当前透明度开始变化。
    /// </summary>
    /// <remarks>FadeIn 动画过程中会锁定触摸按钮不可点击</remarks>
    public void StartTouchFadeInAnimation(double currentOpacity)
    {
        var distance = OpacityFull - currentOpacity;
        if (distance <= 0) return;

        _fadeInAnimation.From = currentOpacity;
        _fadeInAnimation.Duration = new Duration(TimeSpan.FromMilliseconds(
            OpacityFadeInDuration.TotalMilliseconds * (distance / (OpacityFull - OpacityHalf))
        ));

        _fadeInOpacityStoryboard.Begin();
    }

    public void StartTouchFadeOutAnimation(Unit _) => _fadeOutOpacityStoryboard.Begin();
}

public static partial class AnimationTool
{
    /// <summary>
    /// 指示是否需要阻止整个窗口的用户输入。
    /// </summary>
    public static Subject<bool> InputBlocked { get; } = new();

    /// <summary>
    /// 绑定动画到对象的属性。
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

public class MenuScaleTransitsAnimations
{
    public Observable<Unit> MenuOpened => _menuOpenedSubject
        .Where(isOpened => isOpened == true)
        .Select(_ => Unit.Default)
        .Share();

    public Observable<Unit> MenuClosed => _menuOpenedSubject
        .Where(isOpened => isOpened == false)
        .Select(_ => Unit.Default)
        .Share();

    // NOTE: 单 ObjectAnimationUsingKeyFrames 无法 reverse 反转，并且 Storyboard 执行后没法 remove 它重建

    private readonly static TimeSpan MenuTransitsDuration = TimeSpan.FromMilliseconds(200);
    private readonly Storyboard _menuTransitionStoryboard = new();
    private readonly DoubleAnimation _menuTransXAnimation = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _menuTransYAnimation = new() { Duration = MenuTransitsDuration, };
    private const double ScaleOrigin = 1.0;
    private const double ScaleFrom = ScaleOrigin / TouchControl.MenuTouchSizeRatio;
    private readonly DoubleAnimation _menuScaleXAnimation = new()
    {
        From = ScaleFrom,
        To = ScaleOrigin,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _menuScaleYAnimation = new()
    {
        From = ScaleFrom,
        To = ScaleOrigin,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _fakeTouchOpacityAnimation = new()
    {
        From = 1,
        To = 0,
        Duration = MenuTransitsDuration,
    };
    private readonly Storyboard _storyboardReverse = new();
    private readonly DoubleAnimation _menuTransXAnimationReverse = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _menuTransYAnimationReverse = new() { Duration = MenuTransitsDuration, };
    private readonly DoubleAnimation _menuScaleXAnimationReverse = new()
    {
        From = ScaleOrigin,
        To = ScaleFrom,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _menuScaleYAnimationReverse = new()
    {
        From = ScaleOrigin,
        To = ScaleFrom,
        Duration = MenuTransitsDuration,
    };
    private readonly DoubleAnimation _fakeTouchOpacityAnimationReverse = new()
    {
        From = 0,
        To = 1,
        Duration = MenuTransitsDuration,
    };

    private readonly BehaviorSubject<bool> _menuOpenedSubject = new(false);


    public void BindingMenuTransitionAnimations(
        UIElement fakeTouch,
        Border background, ScaleTransform scaleTransform, Transform translateTransform)
    {
        var cornerRadiusAnimation = CreateAntiCornerScaleAnimation();
        _menuTransitionStoryboard.BindingAnimation(_menuScaleXAnimation, scaleTransform, nameof(ScaleTransform.ScaleX));
        _menuTransitionStoryboard.BindingAnimation(_menuScaleYAnimation, scaleTransform, nameof(ScaleTransform.ScaleY));
        _menuTransitionStoryboard.BindingAnimation(_menuTransXAnimation, translateTransform, nameof(TranslateTransform.X));
        _menuTransitionStoryboard.BindingAnimation(_menuTransYAnimation, translateTransform, nameof(TranslateTransform.Y));
        _menuTransitionStoryboard.BindingAnimation(_fakeTouchOpacityAnimation, fakeTouch, nameof(UIElement.Opacity));
        _menuTransitionStoryboard.BindingAnimation(cornerRadiusAnimation, background, nameof(Border.CornerRadius));

        var cornerRadiusAnimationReverse = CreateReverseCornerAnimation(cornerRadiusAnimation);
        _storyboardReverse.BindingAnimation(_menuScaleXAnimationReverse, scaleTransform, nameof(ScaleTransform.ScaleX));
        _storyboardReverse.BindingAnimation(_menuScaleYAnimationReverse, scaleTransform, nameof(ScaleTransform.ScaleY));
        _storyboardReverse.BindingAnimation(_menuTransXAnimationReverse, translateTransform, nameof(TranslateTransform.X));
        _storyboardReverse.BindingAnimation(_menuTransYAnimationReverse, translateTransform, nameof(TranslateTransform.Y));
        _storyboardReverse.BindingAnimation(_fakeTouchOpacityAnimationReverse, fakeTouch, nameof(UIElement.Opacity));
        _storyboardReverse.BindingAnimation(cornerRadiusAnimationReverse, background, nameof(Border.CornerRadius));

        Observable.Merge(
            _menuTransitionStoryboard.Events().Completed,
            _storyboardReverse.Events().Completed)
            .Subscribe(_ =>
            {
                AnimationTool.InputBlocked.OnNext(false);
                _menuOpenedSubject.OnNext(!_menuOpenedSubject.Value);
            });
    }

    public void StartMenuTransitionAnimation(Rect touchRectCenter)
    {
        AnimationTool.InputBlocked.OnNext(true);
        // NOTE: 以中心点为坐标系原点时，原点是触摸按钮的中心点，不再是触摸按钮的左上角
        (_menuTransXAnimation.From, _menuTransYAnimation.From) = (touchRectCenter.X, touchRectCenter.Y);
        _menuTransitionStoryboard.Begin();
    }

    public void StartMenuTransitionAnimationReverse(Rect touchRectCenter)
    {
        AnimationTool.InputBlocked.OnNext(true);
        (_menuTransXAnimationReverse.To, _menuTransYAnimationReverse.To) = (touchRectCenter.X, touchRectCenter.Y);
        _storyboardReverse.Begin();
    }

    private static ObjectAnimationUsingKeyFrames CreateAntiCornerScaleAnimation()
    {
        // NOTE: touch menu 大小变化了理应是圆角变化的，但是没法重建动画，目前来说可以勉强接受 30-150 的圆角变化
        double startRadius = 200;    // 初始角度
        double endRadius = 40;       // 结束角度

        const int fps = 120;
        var totalDuration = MenuTransitsDuration.TotalMilliseconds;

        var cornerRadiusAnimation = new ObjectAnimationUsingKeyFrames
        {
            Duration = MenuTransitsDuration,
        };
        var frames = totalDuration / (1.0 / fps * 1000);
        var millisecondPerFrame = totalDuration / frames;

        for (var i = 0; i < frames; i++)
        {
            var progress = i / (frames - 1);
            //var frameTimeMilliseconds = easeFunction.Ease(progress) * totalDuration;
            var frameTimeMilliseconds = i * millisecondPerFrame;
            var currentRadius = startRadius + (endRadius - startRadius) * progress;

            var keyFrame = new DiscreteObjectKeyFrame
            {
                KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(frameTimeMilliseconds)),
                Value = new CornerRadius(currentRadius, currentRadius, currentRadius, currentRadius)
            };

            cornerRadiusAnimation.KeyFrames.Add(keyFrame);
        }

        return cornerRadiusAnimation;
    }

    private static ObjectAnimationUsingKeyFrames CreateReverseCornerAnimation(
        ObjectAnimationUsingKeyFrames originalAnimation)
    {
        var reverseAnimation = new ObjectAnimationUsingKeyFrames
        {
            Duration = originalAnimation.Duration
        };

        var originalKeyFrames = originalAnimation.KeyFrames.ToList();

        for (int i = 0; i < originalKeyFrames.Count; i++)
        {
            var newKeyFrame = new DiscreteObjectKeyFrame
            {
                KeyTime = originalKeyFrames[i].KeyTime,
                Value = originalKeyFrames[^(i + 1)].Value
            };

            reverseAnimation.KeyFrames.Add(newKeyFrame);
        }

        return reverseAnimation;
    }
}
