using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.Linq;
using WinUIEx;
using WinUIEx.Messaging;

namespace TouchChan;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WAS Shit 4: #3368
        var arguments = Environment.GetCommandLineArgs();
        var gameWindowHandle = nint.Zero;
        if (arguments.Length == 2)
        {
            gameWindowHandle = Process.GetProcessById(int.Parse(arguments[1])).MainWindowHandle;
        }
        else
        {
            var process = Process.GetProcessesByName("notepad").FirstOrDefault();
            if (process == null)
            {
                process = Process.Start("notepad");
                process.WaitForInputIdle();
            }
            gameWindowHandle = process?.MainWindowHandle ?? nint.Zero;
        }
        _mainWindow = new MainWindow(gameWindowHandle);

        var monitor = new WindowMessageMonitor(_mainWindow);
        monitor.WindowMessageReceived += (s, e) =>
        {
            const int WM_Destroy = 0x0002;
            if (e.Message.MessageId == WM_Destroy)
            {
                Debug.WriteLine("WM_Destroy");
                // 退出前重置父窗口设置0，避免 0xc000027b 错误
                Windows.Win32.PInvoke.SetParent(_mainWindow.GetWindowHandle().ToHwnd(), nint.Zero.ToHwnd());
                Current.Exit();
            }
        };

        _mainWindow.Activate();
    }

    private Window? _mainWindow;
}
