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

