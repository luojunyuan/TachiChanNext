using System.Windows;
using System.Windows.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChanX;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        PInvokeCore.SetWindowLong(
            new HWND(new WindowInteropHelper(this).Handle),
            WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT,
            new IntPtr(0x450606));
    }
}