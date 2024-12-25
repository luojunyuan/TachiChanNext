using Microsoft.UI.Xaml;
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;

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
                throw new ArgumentException("no process find");
            }
        }
        else
        {
            var proc = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (proc == null)
            {
                proc = Process.Start(@"C:\Users\kimika\Desktop\9-nine-天色天歌天籁音(樱空)\9-nine-天色天歌天籁音.exe");
                proc.WaitForInputIdle();
            }
            gameWindowHandle = proc.MainWindowHandle;
            process = proc;
        }

        // NOTE: 这是一个仅依赖于非 DPI感知-系统 的窗口的 WinUI 子窗口
        if (IsSystemDpiAware(process.Id))
            throw new InvalidOperationException();

        _mainWindow = new MainWindow(gameWindowHandle);

        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            const int WM_NCDESTROY = 130;
            if (e.Message.MessageId == WM_Destroy || e.Message.MessageId == WM_NCDESTROY)
            {
                Debug.WriteLine("WM_Destroy || WM_NCDESTROY");
                // QUES: 不同计算机上表现不同？有时候无效
                // 两种方法都可以正常退出，SetParent是必须的
                _mainWindow.Close();
                //Current.Exit();
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
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 3))
            return false;

        // GetProcessDpiAwareness 仅支持 Windows 8.1 及以后的系统，因为在那之前没有进程级别的 DPI 感知
        var handle = Process.GetProcessById(pid).Handle;
        using var processHandle = new SafeProcessHandle(handle, true);
        var result = PInvoke.GetProcessDpiAwareness(processHandle, out var awareType);

        return result == 0 && awareType == Windows.Win32.UI.HiDpi.PROCESS_DPI_AWARENESS.PROCESS_SYSTEM_DPI_AWARE;
    }

    private Window? _mainWindow;
}
