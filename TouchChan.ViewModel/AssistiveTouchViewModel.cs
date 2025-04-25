using R3;

namespace TouchChan.ViewModel;

public class AssistiveTouchViewModel
{
    // TODO：你确定这样做?
    public BindableReactiveProperty<bool> IsMenuShowed { get; } = new(false);
}
