using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using WindowsShortcutFactory;
using WinRT.Interop;

namespace TouchChan.WinUI;

public class LogArttubute : MethodBoundaryAspect.Fody.Attributes.OnMethodBoundaryAspect
{
    public static readonly Stopwatch Stopwatch = new();

    private static readonly string[] MethodsName = { "MoveNext", "GetXamlType", "BindMainWindowToGame" };

    public override void OnEntry(MethodBoundaryAspect.Fody.Attributes.MethodExecutionArgs args)
    {
        if (args.Method.IsSpecialName)
            return;

        if (MethodsName.Contains(args.Method.Name))
            return;

        var elapsedMilliseconds = Stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"{elapsedMilliseconds,-4} ms Enter {args.Method.Name,-20}");

        Stopwatch.Restart();
    }

    public override void OnExit(MethodBoundaryAspect.Fody.Attributes.MethodExecutionArgs args)
    {
        if (args.Method.IsSpecialName)
            return;

        if (MethodsName.Contains(args.Method.Name))
            return;

        var elapsedMilliseconds = Stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"{elapsedMilliseconds,-4} ms Leave {args.Method.Name,-20}");

        Stopwatch.Restart();
    }

    public static void Do(string message)
    {
        var elapsedMilliseconds = Stopwatch.ElapsedMilliseconds;

        Console.WriteLine($"{elapsedMilliseconds,-4} ms {message}");

        Stopwatch.Restart();
    }
}

[LogArttubute]
public partial class App : Application
{
    public static readonly SynchronizationContext UISyncContext;

    static App() => UISyncContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());

    public App()
    {
        // warm up (AOT)
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

        LaunchResult result;
        switch (isDefaultLaunch)
        {
            case true:
                result = await LaunchPreferenceAsync();
                break;
            case false:
                result = await StartMainWindowAsync(arguments);
                break;
            default:
                throw new InvalidOperationException("Invalid launch mode.");
        }

        if (result is LaunchResult.Redirected or LaunchResult.Failed)
            Current.Exit();
    }

    private static void WhenGameProcessExit(EventArgs args) => Environment.Exit(0);

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
            return Result.Failure<string>($"Failed when resolve \"{path}\".");
        }

        if (!File.Exists(resolvedPath))
            return Result.Failure<string>($"Resolved link path \"{resolvedPath}\" not found, please try start from game folder.");

        return resolvedPath;
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
                return Result.Failure<Process>(ex.Message);
            }
        }

        var gamePathResult = PrepareValidGamePath(arg);
        if (gamePathResult.IsFailure(out var error, out var gamePath))
            return Result.Failure<Process>(error.Message);

        var firstProcess = await GetProcessByGamePathAsync(gamePath);
        if (firstProcess != null)
            return firstProcess;

        // MASSIVE: Start by locale emulator, and retrive game process by loop
        var startInfo = new ProcessStartInfo
        {
            FileName = gamePath,
            WorkingDirectory = Path.GetDirectoryName(gamePath),
            EnvironmentVariables = { ["__COMPAT_LAYER"] = "HighDpiAware" }
        };
        return Process.Start(startInfo) ?? Result.Failure<Process>("error when start game.");
    }

    private static Task<Process?> GetProcessByGamePathAsync(string gamePath)
    {
        var friendlyName = Path.GetFileNameWithoutExtension(gamePath);
        return Task.Run(() => 
            Process.GetProcessesByName(friendlyName)
                .OrderBy(p => p.StartTime)
                .FirstOrDefault());
    }

    private static async Task<LaunchResult> StartMainWindowAsync(string[] arguments)
    {
        var pathOrPid = arguments[0];

        var processResult = await PrepareValidProcessAsync(pathOrPid);
        if (processResult.IsFailure(out var error, out var process))
        {
            await MessageBox.ShowAsync(error.Message);
            return LaunchResult.Failed;
        }

        var fileImage = "assets\\klee.png";
        var splash = WinUIEx.SimpleSplashScreen.ShowSplashScreenImage(fileImage);

        // TODO: Instaed of ... await GameMainWindowHandleAsync(process);
        process.WaitForInputIdle();
        splash.Hide();

        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(WhenGameProcessExit);

        // QUES: 似乎Process启动游戏后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。而附加窗口没有影响
        var childWindow = new MainWindow();
        childWindow.Activate();

        _ = Task.Factory.StartNew(() => BindWindowToGame(childWindow, process), TaskCreationOptions.LongRunning);

        return LaunchResult.Success;
    }

    /// <summary>
    /// 绑定窗口到游戏进程
    /// </summary>
    /// <param name="childWindow"></param>
    /// <param name="process"></param>
    /// <returns></returns>
    private static async Task BindWindowToGame(MainWindow childWindow, Process process)
    {
        // 设计一个游戏的CurrentMainWindowHandleService, in process
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);
        // use reactive, avoid async Task?
        while (process.HasExited is false)
        {
            CompositeDisposable disposables = [];
            Debug.WriteLine("try to bind window");
            await Task.Delay(1000);

            ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

            var childWindowClosedChannel = Channel.CreateUnbounded<Unit>();
            ServiceLocator.GameWindowService.WindowDestoyed()
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
