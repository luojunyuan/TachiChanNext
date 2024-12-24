using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Linq;
using WinUIEx;
using WinUIEx.Messaging;

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
        if (arguments.Length == 2)
        {
            try
            {
                gameWindowHandle = Process.GetProcessById(int.Parse(arguments[1])).MainWindowHandle;
            }
            catch
            {
                gameWindowHandle = nint.Zero;
            }
        }
        else
        {
            var process = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (process == null)
            {
                process = Process.Start(@"C:\Users\kimika\Desktop\9-nine-天色天歌天籁音(樱空)\9-nine-天色天歌天籁音.exe");
                process.WaitForInputIdle();
            }
            gameWindowHandle = process?.MainWindowHandle ?? nint.Zero;
        }
        // TODO: gameWindowHandle 只考虑非系统缩放 dpi 的情况
        _mainWindow = new MainWindow(gameWindowHandle);

        var monitor = new WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            const int WM_NCDESTROY = 130;
            if (e.Message.MessageId == WM_Destroy || e.Message.MessageId == WM_NCDESTROY)
            {
                Debug.WriteLine("WM_Destroy || WM_NCDESTROY");
                // 退出前重置父窗口设置0，避免 0xc000027b 错误
                // TODO：仍旧错误
                Windows.Win32.PInvoke.SetParent(_mainWindow.GetWindowHandle().ToHwnd(), nint.Zero.ToHwnd());
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

    private Window? _mainWindow;
}
