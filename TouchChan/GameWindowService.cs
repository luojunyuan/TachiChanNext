using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using R3;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace TouchChan;

public static partial class ServiceLocator
{
    private static GameWindowService? _current;

    public static void InitializeWindowHandle(nint newHandle) => _current = new(newHandle); // Log

    public static GameWindowService GameWindowService => _current ??
        throw new ArgumentNullException($"{nameof(GameWindowService)} is not initialized.");
}

public class GameWindowService : IDisposable
{
    public GameWindowService(nint handle)
    {
        WindowHandle = handle;
        DpiScale = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14398) ? handle.GetDpiForWindow() / 96d : 1;
        InstallDpiChangedMessage();
    }

    public nint WindowHandle { get; }

    public double DpiScale { get; private set; }

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

    private static SafeHandle? HookId;

    private static void InstallDpiChangedMessage()
    {
        var moduleHandle = PInvoke.GetModuleHandle((string?)null); // get current exe instant handle

        HookId = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, Hook, moduleHandle, 0); // tid 0 set global hook
        if (HookId.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        static LRESULT Hook(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (nCode < 0)
                return PInvoke.CallNextHookEx(HookId, nCode, wParam, lParam);

            var msg = Marshal.PtrToStructure<CWPSTRUCT>(lParam);

            const int WM_DPICHANGED = 0x02e0;
            if (msg.message == WM_DPICHANGED)
            {
                int newDpiX = (int)(wParam & 0xFFFF);
                int newDpiY = (int)((wParam >> 16) & 0xFFFF);

                System.Diagnostics.Debug.WriteLine($"DPI Changed: {newDpiX}x{newDpiY}");
            }

            return PInvoke.CallNextHookEx(HookId, nCode, wParam, lParam);
        }
    }

    public void Dispose()
    {
        HookId?.Close();
        GC.SuppressFinalize(this);
    }

}
