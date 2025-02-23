using R3;

namespace TouchChan;

public enum LaunchResult
{
    Success,
    Redirected,
    Failed,
}

public enum TouchCorner
{
    Left,
    Top,
    Right,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

public record struct TouchDockAnchor(TouchCorner Corner, double Scale = default);

public partial class ObservableAsPropertyHelper<T> : IDisposable
{
    private readonly IDisposable _subscription;
    private readonly Observable<T> _observable;
    private T _value;

    public ObservableAsPropertyHelper(Observable<T> observable, T initialValue)
    {
        _observable = observable;
        _value = initialValue;

        _subscription = _observable.Subscribe(v => _value = v);
    }

    public T Value => _value;

    public void Dispose()
    {
        _subscription.Dispose();
        GC.SuppressFinalize(this);
    }
}
