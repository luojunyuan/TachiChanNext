using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Nito.AsyncEx;
using R3;
using System;
using System.Diagnostics;
using System.Threading.Channels;

namespace TouchChan.Ava;

public partial class App : Application
{
    public static Subject<Unit> OnTouchShowed { get; private set; } = new();

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var arguments = desktop.Args ?? [];

            var path = arguments[0];

            var gamePathResult = GameStartup.PrepareValidGamePath(path);
            if (gamePathResult.IsFailure(out var pathError, out var gamePath))
            {
                await MessageBox.ShowAsync(pathError.Message);
                return;
            }

            var processTask = Task.Run(async () =>
                await GameStartup.GetOrLaunchGameWithSplashAsync(gamePath, arguments.Contains("-le")));

            var childWindow = new MainWindow
            {
            };
            desktop.MainWindow = childWindow;

            var processResult = await processTask;
            if (processResult.IsFailure(out var processError, out var process))
            {
                await MessageBox.ShowAsync(processError.Message);
                return;
            }

            // 启动后台绑定窗口任务
            _ = Task.Factory.StartNew(async () =>
            {
                try
                {
                    await GameWindowBindingAsync(childWindow, process);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    Environment.Exit(1);
                }
            }, TaskCreationOptions.LongRunning);


            // 程序“[32900] TouchChan.Ava.exe”已退出，返回值为 3221226525 (0xc000041d)。
            // 没有输出
            // 第二屏幕 125% 窗口上，关闭未知 dpi debugview
            //Closed
            //程序“[9576] TouchChan.Ava.exe”已退出，返回值为 3221225480(0xc0000008) 'An invalid handle was specified'。
            //desktop.MainWindow.Closed += (s, e) => Debug.WriteLine("Closed");
            //process.EnableRaisingEvents = true;
            //process.Exited += (_, _) => Dispatcher.UIThread.Invoke(() => desktop.Shutdown());
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 绑定窗口到游戏进程，也是启动后的 WinUI 窗口生命周期
    /// </summary>
    /// <param name="childWindow">WinUI 子窗口</param>
    /// <param name="process">目标进程</param>
    private static async Task GameWindowBindingAsync(MainWindow childWindow, Process process)
    {
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的游戏窗口
        var isDpiUnaware = Win32.IsDpiUnaware(process);

        var childWindowClosedChannel = Channel.CreateUnbounded<Unit>();
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => childWindowClosedChannel.Writer.Complete();

        while (process.HasExited is false)
        {
            var handleResult = await GameStartup.FindGoodWindowHandleAsync(process);
            if (handleResult.IsFailure(out var error, out var windowHandle)
                && error is WindowHandleNotFoundError)
            {
                await MessageBox.ShowAsync("Timeout! Failed to find a valid window of game");
                break;
            }
            else if (error is ProcessExitedError or ProcessPendingExitedError)
            {
                break;
            }

            using CompositeDisposable disposables = [];

            if (isDpiUnaware)
            {
                // HACK: Pending test
                //childWindow.UnawareGameWindowShowHideHack(windowHandle)
                //    .DisposeWith(disposables);
            }

            await NativeMethods.SetParentAsync(childWindow.Hwnd, windowHandle);

            GameWindowService.ClientSizeChanged(windowHandle)
                .Subscribe(size => childWindow.Hwnd.ResizeClient(size));

            GameWindowService.WindowDestroyed(windowHandle)
                .Subscribe(x => childWindowClosedChannel.Writer.TryWrite(x));

            OnTouchShowed.OnNext(Unit.Default);
            await childWindowClosedChannel.Reader.ReadAsync();

            process.Refresh();
        }

        // WAS Shit x: 疑似窗口显示后，在窗口显示前的线程上调用 Current.Exit() 会引发错误
        Environment.Exit(0);
    }
}
