using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LightResults;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan;

public sealed class WindowHandleNotFoundError : Error;

public sealed class ProcessExitedError : Error;

public static partial class GameStartup
{
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

    private const int GoodWindowWidth = 320;
    private const int GoodWindowHeight = 240;

    private static IEnumerable<HWND> GetRootWindowsOfProcess(int pid)
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
