using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using R3;
using R3.ObservableEvents;

namespace TouchChan.WinUI.Sample
{
    public sealed partial class MainWindow : Window
    {
        public static void Mine()
        {
            var a = Instance. AppWindow.ClientSize;
            Instance.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(a.Width + 10, a.Height + 10));
        }

        public static MainWindow Instance { get; private set; } = null!;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
            //this.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(800, 600));
            //this.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1280, 720));
            this.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(1920, 1080));

            // TODO: 仅测试，实际情况应该在外部窗口循环触发
            this.Root.Events().Loaded.Select(_ => Unit.Default).Subscribe(GameContext.WindowAttached.OnNext);

            AnimationTool.InputBlocked.Subscribe(block => this.Root.IsHitTestVisible = !block);
        }
    }
}

// events
// Root.SizeChanged -> TouchControl.Loaded -> Root.Loaded

// 最好的效果当然是窗口改变时，动画也能正常移动到他应该到达的位置
// 1 避免在构造函数里访问父控件
// 2 避免让子控件感知父控件的存在
// 3 利用 xaml 做声明式处理，与 rx

// 苹果的小圆点不仅仅是依赖释放位置来判断动画和停靠，如果拖拽释放速度小于一个值，就是按照边缘动画恢复。
// 如果拖拽释放速度大于一个值，还有加速度作用在控件上往速度方向飞出去

// 基本上拿 Size 都是 Touch.ActualSize.ToSize()
// 拿 double 都是 Touch.Width