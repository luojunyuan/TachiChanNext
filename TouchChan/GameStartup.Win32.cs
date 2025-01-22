using LightResults;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace TouchChan;

public sealed class WindowHandleNotFoundError : Error;

public sealed class ProcessExitedError : Error;

public static partial class GameStartup
{
    private const int GoodWindowWidth = 320;
    private const int GoodWindowHeight = 240;

    public static async Task<Result<nint>> FindGoodWindowHandleAsync(Process proc)
    {
        const int SearchWindowTimeout = 20000;

        var goodHandle = proc.MainWindowHandle;
        PInvoke.GetClientRect(new(goodHandle), out var clientRect);

        if (IsGoodWindow(clientRect))
        {
            return goodHandle;
        }

        var cts = new CancellationTokenSource(SearchWindowTimeout);
        var timeoutToken = cts.Token;
        while (!timeoutToken.IsCancellationRequested)
        {
            if (proc.HasExited)
                return Result.Failure<nint>(new ProcessExitedError());

            var ctsEnum = new CancellationTokenSource();

            await foreach (var handle in GetWindowsOfProcessAsync(proc, ctsEnum.Token))
            {
                PInvoke.GetClientRect(handle, out var rect);
                if (IsGoodWindow(rect))
                {
                    ctsEnum.Cancel();
                    return handle.Value;
                }
            }
        }

        return Result.Failure<nint>(new WindowHandleNotFoundError());
    }

    private static async IAsyncEnumerable<HWND> GetWindowsOfProcessAsync(Process proc, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var hWnd in GetAllChildWindowHandlesAsync(cancellationToken))
        {
            // performance issue?
            //if (proc.HasExited)
            //    yield break;

            _ = PInvoke.GetWindowThreadProcessId(hWnd, out var relativeProcessId);
            if (relativeProcessId == proc.Id)
                yield return hWnd;
        }
    }

    /// <summary>
    /// 获取所有子窗口句柄，EnumChildWindows 改造为异步枚举
    /// </summary>
    private static async IAsyncEnumerable<HWND> GetAllChildWindowHandlesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<HWND>();

        _ = Task.Run(() =>
        {
            try
            {
                PInvoke.EnumChildWindows(HWND.Null, ChildProc, default);
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }, CancellationToken.None);

        await foreach (var handle in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            yield return handle;
        }

        BOOL ChildProc(HWND handle, LPARAM pointer)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            channel.Writer.TryWrite(handle);
            return true;
        }
    }

    public static List<nint> GetAllChildWindowHandles()
    {
        var list = new List<HWND>();

        BOOL ChildProc(HWND handle, LPARAM pointer)
        {
            list.Add(handle);

            return true;
        }
        PInvoke.EnumChildWindows(HWND.Null, ChildProc, default);

        return list.Select(hWnd => hWnd.Value).ToList();
    }

    private static IEnumerable<HWND> GetAllChildWindowsEnumerable()
    {
        List<HWND> result = new();
        var listHandle = GCHandle.Alloc(result);

        static BOOL ChildProc(HWND handle, LPARAM pointer)
        {
            var gch = GCHandle.FromIntPtr(pointer);
            if (gch.Target is not List<nint> list)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<HWND>");
            }
            list.Add(handle);
            return true;
        }
        PInvoke.EnumChildWindows(HWND.Null, ChildProc, GCHandle.ToIntPtr(listHandle));

        return result;
    }

    private static IEnumerable<HWND> GetRootWindowsOfProcess(Process proc)
    {
        var rootWindows = GetChildWindows(HWND.Null);
        foreach (var hWnd in rootWindows)
        {
            _ = PInvoke.GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
            if (lpdwProcessId == proc.Id)
                yield return hWnd;
        }
    }

    private static bool IsGoodWindow(RECT rect) =>
        rect.bottom > GoodWindowHeight && rect.right > GoodWindowWidth;

    [Obsolete("Old")]
    public static async Task<Result<nint>> FindRealWindowHandleAsync(Process proc)
    {
        const int WaitGameStartTimeout = 20000;
        const int UIMinimumResponseTime = 50;

        var gameHwnd = proc.MainWindowHandle;

        PInvoke.GetClientRect(new(gameHwnd), out var clientRect);

        if (clientRect.bottom > GoodWindowHeight &&
            clientRect.right > GoodWindowWidth)
        {
            return gameHwnd;
        }
        else
        {
            var spendTime = new Stopwatch();
            spendTime.Start();
            while (spendTime.Elapsed.TotalMilliseconds < WaitGameStartTimeout)
            {
                if (proc.HasExited)
                    return Result.Failure<nint>(new ProcessExitedError());

                // Process.MainGameHandle should included in handles
                var handles = GetRootWindowsOfProcess(proc.Id);
                foreach (var handle in handles)
                {
                    PInvoke.GetClientRect(handle, out clientRect);
                    if (clientRect.bottom > GoodWindowHeight &&
                        clientRect.right > GoodWindowWidth)
                    {
                        return handle.Value;
                    }
                }
                await Task.Delay(UIMinimumResponseTime);
            }

            return Result.Failure<nint>(new WindowHandleNotFoundError());
        }
    }

    private static List<HWND> GetRootWindowsOfProcess(int pid)
    {
        var rootWindows = GetChildWindows(HWND.Null);
        var dsProcRootWindows = new List<HWND>();
        foreach (var hWnd in rootWindows)
        {
            _ = PInvoke.GetWindowThreadProcessId(hWnd, out var lpdwProcessId);
            if (lpdwProcessId == pid)
                dsProcRootWindows.Add(hWnd);
        }
        return dsProcRootWindows;
    }

    private static IEnumerable<HWND> GetChildWindows(HWND parent)
    {
        List<HWND> result = new();
        var listHandle = GCHandle.Alloc(result);
        try
        {
            static BOOL ChildProc(HWND handle, LPARAM pointer)
            {
                var gch = GCHandle.FromIntPtr(pointer);
                if (gch.Target is not List<nint> list)
                {
                    throw new InvalidCastException("GCHandle Target could not be cast as List<HWND>");
                }
                list.Add(handle);
                return true;
            }
            PInvoke.EnumChildWindows(parent, ChildProc, GCHandle.ToIntPtr(listHandle));
        }
        finally
        {
            if (listHandle.IsAllocated)
                listHandle.Free();
        }
        return result;
    }
}