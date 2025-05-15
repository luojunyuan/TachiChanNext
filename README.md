针对旧项目 https://github.com/luojunyuan/TachiChan 的重写版本，得益于 [`SetWindowRgn`](https://learn.microsoft.com/zh-cn/windows/win32/api/winuser/nf-winuser-setwindowrgn) 的特性，才能够在 WinUI 上实现 TachiChan 需要的透明窗口的效果

### 本项目的特征
- 使用 Result 类型作为函数返回值来构建业务逻辑（ Prefer `Result<T>` over `Exception` ）
- 使用 Observable(流) 替代 Event(事件)
- 使用 Reactive Extensions ([R3](https://github.com/Cysharp/R3)) 反应式构建 UI 拖动交互，数据处理等核心逻辑
- 简单封装部分窗口扩展方法，假装调用看起来更 Fluent、统一声明式风格
- 对程序体积大小不敏感，合理利用第三方库来减少工作量和复杂性
- 使用较为 hack 的 WS_CHILD style 方法来嵌入 WinUI 窗口到其他窗口，导致 WinUI 部分功能被破坏，或者有一定不稳定性，需要逐一排查和测试
