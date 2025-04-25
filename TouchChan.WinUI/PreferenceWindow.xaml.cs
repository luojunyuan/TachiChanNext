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
