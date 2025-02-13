#if WinUI
using Size = Windows.Foundation.Size;
using Rect = Windows.Foundation.Rect;
#elif Avalonia
using Size = Avalonia.Size;
using Rect = Avalonia.Rect;
#endif

namespace TouchChan;

public static class Extensions
{
    public static System.Drawing.Size ToGdiSize(this Size size) => new((int)size.Width, (int)size.Height);

    public static System.Drawing.Rectangle ToGdiRect(this Rect size) => new((int)size.X, (int)size.Y, (int)size.Width, (int)size.Height);

}
