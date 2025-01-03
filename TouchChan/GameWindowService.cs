using R3;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan;

public static partial class ServiceLocator
{
    private static GameWindowService? _current;

    public static void InitializeWindowHandle(nint newHandle, bool dpiUnaware) =>
        _current = new(newHandle, dpiUnaware); // Log

    public static GameWindowService GameWindowService => _current ??
        throw new ArgumentNullException($"{nameof(GameWindowService)} is not initialized.");
}

public class GameWindowService(nint handle, bool dpiUnaware)
{
    public nint WindowHandle { get; } = handle;

    public bool IsDpiUnaware { get; } = dpiUnaware;

    private const uint EventObjectLocationChange = 0x800B;

    private const int OBJID_WINDOW = 0;
    private const long SWEH_CHILDID_SELF = 0;

    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const uint WinEventHookInternalFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

    public Observable<Size> ClientSizeChanged() =>
        Observable.Create<Size>(observer =>
        {
            PInvoke.GetClientRect(new(WindowHandle), out var initRect);
            var lastGameWindowSize = initRect.Size;
            observer.OnNext(lastGameWindowSize);

            var winEventDelegate = new WINEVENTPROC((hWinEventHook, eventId, hWnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
            {
                if (eventId == EventObjectLocationChange &&
                    hWnd.Value == WindowHandle &&
                    idObject == OBJID_WINDOW &&
                    idChild == SWEH_CHILDID_SELF)
                {
                    PInvoke.GetClientRect(hWnd, out var rectClient);
                    if (rectClient.Size == lastGameWindowSize)
                        return;

                    lastGameWindowSize = rectClient.Size;
                    observer.OnNext(lastGameWindowSize);
                }
            });
            var winEventDelegateHandle = GCHandle.Alloc(winEventDelegate);
            var targetThreadId = PInvoke.GetWindowThreadProcessId(new(WindowHandle), out var processId);
            var windowsEventHook = PInvoke.SetWinEventHook(
                EventObjectLocationChange, EventObjectLocationChange,
                null, winEventDelegate, processId, targetThreadId,
                WinEventHookInternalFlags);

            return Disposable.Create(() =>
            {
                // 可能会生产 EventObjectDestroy 事件
                windowsEventHook.Close();
                winEventDelegateHandle.Free();
            });
        });
}
