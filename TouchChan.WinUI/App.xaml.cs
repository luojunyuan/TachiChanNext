using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using WinRT.Interop;

namespace TouchChan.WinUI;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WAS Shit 4: 在 desktop 下 LaunchActivatedEventArgs 接收不到命令行参数 #3368
        var arguments = Environment.GetCommandLineArgs();
        var process = Process.GetProcessById(int.Parse(arguments[1]));

        // NOTE: HiDpi 高分屏不支持非 DPI 感知的窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);
        ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

        _mainWindow = new MainWindow();

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
            WinUIEx.WindowExtensions.Hide(_mainWindow);
            NativeMethods.SetParent(WindowNative.GetWindowHandle(_mainWindow), nint.Zero);
        };
        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            const int WM_NCDESTROY = 0x0082;
            if (e.Message.MessageId == WM_Destroy || e.Message.MessageId == WM_NCDESTROY)
            {
                Debug.WriteLine("WM_Destroy || WM_NCDESTROY");

                // Arm 下调这个必死
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
    //private static bool IsDpiUnaware(int pid)
    //{
    //    // GetProcessDpiAwareness 仅支持 Windows 8.1 及以后的系统，因为在那之前没有进程级别的 DPI 感知，
    //    // 所以默认进程对象是 DPI 感知的。
    //    if (!OperatingSystem.IsWindowsVersionAtLeast(6, 3))
    //        return false;

    //    var handle = Process.GetProcessById(pid).Handle;
    //    using var processHandle = new SafeProcessHandle(handle, true);
    //    var result = PInvoke.GetProcessDpiAwareness(processHandle, out var awareType);

    //    // 100% DPI 下启动，**任何窗口都可以正常工作**
    //    // 100% DPI 下改变显示器 DPI（这里无法正常工作意为无法接收鼠标输入）
    //    // “系统DPI缩放”，子窗口无法正常工作。（B站弹幕姬）
    //    // “未知”，子窗口无法正常工作。（DebugView）

    //    // DPIv1v2：正常适配
    //    // TODO: System-DPI 到底怎么样？

    //    // FUTURE：HiDpi 下，“不可用 awareType == 0，系统会将这类窗口放大，全屏无法正常显示，拿到 dpi 是为 1
    //    // 考虑支持这种情况下的窗口模式，子窗口没有正常设置到 0，0 位置，提示用户如果需要全屏游玩请使用 TouchChan启动 还需测试触控输入是否偏移
    //    return result == 0 && awareType == 0;
    //}

    private Window? _mainWindow;
}
