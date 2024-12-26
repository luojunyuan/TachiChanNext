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

        // NOTE: 这是一个仅依赖于非 DPI感知-系统 的窗口的 WinUI 子窗口
        if (IsSystemDpiAware(process.Id))
            throw new InvalidOperationException();

        _mainWindow = new MainWindow(gameWindowHandle);

        var uiThread = DispatcherQueue.GetForCurrentThread();

        // 总结一下 0xc000027b 错误的表现
        // 1. 必须先要重新设置父窗口为 0
        // 2. 在 1 的基础上，只能在窗口中，或窗口消息循环中调用 Close 或 Current.Exit 方法才能做到正常退出
        // 3. 在 Arm 架构下，基于 1，消息循环中也会出现该错误（暂未尝试在窗口中 Current.Exit）
        // 4. 在 Arm 架构下，基于 1，似乎父窗口进程与同为 Arm64 架构的子窗口进程相同，不会出现该错误 (同为 x86 不影响)
        // 5. 在除窗口和消息循环外其他地方无论有没有步骤 1，通过 uiThread Close() 或 Current.Exit 都会出现该错误
        // NOTE: 暂时可以搁置这个错误调查，因为还需考虑不使用消息循环，或者关闭窗口不退出进程重建窗口的情况，对这部分产生的影响。
        // 比如先隐藏，再创建新窗口，再关闭旧窗口

        // QUES: 取消父窗口前，必须先 hide 一下，否则可能会脱离父窗口出现在屏幕其他地方？
        _mainWindow.Closed += (_, _) => PInvoke.SetParent(WindowNative.GetWindowHandle(_mainWindow).ToHwnd(), nint.Zero.ToHwnd());
        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            const int WM_NCDESTROY = 0x0082;
            if (e.Message.MessageId == WM_Destroy || e.Message.MessageId == WM_NCDESTROY)
            {
                Debug.WriteLine("WM_Destroy || WM_NCDESTROY");

                Current.Exit();

                // HACK: 避免 0xc000027b 错误
                // FEAT: 可以进一步检查父进程的架构
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    Environment.Exit(0);
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
    /// 判断进程对象是否为系统 DPI 感知
    /// </summary>
    private static bool IsSystemDpiAware(int pid)
    {
        // GetProcessDpiAwareness 仅支持 Windows 8.1 及以后的系统，因为在那之前没有进程级别的 DPI 感知，
        // 所以默认认为进程对象 pid 是非系统 DPI 感知。
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 3))
            return false;

        var handle = Process.GetProcessById(pid).Handle;
        using var processHandle = new SafeProcessHandle(handle, true);
        var result = PInvoke.GetProcessDpiAwareness(processHandle, out var awareType);

        return result == 0 && awareType == Windows.Win32.UI.HiDpi.PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE;
    }

    private Window? _mainWindow;
}
