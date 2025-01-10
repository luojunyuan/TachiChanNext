using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace TouchChan.WinUI;

public sealed partial class PreferenceWindow : Window
{
    public PreferenceWindow()
    {
        this.InitializeComponent();

        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;
        WinUIEx.WindowExtensions.SetWindowSize(this, 500, 500);
        WinUIEx.WindowManager.Get(this).MinWidth = 500;
        WinUIEx.WindowManager.Get(this).MinHeight = 500;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
    }
}
