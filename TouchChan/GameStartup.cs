using LightResults;
using System.ComponentModel;
using System.Diagnostics;
using WindowsShortcutFactory;

namespace TouchChan;

public static class GameStartup
{
    /// <summary>
    /// 启动游戏进程
    /// </summary>
    public static async Task<Result<Process>> LaunchGameAsync(string path, bool leEnable)
    {
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

        const int WaitMainWindowTimeout = 20000;
        const int UIMinimumResponseTime = 50;

        using var cts = new CancellationTokenSource(WaitMainWindowTimeout);
        var timeoutToken = cts.Token;

        while (!timeoutToken.IsCancellationRequested)
        {
            var gameProcess = await GetWindowProcessByPathAsync(path);
            if (gameProcess != null)
                return gameProcess;

            await Task.Delay(UIMinimumResponseTime);
        }

        // leProc.kill()
        return Result.Failure<Process>("Failed to start game within the timeout period.");
    }

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
            Trace.WriteLine(ex);
            return Result.Failure<string>($"Failed when resolve \"{path}\", please try start from game folder.");
        }

        if (!File.Exists(resolvedPath))
            return Result.Failure<string>($"Resolved link path \"{resolvedPath}\" not found, please try start from game folder.");

        return resolvedPath;
    }

    /// <summary>
    /// 尝试通过限定的程序路径获取对应正在运行的，存在 MainWindowHandle 的进程
    /// </summary>
    public static Task<Process?> GetWindowProcessByPathAsync(string gamePath)
    {
        var friendlyName = Path.GetFileNameWithoutExtension(gamePath);
        return Task.Run(() => Process.GetProcessesByName(friendlyName)
            .Where(p =>
            {
                try
                {
                    return p.MainModule?.FileName.Equals(gamePath, StringComparison.OrdinalIgnoreCase) ?? false;
                }
                catch (Win32Exception ex)
                {
                    // NOTE: 在获取 proc.MainModule 相关信息过程中，有概率进程已经退出了
                    Debug.WriteLine(nameof(GetWindowProcessByPathAsync) + ex.Message);
                    return false;
                }
            })
            .OrderBy(p => p.StartTime)
            .FirstOrDefault(p => p.MainWindowHandle != nint.Zero));
    }
}
