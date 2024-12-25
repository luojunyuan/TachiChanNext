using Microsoft.UI.Dispatching;
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

        var uiThread = DispatcherQueue.GetForCurrentThread();
        
        process.EnableRaisingEvents = true;
        process.Exited += (s, e) =>
        {
            // HACK: 避免 0xc000027b 错误
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                Environment.Exit(0);
        };

        // NOTE: 这是一个仅依赖于非 DPI感知-系统 的窗口的 WinUI 子窗口
        if (IsSystemDpiAware(process.Id))
            throw new InvalidOperationException();

        _mainWindow = new MainWindow(gameWindowHandle);

        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
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
