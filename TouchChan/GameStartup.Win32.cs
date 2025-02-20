﻿using LightResults;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace TouchChan;

public sealed class WindowHandleNotFoundError : Error;

public sealed class ProcessExitedError : Error;

public sealed class ProcessPendingExitedError : Error;

public static partial class GameStartup
{
    private const int GoodWindowWidth = 320;
    private const int GoodWindowHeight = 240;

    /// <summary>
    /// 查找合适的窗口句柄，这里需要等待是因为超时处理
    /// </summary>
    public static async Task<Result<nint>> FindGoodWindowHandleAsync(Process proc)
    {
        const int SearchWindowTimeout = 20000;
        const int CheckResponse = 16;

        var goodHandle = proc.MainWindowHandle;

        if (goodHandle == nint.Zero)
            return Result.Failure<nint>(new ProcessPendingExitedError());

        PInvoke.GetClientRect(new(goodHandle), out var clientRect);

        if (IsGoodWindow(clientRect))
            return goodHandle;

        var cts = new CancellationTokenSource(SearchWindowTimeout);
        var timeoutToken = cts.Token;
        Log.Do3("loop start", true);
        var count = 0;
        while (!timeoutToken.IsCancellationRequested)
        {
            count++;
            if (proc.HasExited)
            {
                Log.Do3($"loop exit {count}");
                return Result.Failure<nint>(new ProcessExitedError());
            }

            var windows = GetWindowsOfProcess(proc);
            foreach (var handle in windows)
            {
                PInvoke.GetClientRect(handle, out var rect);

                if (IsGoodWindow(rect))
                {
                    Log.Do3($"loop found {count}");
                    return (nint)handle;
                }
            }

            await Task.Delay(CheckResponse);
        }

        return Result.Failure<nint>(new WindowHandleNotFoundError());
    }

    private static List<HWND> GetWindowsOfProcess(Process proc)
    {
        var list = new List<HWND>();

        BOOL ChildProc(HWND handle, LPARAM pointer)
        {
            _ = PInvoke.GetWindowThreadProcessId(handle, out var relativeProcessId);
            if (relativeProcessId != proc.Id)
                return true;

            list.Add(handle);
            return true;
        }
        PInvoke.EnumChildWindows(HWND.Null, ChildProc, default);

        return list;
    }

    private static bool IsGoodWindow(RECT rect) =>
        rect.bottom > GoodWindowHeight && rect.right > GoodWindowWidth;
}