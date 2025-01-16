using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WinRT.Interop;

namespace TouchChan.WinUI;

public partial class App : Application
{
    public static readonly SynchronizationContext UISyncContext;

    static App() => UISyncContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());

    public App()
    {
        // Benchmark: 预加载 (Warm Up AOT)
        _ = int.TryParse(string.Empty, out _);

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

        var processTask = GetOrLaunchGameWithSplashAsync(gamePath, arguments.Contains("-le"));

        var childWindow = new MainWindow(); // Ryzen 7 5800H: 33ms
        childWindow.Activate();

        var processResult = await processTask;
        if (processResult.IsFailure(out var processError, out var process))
        {
            await MessageBox.ShowAsync(processError.Message);
            return LaunchResult.Failed;
        }

        // 确保 process 后，立即订阅退出事件，绑定生命周期到游戏进程
        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(_ => Environment.Exit(0));

        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                await GameWindowBindingAsync(childWindow, process);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                Environment.Exit(1);
            }
        }, TaskCreationOptions.LongRunning);

        return LaunchResult.Success;
    }

    private static async Task<Result<Process>> GetOrLaunchGameWithSplashAsync(string path, bool leEnable)
    {
        var process = await GameStartup.GetWindowProcessByPathAsync(path);
        if (process != null)
            return process;

        // TODO: Win32 native gdi+ splash
        // 比较流和文件哪个速度更快，理应流更快，因为没有磁盘IO，但是COM的转化有可能比计算机上的文件IO慢
        const string fileImage = "assets\\klee.png";
        var splash = WinUIEx.SimpleSplashScreen.ShowSplashScreenImage(fileImage);

        var launchResult = await GameStartup.LaunchGameAsync(path, leEnable);
        try
        {
            if (launchResult.IsFailure(out var launchGameError, out process))
                return Result.Failure<Process>(launchGameError.Message);

            return process;
        }
        finally
        {
            splash.Hide();
        }
    }

    /// <summary>
    /// 绑定窗口到游戏进程
    /// </summary>
    /// <param name="childWindow">WinUI 3 子窗口</param>
    /// <param name="process">目标进程</param>
    private static async Task GameWindowBindingAsync(MainWindow childWindow, Process process)
    {
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的游戏窗口
        var isDpiUnaware = Win32.IsDpiUnaware(process);

        // QUES: use reactive, avoid async Task?

        // TODO：这个循环只做窗口检查，不做进程检查
        while (process.HasExited is false)
        {
            // 设计一个游戏的CurrentMainWindowHandleService, in process
            // TODO: 在查找窗口的过程中，也会检查进程是否已经退出吗？不会
            // 如果找不到有效窗口，就弹窗提示信息

            Debug.WriteLine("im coming in");
            CompositeDisposable disposables = [];

            ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

            var childWindowClosedChannel = Channel.CreateUnbounded<Unit>();
            ServiceLocator.GameWindowService.WindowDestroyed()
                .SubscribeOn(UISyncContext)
                .Subscribe(x => childWindowClosedChannel.Writer.TryWrite(x))
                .DisposeWith(disposables);

            if (isDpiUnaware)
            {
                childWindow.UnawareGameWindowShowHideHack(ServiceLocator.GameWindowService)
                    .DisposeWith(disposables);
            }

            NativeMethods.SetParent(childWindow.Hwnd, ServiceLocator.GameWindowService.WindowHandle);

            ServiceLocator.GameWindowService.ClientSizeChanged()
                .SubscribeOn(UISyncContext)
                .Subscribe(size => childWindow.Hwnd.ResizeClient(size))
                .DisposeWith(disposables);

            await childWindowClosedChannel.Reader.WaitToReadAsync();

            disposables.Dispose();
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

        AppInstance.GetCurrent().RxActivated()
            .Subscribe(_ =>
            {
                // WAS Shit 9: preference.Active() 在这里不起用
                var preferenceHandle = WindowNative.GetWindowHandle(preference);
                Win32.ActiveWindow(preferenceHandle);
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
