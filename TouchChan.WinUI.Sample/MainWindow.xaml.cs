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

            // TODO: �����ԣ�ʵ�����Ӧ�����ⲿ����ѭ������
            this.Root.Events().Loaded.Select(_ => Unit.Default).Subscribe(GameContext.WindowAttached.OnNext);

            AnimationTool.InputBlocked.Subscribe(block => this.Root.IsHitTestVisible = !block);
        }
    }
}

// events
// Root.SizeChanged -> TouchControl.Loaded -> Root.Loaded

// ��õ�Ч����Ȼ�Ǵ��ڸı�ʱ������Ҳ�������ƶ�����Ӧ�õ����λ��
// 1 �����ڹ��캯������ʸ��ؼ�
// 2 �������ӿؼ���֪���ؼ��Ĵ���
// 3 ���� xaml ������ʽ������ rx

// ƻ����СԲ�㲻�����������ͷ�λ�����ж϶�����ͣ���������ק�ͷ��ٶ�С��һ��ֵ�����ǰ��ձ�Ե�����ָ���
// �����ק�ͷ��ٶȴ���һ��ֵ�����м��ٶ������ڿؼ������ٶȷ���ɳ�ȥ

// �������� Size ���� Touch.ActualSize.ToSize()
// �� double ���� Touch.Width