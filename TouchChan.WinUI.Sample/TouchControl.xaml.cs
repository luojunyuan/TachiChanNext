using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using R3;
using R3.ObservableEvents;
using Windows.Foundation;

namespace TouchChan.WinUI.Sample;

public sealed partial class TouchControl // State
{
    private readonly ObservableAsPropertyHelper<TouchDockAnchor> _currentDockHelper;

    public TouchDockAnchor CurrentDock => _currentDockHelper.Value;
}

public sealed partial class TouchControl : UserControl
{
    public TouchControl()
    {
        InitializeComponent();

        _currentDockHelper = Observable.Never<TouchDockAnchor>()
            .ToProperty(initialValue: new(TouchCorner.Left, 0.5));

        // QUES: Ϊʲô�� 600
        const int windowWidthThreshold = 600;
        this.Events().SizeChanged
            .Select(x => x.NewSize)
            .Select(window =>
            {
                var touchRect = PositionCalculator.CalculateTouchDockRect(
                    window, CurrentDock, window.Width < windowWidthThreshold ? 60 : 80);

                // NOTE: �����ĵ�Ϊ����ϵԭ��ʱ��ԭ���Ǵ�����ť�����ĵ㣬�����Ǵ�����ť�����Ͻ�
                var offset = new Point((window.Width - touchRect.Width) / 2, (window.Height - touchRect.Width) / 2);
                return (touchRect, offset);
            })
            .Subscribe(SetTouchDockRect);

        // can be faded or not ֻ����ͣʱ���ܹ������뵭��
        AnimationTool.InitializeOpacityAnimations(Touch);
        GameContext.WindowAttached
            .Select(_ => Observable.Timer(AnimationTool.OpacityFadeDelay))
            .Switch()
            .ObserveOn(App.UISyncContext)
            .Subscribe(AnimationTool.StartTouchFadeOutAnimation);
    }

    /// <summary>
    /// ���ô�����ťͣ��ʱӦ���ڵ�λ��
    /// </summary>
    /// <param name="touchRect">���� Touch ������λ�ã���������ϵ��</param>
    /// <param name="centerOffset">��ͬ����ϵ�´�����ť���ĵ�ľ���ֵ</param>
    private void SetTouchDockRect(Rect touchRect, Point centerOffset) =>
        (TouchTransform.X, TouchTransform.Y, Touch.Width)
            = (touchRect.X - centerOffset.X, touchRect.Y - centerOffset.Y, touchRect.Width);
}

// 1 �����ڹ��캯������ʸ��ؼ�
// 2 �������ӿؼ���֪���ؼ��Ĵ���
// 3 ���� xaml ������ʽ������ rx

file static partial class AnimationTool
{
    /// <summary>
    /// �󶨶��������������
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

file static partial class AnimationTool // Touch Opacity Animations
{
    private static readonly TimeSpan OpacityFadeInDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan OpacityFadeOutDuration = TimeSpan.FromMilliseconds(400);
    private const double OpacityHalf = 0.4;
    private const double OpacityFull = 1;
    private static readonly DoubleAnimation FadeInAnimation = new()
    {
        From = OpacityHalf,
        To = OpacityFull,
        Duration = OpacityFadeInDuration,
    };
    private static readonly DoubleAnimation FadeOutAnimation = new()
    {
        From = OpacityFull,
        To = OpacityHalf,
        Duration = OpacityFadeOutDuration,
    };
    private static readonly Storyboard FadeInOpacityStoryboard = new();
    private static readonly Storyboard FadeOutOpacityStoryboard = new();

    public static readonly TimeSpan OpacityFadeDelay = TimeSpan.FromMilliseconds(4000);

    public static void InitializeOpacityAnimations(FrameworkElement touch)
    {
        FadeInOpacityStoryboard.BindingAnimation(FadeInAnimation, touch, nameof(FrameworkElement.Opacity));
        FadeOutOpacityStoryboard.BindingAnimation(FadeOutAnimation, touch, nameof(FrameworkElement.Opacity));
    }

    public static void StartTouchFadeInAnimation(Unit _) => FadeInOpacityStoryboard.Begin();

    public static void StartTouchFadeOutAnimation(Unit _) => FadeOutOpacityStoryboard.Begin();

    public static bool IsFullyOpaque(this UIElement element, double tolerance = 0.001)
        => Math.Abs(element.Opacity - OpacityFull) < tolerance;
}