using System.Diagnostics;
using LightResults;
using TouchChan.SplashScreenGdiPlus;
using WindowsShortcutFactory;

namespace TouchChan;

public class Log
{
    private static bool HasConsole { get; } = NativeMethods.GetConsoleWindow() != nint.Zero;

    private static readonly Stopwatch Stopwatch = new();

    public static void Do(string message, bool longWait = false)
    {
        var elapsedMilliseconds = Stopwatch.ElapsedMilliseconds;


        var output = longWait
            ? $"Main {message} ({elapsedMilliseconds + "ms"})"
            : $"Main {elapsedMilliseconds + " ms",-5} {message}";

        if (HasConsole) Console.WriteLine(output);
        else Debug.WriteLine(output);

        Stopwatch.Restart();
    }

    private static readonly Stopwatch Stopwatch2 = new();

    public static void Do2(string message, bool longWait = false,  bool recount = false)
    {
        if (recount)
            Stopwatch2.Restart();

        var elapsedMilliseconds = Stopwatch2.ElapsedMilliseconds;

        var output = longWait 
            ? $"Async {message} ({elapsedMilliseconds + "ms"})"
            : $"Async {elapsedMilliseconds + " ms",-5} {message}";

        if (HasConsole) Console.WriteLine(output);
        else Debug.WriteLine(output);

        Stopwatch2.Restart();
    }

    private static readonly Stopwatch Stopwatch3 = new();

    public static void Do3(string message)
    {
        var elapsedMilliseconds = Stopwatch3.ElapsedMilliseconds;

        var output = $"san {elapsedMilliseconds + " ms",-5} {message}";
        if (HasConsole) Console.WriteLine(output);
        else Debug.WriteLine(output);

        Stopwatch3.Restart();
    }

    public static void Do(int message) => Do(message.ToString());
}

public static partial class GameStartup
{
    /// <summary>
    /// 准备有效的游戏路径
    /// </summary>
    public static Result<string> PrepareValidGamePath(string path)
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

    public static async Task<Result<Process>> GetOrLaunchGameWithSplashAsync(string path, bool leEnable)
    {
        Log.Do2("GetOrLaunchGameWithSplashAsync Start");

        var process = await GetWindowProcessByPathAsync(path);
        if (process != null)
        {
            Log.Do2("GetWindowProcessByPathAsync");
            Win32.TryRestoreWindow(process.MainWindowHandle);
            Log.Do2("TryRestoreWindow");
            return process;
        }

        Log.Do2("GetWindowProcessByPathAsync");

        using var fileStream = EmbeddedResource.KleeGreen;

        Log.Do2("Splash");
        return await SplashScreen.WithShowAndExecuteAsync(fileStream, async () =>
        {
            Log.Do2("Splash Showed");
            var launchResult = await LaunchGameAsync(path, leEnable);
            if (launchResult.IsFailure(out var launchGameError, out var launchedProcess))
                return Result.Failure<Process>(launchGameError.Message);

            return launchedProcess;
        });
    }

    /// <summary>
    /// 启动游戏进程
    /// </summary>
    private static async Task<Result<Process>> LaunchGameAsync(string path, bool leEnable)
    {
        Log.Do2("LaunchGameAsync Start");
        // NOTE: NUKITASHI2(steam) 会先启动一个进程闪现黑屏窗口，然后再重新启动游戏进程

        // TODO: 通过 LE 启动，思考检查游戏id好的方法，处理超时和错误情况
        // 考虑 LE 通过注册表查找还是通过配置文件，还是通过指定路径来启动

        // NOTE: 设置 WorkingDirectory 在游戏路径，避免部分游戏无法索引自身资源导致异常
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path),
            EnvironmentVariables = { ["__COMPAT_LAYER"] = "HighDpiAware" }
        };
        _ = Process.Start(startInfo);
        Log.Do2("Process.Start");

        const int WaitMainWindowTimeout = 20000;
        const int UIMinimumResponseTime = 50;

        // NOTE: 这是反复-超时任务的最佳实践，基于任务驱动
        using var cts = new CancellationTokenSource(WaitMainWindowTimeout);
        var timeoutToken = cts.Token;

        Log.Do2("Start search process");
        var count = 0;
        while (!timeoutToken.IsCancellationRequested)
        {
            count++;
            var gameProcess = await GetWindowProcessByPathAsync(path);

            if (gameProcess != null)
            {
                // leProc?.kill()
                Log.Do2($"LaunchGameAsync End {count}", true);
                return gameProcess;
            }

            await Task.Delay(UIMinimumResponseTime);
        }

        return Result.Failure<Process>("Failed to start game within the timeout period.");
    }

    /// <summary>
    /// 尝试通过限定的程序路径获取对应正在运行的，存在 MainWindowHandle 的进程
    /// </summary>
    public static Task<Process?> GetWindowProcessByPathAsync(string gamePath)
    {
        var friendlyName = Path.GetFileNameWithoutExtension(gamePath);
        // FUTURE: .log main.bin situation
        return Task.Run(() => Process.GetProcessesByName(friendlyName)
            .FirstOrDefault(p =>
            {
                if (p.MainWindowHandle == nint.Zero)
                    return false;

                var mainModule = p.HasExited ? null : p.MainModule;
                return mainModule?.FileName.Equals(gamePath, StringComparison.OrdinalIgnoreCase) ?? false;
            }));
    }
}
