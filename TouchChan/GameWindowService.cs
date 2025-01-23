﻿using System.Drawing;
using System.Runtime.InteropServices;
using R3;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;

namespace TouchChan;

public static class GameWindowService
{
    private const uint EventObjectLocationChange = 0x800B;

    private const int OBJID_WINDOW = 0;
    private const long SWEH_CHILDID_SELF = 0;

    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private const uint WinEventHookInternalFlags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;

    /// <summary>
    /// 监听游戏窗口大小变化
    /// </summary>
    /// <remarks>必须在 UI 线程中订阅</remarks>
    public static Observable<Size> ClientSizeChanged(nint windowHandle) =>
        Observable.Create<Size>(observer =>
        {
            PInvoke.GetClientRect(new(windowHandle), out var initRect);
            var lastGameWindowSize = initRect.Size;
            observer.OnNext(lastGameWindowSize);

            var winEventDelegate = new WINEVENTPROC((hWinEventHook, eventId, hWnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
            {
                if (eventId == EventObjectLocationChange &&
                    hWnd.Value == windowHandle &&
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
            var targetThreadId = PInvoke.GetWindowThreadProcessId(new(windowHandle), out var processId);
            var windowsEventHook = PInvoke.SetWinEventHook(
                EventObjectLocationChange, EventObjectLocationChange,
                null, winEventDelegate, processId, targetThreadId,
                WinEventHookInternalFlags);

            return Disposable.Create(() =>
            {
                windowsEventHook.Close();
                winEventDelegateHandle.Free();
            });
        });

    private const uint EventObjectDestroy = 0x8001;

    // NOTE: 要注意重复订阅相同 windowHandle （已经销毁）是不符合预期的
    /// <summary>
    /// 监听游戏窗口销毁消息
    /// </summary>
    /// <remarks>必须在 UI 线程中订阅</remarks>
    public static Observable<Unit> WindowDestroyed(nint windowHandle) =>
        Observable.Create<Unit>(observer =>
        {
            var winEventDelegate = new WINEVENTPROC((hWinEventHook, eventId, hWnd, idObject, idChild, dwEventThread, dwmsEventTime) =>
            {
                if (eventId == EventObjectDestroy &&
                    hWnd.Value == windowHandle &&
                    idObject == OBJID_WINDOW &&
                    idChild == SWEH_CHILDID_SELF)
                {
                    observer.OnNext(Unit.Default);
                }
            });
            var winEventDelegateHandle = GCHandle.Alloc(winEventDelegate);
            var targetThreadId = PInvoke.GetWindowThreadProcessId(new(windowHandle), out var processId);
            var windowsEventHook = PInvoke.SetWinEventHook(
                EventObjectDestroy, EventObjectDestroy,
                null, winEventDelegate, processId, targetThreadId,
                WinEventHookInternalFlags);

            return Disposable.Create(() =>
            {
                windowsEventHook.Close();
                winEventDelegateHandle.Free();
            });
        });
}
