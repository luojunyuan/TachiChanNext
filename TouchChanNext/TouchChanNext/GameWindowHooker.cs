using R3;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;

namespace TouchChan;

class GameWindowHooker
{
    private const uint EventObjectLocationChange = 0x800B;

    private const int OBJID_WINDOW = 0;
    private const long SWEH_CHILDID_SELF = 0;

    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const uint WinEventHookInternalFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

    public static Observable<Size> ClientSizeChanged(nint gameWindowHandle) =>
        Observable.Create<Size>(observer =>
        {
            PInvoke.GetClientRect(gameWindowHandle.ToHwnd(), out var initRect);
            var lastGameWindowSize = initRect.Size;
            observer.OnNext(lastGameWindowSize);

            var winEventDelegate = new WINEVENTPROC((hWinEventHook, eventId, hWnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
            {
                if (eventId == EventObjectLocationChange &&
                    hWnd.Value == gameWindowHandle &&
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
            var targetThreadId = PInvoke.GetWindowThreadProcessId(gameWindowHandle.ToHwnd(), out var processId);
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

