For the rewritten version of the old project [TachiChan](https://github.com/luojunyuan/TachiChan), the transparent window effect required by TachiChan on WinUI is achieved thanks to the feature of [`SetWindowRgn`](https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-setwindowrgn).

### Features of this project:
- Uses the Result type as the function return value to build business logic (Prefer `Result<T>` over `Exception`).
- Replaces events with Observable (streams).
- Utilizes Reactive Extensions ([R3](https://github.com/Cysharp/R3)) for reactive construction of core logic such as UI drag interactions and data handling.
- Uses the WS_CHILD style to embed the WinUI window into other windows, which causes some WinUI features to break.
