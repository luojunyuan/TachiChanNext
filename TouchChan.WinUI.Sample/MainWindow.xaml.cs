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

            //this.Root.Events().Loaded.Subscribe(_ => Debug.WriteLine(Unit.Default));

            // 为什么是600，如果是800 dpi*1 的游戏窗口岂不是不会用小按钮
            //this.Root.Events().SizeChanged
            //    .Select(x => x.NewSize)
            //    .Subscribe(window => Touch.Width = window.Width < 600 ? 60 : 80);
        }
    }
}
