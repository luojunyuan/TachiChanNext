~~仅因为同样的实现下 WinUI 触摸拖动 Touch 延迟感强烈，而转用 Avalonia 作为主要的开发框架。~~

### 已知问题 Bug

1. 点击 Touch 后，alt+enter 全屏切换快捷键失效（焦点失去）
2. Win7 虚拟机中，Aot 编译的产物，能运行看到 Splash，子窗口潜入了但是无法正常显示

##### 疑问

Q: WinUI 单一 MainWindow 是因为存在框架很难解决的内存泄漏而没有办法，ava 是否也应该使用单一 MainWindow 的结构

A: ava 能够正常依赖父窗口关闭而关闭，并且反馈 MainWindow 关闭事件，所以考虑关闭多窗口的模式并确保没有内存泄漏。

##### MISC

查找名称为 ONE 的进程并强制关闭：

`Get-Process | Where-Object { $_.Name -like "ONE" } | Stop-Process -Force`