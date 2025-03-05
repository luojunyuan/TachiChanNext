using R3;

namespace TouchChan.ViewModel;

public class AssistiveTouchViewModel
{
    public BindableReactiveProperty<bool> IsMenuShowed { get; } = new(false);
}
