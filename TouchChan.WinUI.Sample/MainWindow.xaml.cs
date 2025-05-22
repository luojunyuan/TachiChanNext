using Microsoft.UI.Xaml;
using R3;
using R3.ObservableEvents;

namespace TouchChan.WinUI.Sample
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // this.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(800, 600));
            this.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1280, 720));

            this.Root.Events().Loaded.Select(_ => Unit.Default).Subscribe(GameContext.WindowAttached.OnNext);
        }
    }
}

// events
// Root.SizeChanged -> TouchControl.Loaded -> Root.Loaded