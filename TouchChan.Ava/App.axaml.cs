using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using R3;
using System.Diagnostics;
using System.Threading.Channels;

namespace TouchChan.Ava;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { Args: var arguments } desktop || arguments is null)
            return;

        var isDefaultLaunch = arguments.Length == 0;

        var result = isDefaultLaunch switch
        {
            //true => await LaunchPreferenceAsync(),
            _ => await StartMainWindowSimulateAsync(desktop, arguments),
        };

        if (result is LaunchResult.Redirected or LaunchResult.Failed)
            Environment.Exit(0);
    }

    private static async Task<LaunchResult> StartMainWindowSimulateAsync(IClassicDesktopStyleApplicationLifetime desktop, string[] arguments)
    {
        var path = arguments[0];

        var gamePathResult = GameStartup.PrepareValidGamePath(path);
        if (gamePathResult.IsFailure(out var pathError, out var gamePath))
        {
            await MessageBox.ShowAsync(pathError.Message);
            return LaunchResult.Failed;
        }

        var processResult = await GameStartup.GetOrLaunchGameWithSplashAsync(gamePath, arguments.Contains("-le"));
        if (processResult.IsFailure(out var processError, out var process))
        {
            await MessageBox.ShowAsync(processError.Message);
            return LaunchResult.Failed;
        }

        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                await GameWindowBindingAsync(desktop, process);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Environment.Exit(1);
            }
        }, TaskCreationOptions.LongRunning);

        return LaunchResult.Success;
    }

    private static async Task GameWindowBindingAsync(IClassicDesktopStyleApplicationLifetime desktop, Process process)
    {
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(_ => Dispatcher.UIThread.Invoke(() => desktop.Shutdown()));

        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的游戏窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) &&Win32.IsDpiUnaware(process);

        var childWindowClosedChannel = Channel.CreateUnbounded<Unit>();

        while (process.HasExited is false)
        {
            var handleResult = await GameStartup.FindGoodWindowHandleAsync(process);
            if (handleResult.IsFailure(out var error, out var gameWindowHandle)
                && error is WindowHandleNotFoundError)
            {
                await MessageBox.ShowAsync("Timeout! Failed to find a valid window of game");
                break;
            }
            else if (error is ProcessExitedError or ProcessPendingExitedError)
            {
                break;
            }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var childWindow = new MainWindow();
                childWindow.RxClosed().Subscribe(_ => childWindowClosedChannel.Writer.TryWrite(Unit.Default));
                childWindow.Show();

                desktop.MainWindow = childWindow;

                await NativeMethods.SetParentAsync(childWindow.Hwnd, gameWindowHandle);

                GameWindowService.ClientSizeChanged(gameWindowHandle)
                    .Subscribe(size => childWindow.Hwnd.ResizeClient(size))
                    .DisposeWith(childWindow);
                childWindow.TouchShowed.OnNext(Unit.Default);
            });

            await childWindowClosedChannel.Reader.ReadAsync();

            process.Refresh();
        }
    }

    enum LaunchResult
    {
        Success,
        Redirected,
        Failed,
    }
}

static partial class ObservableEventsExtensions
{
    public static void DisposeWith(this IDisposable disposable, CompositeDisposable compositeDisposable) =>
        compositeDisposable.Add(disposable);

    public static void DisposeWith(this IDisposable disposable, MainWindow mainWindow) =>
        mainWindow.RxClosed()
            .Do(_ => disposable.Dispose())
            .Subscribe();

    public static Observable<EventArgs> RxExited(this Process data) =>
        Observable.FromEvent<EventHandler, EventArgs>(
            h => (sender, e) => h(e),
            e => data.Exited += e,
            e => data.Exited -= e);

    public static Observable<EventArgs> RxClosed(this MainWindow data) =>
        Observable.FromEvent<EventHandler, EventArgs>(
            h => (sender, e) => h(e),
            e => data.Closed += e,
            e => data.Closed -= e);
}
