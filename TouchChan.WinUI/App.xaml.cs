using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        // Benchmark: 预加载 (AOT)
        _ = int.TryParse(string.Empty, out _);

        this.InitializeComponent();

#if !DEBUG
        UnhandledException += (sender, e) =>
        {
            Debug.WriteLine(e.Exception);
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

    private static void WhenGameProcessExit(EventArgs args) => Environment.Exit(0);

    private static async Task<LaunchResult> StartMainWindowAsync(string[] arguments)
    {
        var pathOrPid = arguments[0];

        var processResult = await PrepareValidProcessAsync(pathOrPid);
        if (processResult.IsFailure(out var error, out var process))
        {
            await MessageBox.ShowAsync(error.Message);
            return LaunchResult.Failed;
        }

        // Refactor: 只在启动进程时才打 Splash，收集进程找不到时
        // ... 还需要返回有效的路径

        Log.Do(1);
        var fileImage = "assets\\klee.png";
        var splash = WinUIEx.SimpleSplashScreen.ShowSplashScreenImage(fileImage);
        Log.Do(2);
        var image = typeof(Program).Assembly.GetManifestResourceStream("TouchChan.WinUI.Assets.klee.png");
        Log.Do(3);
        var splash2 = new SplashScreenGdiPlus.SplashScreen().Show(image);
        Log.Do(4);
        // TODO: Instaed of ... await GameMainWindowHandleAsync(process);
        process.WaitForInputIdle();
        splash.Hide();

        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(WhenGameProcessExit);

        // QUES: 似乎Process启动游戏后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。而附加窗口没有影响
        var childWindow = new MainWindow(); // Ryzen 7 5800H: 33ms
        childWindow.Activate();

        _ = Task.Factory.StartNew(() => BindWindowToGame(childWindow, process), TaskCreationOptions.LongRunning);

        return LaunchResult.Success;
    }

    private static Result<Process> Xxx(string arg)
    {
        if (int.TryParse(arg, out var processId))
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch (Exception ex)
            {
                // NOTE: 对于高级用户直接返回有效的默认英文错误信息
                return Result.Failure<Process>(ex.Message);
            }
        }
        return Result.Failure<Process>();
    }

    private static async Task<Result<Process>> PrepareValidProcessAsync(string arg)
    {
        if (int.TryParse(arg, out var processId))
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch (Exception ex)
            {
                // NOTE: 对于高级用户直接返回有效的默认英文错误信息
                return Result.Failure<Process>(ex.Message);
            }
        }

        var gamePathResult = PrepareValidGamePath(arg);
        if (gamePathResult.IsFailure(out var error, out var gamePath))
            return Result.Failure<Process>(error.Message);

        var firstProcess = await GetProcessByPathAsync(gamePath);
        if (firstProcess != null)
            return firstProcess;

        // TODO: 通过 LE 启动，思考检查游戏id好的方法，处理超时和错误情况
        var startInfo = new ProcessStartInfo
        {
            FileName = gamePath,
            WorkingDirectory = Path.GetDirectoryName(gamePath),
            EnvironmentVariables = { ["__COMPAT_LAYER"] = "HighDpiAware" }
        };
        return Process.Start(startInfo) ?? Result.Failure<Process>("error when start game.");
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
    /// 尝试通过路径获取进程
    /// </summary>
    private static Task<Process?> GetProcessByPathAsync(string gamePath)
    {
        var friendlyName = Path.GetFileNameWithoutExtension(gamePath);
        return Task.Run(() =>
            Process.GetProcessesByName(friendlyName)
                .OrderBy(p => p.StartTime)
                .FirstOrDefault());
    }

    /// <summary>
    /// 绑定窗口到游戏进程
    /// </summary>
    /// <param name="childWindow">WinUI 3 子窗口</param>
    /// <param name="process">目标进程</param>
    private static async Task BindWindowToGame(MainWindow childWindow, Process process)
    {
        // 设计一个游戏的CurrentMainWindowHandleService, in process
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);
        // use reactive, avoid async Task?
        while (process.HasExited is false)
        {
            CompositeDisposable disposables = [];
            await Task.Delay(1000);

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
                .Subscribe(size => HwndExtensions.ResizeClient(childWindow.Hwnd, size))
                .DisposeWith(disposables);

            await childWindowClosedChannel.Reader.WaitToReadAsync();

            disposables.Dispose();
        }
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
