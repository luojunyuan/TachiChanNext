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
        //var process = Process.GetProcessById(int.Parse(arguments[1]));

        var process = Process.Start(@"C:\Users\CYCY\Desktop\software\DebugView\Dbgview.exe");
        process.WaitForInputIdle();

        // NOTE: HiDpi 高分屏不支持非 DPI 感知的窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);
        ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Environment.Exit(0);
    }

    private Window? _mainWindow;
}
