using System.Diagnostics;
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TouchChan;
using Avalonia.Threading;

namespace AvaFramelessChildWindow;

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

            var process = Process.GetProcessById(int.Parse(args[0]));
            ServiceLocator.InitializeWindowHandle(process.MainWindowHandle);
            // NOTE: HiDpi 高分屏不支持非 DPI 感知的窗口
            if (ServiceLocator.GameWindowService.DpiScale != 1 && Win32.IsDpiUnaware(process.Id))
                throw new InvalidOperationException();

            desktop.MainWindow = new MainWindow
            {
            };
            
            desktop.MainWindow.Closed += (s, e) => Debug.WriteLine("Closed");
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => Dispatcher.UIThread.Invoke(() => desktop.Shutdown());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
