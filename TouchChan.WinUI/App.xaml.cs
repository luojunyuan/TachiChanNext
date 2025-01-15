using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WindowsShortcutFactory;
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
        UnhandledException += (sender, e) =>
        {
            Debug.WriteLine(e.Exception);
            // WAS Shit 8: e.Handled 默认为 false，但是却不会退出程序
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

    private static async Task<LaunchResult> StartMainWindowAsync(string[] arguments)
    {
        var path = arguments[0];

        var gamePathResult = PrepareValidGamePath(path);
        if (gamePathResult.IsFailure(out var pathError, out var gamePath))
        {
            await MessageBox.ShowAsync(pathError.Message);
            return LaunchResult.Failed;
        }

        var process = await GetWindowProcessByPathAsync(gamePath);

        // TODO: Win32 native gdi+ splash
        // 比较流和文件哪个速度更快，理应流更快，因为没有磁盘IO，但是COM的转化有可能比计算机上的文件IO慢
        // LaunchGameWithSplashAsync(gamePath, arguments.Contains("-le"))
        if (process == null)
        {
            const string fileImage = "assets\\klee.png";
            var splash = WinUIEx.SimpleSplashScreen.ShowSplashScreenImage(fileImage);

            var launchResult = await LaunchGameAsync(gamePath, arguments.Contains("-le"));
            if (launchResult.IsFailure(out var launchGameError, out process))
            {
                splash.Hide();
                await MessageBox.ShowAsync(launchGameError.Message);
                return LaunchResult.Failed;
            }

            splash.Hide();
        }

        // QUES: 似乎Process启动游戏后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。而附加窗口没有影响
        var childWindow = new MainWindow(); // Ryzen 7 5800H: 33ms
        childWindow.Activate();

        // var process = await Async(

        // QUES: 如果是 _ = XxxAsync() 任务，能够捕捉到吗，是不是还区分方法内部有没有 await 等待。
        _ = Task.Factory.StartNew(async () =>
        {
            try
            {
                await ManageGameWindowBindingAsync(childWindow, process);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                Environment.Exit(1);
            }
        }, TaskCreationOptions.LongRunning);

        return LaunchResult.Success;
    }

    /// <summary>
    /// 启动游戏进程
    /// </summary>
    private static async Task<Result<Process>> LaunchGameAsync(string path, bool leEnable)
    {
        // NOTE: NUKITASHI2(steam) 会先启动一个进程闪现黑屏窗口，然后再重新启动游戏进程

        // TODO: 通过 LE 启动，思考检查游戏id好的方法，处理超时和错误情况
        // 考虑 LE 通过注册表查找还是通过配置文件，还是通过指定路径来启动
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path),
            EnvironmentVariables = { ["__COMPAT_LAYER"] = "HighDpiAware" }
        };
        _ = Process.Start(startInfo);

        const int WaitGameStartTimeout = 20000;
        const int UIMinimumResponseTime = 50;

        var gameProcess = await GetWindowProcessByPathAsync(path);

        if (gameProcess == null)
            return Result.Failure<Process>("error not implement");

        // leProc.kill()
        return gameProcess;
    }

    /// <summary>
    /// 准备有效的游戏路径
    /// </summary>
    private static Result<string> PrepareValidGamePath(string path)
    {
        if (!File.Exists(path))
            return Result.Failure<string>($"Game path \"{path}\" not found, please check if it exist.");

        var isNotLnkFile = !Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

        if (isNotLnkFile)
            return path;

        string? resolvedPath;
        try
        {
            resolvedPath = WindowsShortcut.Load(path).Path;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return Result.Failure<string>($"Failed when resolve \"{path}\", please try start from game folder.");
        }

        if (!File.Exists(resolvedPath))
            return Result.Failure<string>($"Resolved link path \"{resolvedPath}\" not found, please try start from game folder.");

        return resolvedPath;
    }

    /// <summary>
    /// 尝试通过限定的程序路径获取对应正在运行的，存在 MainWindowHandle 的进程
    /// </summary>
    private static Task<Process?> GetWindowProcessByPathAsync(string gamePath)
    {
        var friendlyName = Path.GetFileNameWithoutExtension(gamePath);
        return Task.Run(() => Process.GetProcessesByName(friendlyName)
            .Where(p => p.MainModule != null &&
                p.MainModule.FileName.Equals(gamePath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.StartTime)
            .FirstOrDefault(p => p.MainWindowHandle != nint.Zero));
    }

    /// <summary>
    /// 绑定窗口到游戏进程，跟随整个进程生命周期
    /// </summary>
    /// <param name="childWindow">WinUI 3 子窗口</param>
    /// <param name="process">目标进程</param>
    private static async Task ManageGameWindowBindingAsync(MainWindow childWindow, Process process)
    {
        // 设计一个游戏的CurrentMainWindowHandleService, in process
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的窗口
        var isDpiUnaware = Win32.IsDpiUnaware(process);
        // use reactive, avoid async Task?
        while (process.HasExited is false)
        {
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

        Environment.Exit(0);
    }

    /// <summary>
    /// 设置生命周期，启动偏好设置面板。
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
                // QUES: 即使回到 UI 线程中 preference.Activate() 似乎不启用
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
