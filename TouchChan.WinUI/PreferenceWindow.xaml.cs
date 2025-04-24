using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using R3;

namespace TouchChan.WinUI;

public sealed partial class PreferenceWindow : Window
{
    public PreferenceWindow()
    {
        this.InitializeComponent();

        this.SystemBackdrop = new MicaBackdrop();
        this.ExtendsContentIntoTitleBar = true;

        this.AppWindow.Resize(new(500, 500)); // Ques: 非 Dpi 感应？
        WinUIEx.WindowManager.Get(this).MinWidth = 500;
        WinUIEx.WindowManager.Get(this).MinHeight = 500;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        Task.Run(() =>
        {
            Observable.Return(Unit.Default)
                .ObserveOn(App.UISyncContext)
                .Subscribe(_ => App.Current.Exit());
        });
    }
}
