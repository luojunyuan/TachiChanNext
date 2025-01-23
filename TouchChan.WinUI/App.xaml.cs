using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using WinRT.Interop;

namespace TouchChan.WinUI;

public partial class App : Application
{
    public static readonly SynchronizationContext UISyncContext;

    static App() => UISyncContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());

    public App()
    {

        Log.Do("App Start");
        // Benchmark: 预加载 (Warm Up AOT) 可能是因为需要读入程序集，考验IO能力
        _ = int.TryParse(string.Empty, out _);
        Log.Do("Warm Up AOT");

        this.InitializeComponent();
        Log.Do("InitializeComponent");

#if !DEBUG
        // WAS Shit 8: 异常发生时默认不会结束程序
        UnhandledException += (sender, e) =>
        {
            Debug.WriteLine(e.Exception);
            Environment.Exit(1);
        };
#endif
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log.Do("App OnLaunched Start");

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
        Log.Do("StartMainWindowAsync Start");
        var path = arguments[0];

        var gamePathResult = GameStartup.PrepareValidGamePath(path);
        if (gamePathResult.IsFailure(out var pathError, out var gamePath))
        {
            await MessageBox.ShowAsync(pathError.Message);
            return LaunchResult.Failed;
        }
        Log.Do("PrepareValidGamePath");

        var processTask = Task.Run(async () =>
            await GameStartup.GetOrLaunchGameWithSplashAsync(gamePath, arguments.Contains("-le")));

        Log.Do("MainWindow");
        var childWindow = new MainWindow();
        childWindow.Activate();
        Log.Do("MainWindow Activated");

        var processResult = await processTask;
        Log.Do("processResult got", true);
        if (processResult.IsFailure(out var processError, out var process))
        {
            await MessageBox.ShowAsync(processError.Message);
            return LaunchResult.Failed;
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

        Log.Do("StartMainWindowAsync End");

        return LaunchResult.Success;
    }

    /// <summary>
    /// 绑定窗口到游戏进程，也是启动后的 WinUI 窗口生命周期
    /// </summary>
    /// <param name="childWindow">WinUI 子窗口</param>
    /// <param name="process">目标进程</param>
    private static async Task GameWindowBindingAsync(MainWindow childWindow, Process process)
    {
        Log.Do2("GameWindowBindingAsync Start", false, true);
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的游戏窗口
        var isDpiUnaware = Win32.IsDpiUnaware(process);

        var childWindowClosedChannel = Channel.CreateUnbounded<Unit>();
        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(_ => childWindowClosedChannel.Writer.Complete());

        while (process.HasExited is false)
        {
            Log.Do2("Start FindRealWindowHandleAsync");
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
            Log.Do2("FindGoodWindowHandleAsync");

            using CompositeDisposable disposables = [];

            if (isDpiUnaware)
            {
                // HACK: Pending test
                childWindow.UnawareGameWindowShowHideHack(windowHandle)
                    .DisposeWith(disposables);
            }

            NativeMethods.SetParent(childWindow.Hwnd, windowHandle);

            GameWindowService.ClientSizeChanged(windowHandle)
                .SubscribeOn(UISyncContext)
                .Subscribe(size => childWindow.Hwnd.ResizeClient(size))
                .DisposeWith(disposables);

            GameWindowService.WindowDestroyed(windowHandle)
                .SubscribeOn(UISyncContext)
                .Subscribe(x => childWindowClosedChannel.Writer.TryWrite(x))
                .DisposeWith(disposables);

            Log.Do2("Subscribe");
            await childWindowClosedChannel.Reader.ReadAsync();
            Log.Do2("Window Destoyred", true);

            process.Refresh();
        }

        // WAS Shit x: 疑似窗口显示后，在窗口显示前的线程上调用 Current.Exit() 会引发错误
        Environment.Exit(0);
    }

    /// <summary>
    /// 设置单实例应用，启动偏好设置面板。
    /// </summary>
    private static async Task<LaunchResult> LaunchPreferenceAsync()
    {
        Log.Do("App-LaunchPreferenceAsync Start");

        const string InstanceID = "13615F35-469B-4341-B3CE-121C694C042C";
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceID);
        if (!mainInstance.IsCurrent)
        {
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(appArgs);
            Log.Do("RedirectActivationToAsync");
            return LaunchResult.Redirected;
        }

        var preference = new PreferenceWindow();
        preference.Activate();
        Log.Do("Activated PreferenceWindow");

        AppInstance.GetCurrent().RxActivated()
            .Subscribe(_ =>
            {
                // WAS Shit 9: preference.Active() 在这里不起用
                Log.Do2("RedirectActivationToAsync", false, true);
                var preferenceHandle = WindowNative.GetWindowHandle(preference);
                Win32.ActiveWindow(preferenceHandle);
                Log.Do2("ActiveWindow");
            });

        return LaunchResult.Success;
    }

    enum LaunchResult
    {
        Success,
        Redirected,
        Failed,
    }
}
