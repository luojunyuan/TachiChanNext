using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using WinRT.Interop;
using WinUIEx;

namespace TouchChan;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WAS Shit 4: #3368
        var arguments = Environment.GetCommandLineArgs();
        var gameWindowHandle = nint.Zero;
        Process process;
        if (arguments.Length == 2)
        {
            try
            {
                process = Process.GetProcessById(int.Parse(arguments[1]));
                gameWindowHandle = process.MainWindowHandle;
            }
            catch
            {
                Debug.WriteLine("异常：设置了 pid 没有找到进程，将去找 notepad");
                var proc = Process.GetProcessesByName("notepad").FirstOrDefault()
                    ?? throw new InvalidOperationException();
                gameWindowHandle = proc.MainWindowHandle;
                process = proc;
            }
        }
        else
        {
            var proc = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (proc == null)
            {
                proc = Process.Start(@"C:\Users\kimika\Desktop\9-nine-天色天歌天籁音(樱空)\9-nine-天色天歌天籁音.exe");
                proc.WaitForInputIdle();
                System.Threading.Thread.Sleep(3000);
            }
            gameWindowHandle = proc.MainWindowHandle;
            process = proc;
        }

        // NOTE: ?这是一个仅依赖于非 DPI感知-系统 的窗口的 WinUI 子窗口
        // TODO: 还需要判断游戏所在显示器 dpi 是否 100%？如果游戏不支持，但是显示器默认是 100%，那么 touch 理应正常工作
        if (IsDpiUnaware(process.Id))
            throw new InvalidOperationException();

        _mainWindow = new MainWindow(gameWindowHandle);

        var uiThread = DispatcherQueue.GetForCurrentThread();

        // 总结一下 0xc000027b 错误的表现
        // 1. 必须先要重新设置父窗口为 0
        // 2. 在 1 的基础上，只能在窗口中，或窗口消息循环中调用 Close 或 Current.Exit 方法才能做到正常退出
        // 3. 在 Arm 架构下，基于 1，消息循环中也会出现该错误（暂未尝试在窗口中 Current.Exit）
        // 4. 在 Arm 架构下，基于 1，似乎父窗口进程与同为 Arm64 架构的子窗口进程相同，不会出现该错误 (同为 x86 不影响)
        // 5. 在除窗口和消息循环外其他地方无论有没有步骤 1，通过 uiThread Close() 或 Current.Exit 都会出现该错误
        // 6. 在 Arm 架构下，编译为 x64，错误发生在 Current.Exit() 之中，甚至后续代码没法执行，直接错误退出
        // 7. 在 Arm 架构下，编译为 x64，消息循环中 Current.Exit() 无法退出
        // 8. 在 Arm 架构下，编译为 x64，父窗口是 arm64，消息循环中 Current.Exit() 正常退出
        // 9. 在 Arm 架构下，编译为 x64，父窗口是 x64，消息循环中 Current.Exit() 正常退出
        // NOTE: 暂时可以搁置这个错误调查，因为还需考虑不使用消息循环，或者关闭窗口不退出进程重建窗口的情况，对这部分产生的影响。
        // 比如先隐藏，再创建新窗口，再关闭旧窗口

        // FIXME: 取消父窗口后可能会脱离父窗口出现在屏幕其他地方，我确实观察到了
        // プログラム '[8180] TouchChanNext.exe' はコード 3221225480(0xc0000008) 'An invalid handle was specified' で終了しました。
        _mainWindow.Closed += (_, _) =>
        {
            _mainWindow.Hide();
            PInvoke.SetParent(WindowNative.GetWindowHandle(_mainWindow).ToHwnd(), nint.Zero.ToHwnd());
        };
        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            const int WM_NCDESTROY = 0x0082;
            if (e.Message.MessageId == WM_Destroy || e.Message.MessageId == WM_NCDESTROY)
            {
                Debug.WriteLine("WM_Destroy || WM_NCDESTROY");

                Current.Exit();
            }

            const uint WM_DPICHANGED = 736u;
            if (e.Message.MessageId == WM_DPICHANGED)
            {
                Debug.WriteLine("WM_DPICHANGED");
            }
        };

        _mainWindow.Activate();
    }

    /// <summary>
    /// 判断进程对象是否对 DPI 不感知
    /// </summary>
    private static bool IsDpiUnaware(int pid)
    {
        // GetProcessDpiAwareness 仅支持 Windows 8.1 及以后的系统，因为在那之前没有进程级别的 DPI 感知，
        // 所以默认进程对象是 DPI 感知的。
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 3))
            return false;

        var handle = Process.GetProcessById(pid).Handle;
        using var processHandle = new SafeProcessHandle(handle, true);
        var result = PInvoke.GetProcessDpiAwareness(processHandle, out var awareType);

        // TODO: 刚刚确定DebugView 非对应DPI 子窗口没法用，那么System DPI到底怎么样？
        return result == 0 && awareType == 0;
    }

    private Window? _mainWindow;
}
