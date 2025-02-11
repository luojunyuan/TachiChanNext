仅因为同样的实现下 WinUI 触摸拖动 Touch 延迟感强烈，而转用 Avalonia 作为主要的开发框架。

##### 疑问

Q: WinUI 单一 MainWindow 是因为存在框架很难解决的内存泄漏而没有办法，ava 是否也应该使用单一 MainWindow 的结构

A: ava 能够正常依赖父窗口关闭而关闭，并且反馈 MainWindow 关闭事件，所以考虑关闭多窗口的模式并确保没有内存泄漏。

##### MISC

查找名称为 ONE 的进程并强制关闭：

`Get-Process | Where-Object { $_.Name -like "ONE" } | Stop-Process -Force`