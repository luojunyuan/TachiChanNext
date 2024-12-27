using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace TouchChan;

internal class AnimationTool
{
    // 在设置 storyboard 时使用并且确保绑定对象没有在 TransformGroup 里面
    public const string XProperty = "X";
    public const string YProperty = "Y";

    /// <summary>
    /// Binding animations in storyboard
    /// </summary>
    public static void BindingAnimation(
        Storyboard storyboard,
        Timeline animation,
        DependencyObject target,
        string path)
    {
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, path);
        storyboard.Children.Add(animation);
    }
}
