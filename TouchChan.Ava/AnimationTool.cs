using Avalonia.Animation;
using Avalonia.Media;
using Avalonia.Styling;

namespace TouchChan.Ava;

static class AnimationTool
{
    public static KeyFrame CreatePointKeyFrame(double timePoint) => new()
    {
        Cue = new Cue(timePoint),
        Setters =
            {
                new Setter(TranslateTransform.XProperty, default),
                new Setter(TranslateTransform.YProperty, default)
            }
    };
}
