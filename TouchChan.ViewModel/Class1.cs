namespace TouchChan.ViewModel
{
    public class Class1
    {

    }

    record TouchDockAnchor(TouchCorner Corner, double Scale);

    enum TouchCorner
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
}
