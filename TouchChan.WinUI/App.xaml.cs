using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using R3.ObservableEvents;
using TouchChan.Interop;
using WinRT.Interop;

namespace TouchChan.WinUI;

public partial class App : Application
{
    public static readonly SynchronizationContext UISyncContext;

    static App() => UISyncContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());

    public App()
    {
        this.InitializeComponent();

#if !DEBUG
        // WAS Shit 8: 异常发生时默认不会结束程序
        UnhandledException += (sender, e) =>
        {
            Trace.WriteLine(e.Exception);
            Environment.Exit(1);
        };
#endif
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WAS Shit 4: 在 desktop 下 LaunchActivatedEventArgs 接收不到命令行参数 #3368
        var arguments = Environment.GetCommandLineArgs()[1..];

        var isDefaultLaunch = arguments.Length == 0;

        var result = isDefaultLaunch switch
        {
            true => await LaunchPreferenceAsync(),
            false => await StartMainWindowAsync(arguments),
        };

        if (result is LaunchResult.Redirected or LaunchResult.Failed)
            Current.Exit();
    }

    /// <summary>
    /// 启动小圆点主窗口
    /// </summary>
    private static async Task<LaunchResult> StartMainWindowAsync(string[] arguments)
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

        var childWindow = new MainWindow();
        childWindow.Activate();

        childWindow.Loaded.Subscribe(async _ =>
        {
            // 在窗口完成准备后启动后台绑定窗口任务
            await Task.Factory.StartNew(async () =>
                await GameWindowBindingAsync(childWindow, process),
                TaskCreationOptions.LongRunning).Unwrap();

            Current.Exit();
        });

        return LaunchResult.Success;
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

        // WAS Shit 11: 关闭子窗口不能正常退出程序
        const uint WM_DESTROY = 0x0002;
        var monitor = new WinUIEx.Messaging.WindowMessageMonitor(childWindow);
        monitor.Events().WindowMessageReceived
            .SubscribeOn(UISyncContext)
            .Where(e => e.Message.MessageId == WM_DESTROY &&
                !childWindowClosedChannel.Reader.Completion.IsCompleted)
            .Subscribe(async _ =>
            {
                Win32.HideWindow(childWindow.Hwnd);
                await Win32.SetParentWindowAsync(childWindow.Hwnd, nint.Zero);
            });

        process.Events().Exited.Subscribe(_ => childWindowClosedChannel.Writer.Complete());

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

            // HACK: 传递 Alt+Enter 按键事件到游戏窗口
            childWindow.UpdateAltEnterHandle(windowHandle);

            // HACK: Pending test 并且一定要显式提示用户
            if (isDpiUnaware)
            {
                childWindow.UnawareGameWindowShowHideHack(windowHandle)
                    .DisposeWith(disposables);
            }

            await Win32.SetParentWindowAsync(childWindow.Hwnd, windowHandle);

            GameWindowService.ClientSizeChanged(windowHandle)
                .SubscribeOn(UISyncContext)
                .Subscribe(size => Win32.ResizeWindow(childWindow.Hwnd, size))
                .DisposeWith(disposables);

            GameWindowService.WindowDestroyed(windowHandle)
                .SubscribeOn(UISyncContext)
                .Subscribe(x => childWindowClosedChannel.Writer.TryWrite(x))
                .DisposeWith(disposables);

            MainWindow.OnTouchShowed.OnNext(Unit.Default);
            await childWindowClosedChannel.Reader.ReadAsync();

            process.Refresh();
        }
    }

    /// <summary>
    /// 设置单实例应用，启动偏好设置面板。
    /// </summary>
    private static async Task<LaunchResult> LaunchPreferenceAsync()
    {
        const string InstanceID = "13615F35-469B-4341-B3CE-121C694C042C";
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceID);
        if (!mainInstance.IsCurrent)
        {
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(appArgs);
            return LaunchResult.Redirected;
        }

        var preference = new PreferenceWindow();
        preference.Activate();

        AppInstance.GetCurrent().Events().Activated
            .Subscribe(_ =>
            {
                // WAS Shit 9: preference.Active() 在这里不起用 #7595
                var preferenceHandle = WindowNative.GetWindowHandle(preference);
                Win32.ActiveWindow(preferenceHandle);
            });

        return LaunchResult.Success;
    }
}
