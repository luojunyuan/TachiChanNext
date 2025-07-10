using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using TouchChanX.Interop;

// ReSharper disable InconsistentNaming

namespace TouchChanX;

public class GameWindowHooker(nint WindowHandle) : IDisposable
{
    private const uint EventObjectDestroy = 0x8001;
    private const uint EventObjectLocationChange = 0x800B;

    private const int OBJID_WINDOW = 0;
    private const long SWEH_CHILDID_SELF = 0;

    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    private const uint WinEventHookInternalFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

    public void Bind(MainWindow mainWindow)
    {
        PInvokeCore.SetWindowLong(
            new HWND(mainWindow.Handle),
            WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT,
            WindowHandle);
        
        var initBounds = GetClientBounds(WindowHandle);
        UpdateWindowPosition(mainWindow, initBounds);
        
        var winEventDelegate = new WINEVENTPROC(
            (_, eventId, hWnd, idObject, idChild, _, _) =>
        {
            switch (eventId)
            {
                case EventObjectLocationChange when
                    hWnd == WindowHandle &&
                    idObject == OBJID_WINDOW &&
                    idChild == SWEH_CHILDID_SELF:
                {
                    // FIXME: 很大几率窗口内元素Size不更新，检查位置和大小数值都正常，PInvoke 也一样，改到最新框架也一样
                    // 尝试制造一个 Minimal Reproduce，我目前是通过最小化 restore Owner 窗口这样的行为
                    // 可以尝试仅设置 Owned 来复现
                    var bounds = GetClientBounds(WindowHandle);
                    // mainWindow.Dispatcher.InvokeAsync(() => UpdateWindowPosition(mainWindow, bounds));
                    break;
                }
                case EventObjectDestroy when
                    hWnd == WindowHandle &&
                    idObject == OBJID_WINDOW &&
                    idChild == SWEH_CHILDID_SELF:
                {
                    // NOTE: 必须要跳出 win32 消息上下文再去操作 MainWindow
                    mainWindow.Dispatcher.InvokeAsync(mainWindow.Close);
                    break;
                }
            }
        });
        
        _winEventDelegateHandle = GCHandle.Alloc(winEventDelegate);
        var targetThreadId = PInvoke.GetWindowThreadProcessId(new HWND(WindowHandle), out var processId);
        _windowsEventHook = PInvoke.SetWinEventHook(
            EventObjectDestroy, EventObjectLocationChange,
            null, winEventDelegate, processId, targetThreadId,
            WinEventHookInternalFlags);
    }
    
    private UnhookWinEventSafeHandle? _windowsEventHook;
    private GCHandle? _winEventDelegateHandle;

    private static Rect GetClientBounds(nint hWnd)
    {
        PInvoke.GetWindowRect(new HWND(hWnd), out var rect);
        PInvoke.GetClientRect(new HWND(hWnd), out var rectClient);

        var winShadow = (rect.Width - rectClient.right) / 2;
        var left = rect.left + winShadow;

        var wholeHeight = rect.bottom - rect.top;
        var winTitleHeight = wholeHeight - rectClient.bottom - winShadow;
        var top = rect.top + winTitleHeight;

        return new Rect(left, top, rectClient.Width, rectClient.Height);
    }

    private static void UpdateWindowPosition(MainWindow mainWindow, Rect newRect)
    {
        var dpi = mainWindow.Dpi;
        mainWindow.Left = newRect.Left / dpi;
        mainWindow.Top = newRect.Top / dpi;
        mainWindow.Width = newRect.Width / dpi;
        mainWindow.Height = newRect.Height / dpi;
    }

    public void Dispose()
    {
        _windowsEventHook?.Dispose();
        _winEventDelegateHandle?.Free();
    }
}