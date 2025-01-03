using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;

namespace TouchChan.Ava;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? [];

            //startInfo.EnvironmentVariables["__COMPAT_LAYER"] = "HighDpiAware";

            var process = Process.GetProcessById(int.Parse(args[0]));
            var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);
            ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

            desktop.MainWindow = new MainWindow
            {
            };

            // 程序“[32900] TouchChan.Ava.exe”已退出，返回值为 3221226525 (0xc000041d)。
            // 没有输出
            // 第二屏幕 125% 窗口上，关闭未知 dpi debugview
            //Closed
            //程序“[9576] TouchChan.Ava.exe”已退出，返回值为 3221225480(0xc0000008) 'An invalid handle was specified'。
            desktop.MainWindow.Closed += (s, e) => Debug.WriteLine("Closed");
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.UIThread.Invoke(() => desktop.Shutdown());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
