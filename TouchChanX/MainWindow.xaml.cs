using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using TouchChanX.Interop;

namespace TouchChanX;

public partial class MainWindow
{
    public nint Handle { get; }
    
    public double Dpi { get; private set; }
    
    public MainWindow()
    {
        InitializeComponent();

        Handle = new WindowInteropHelper(this).EnsureHandle();
        Dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        DpiChanged += (_, args) => Dpi = args.NewDpi.DpiScaleX;
    }
}