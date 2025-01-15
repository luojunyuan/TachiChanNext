### 第一阶段

- [ ] 验证启动游戏到绑定游戏的整个生命周期流程，确保错误处理的正确方式。

### 发布

AOT发布，把程序先运行起来再尝试删除文件夹。我得到了约 40MB 的大小。

商店页展览用 掌机斜侧面拍照渲染

### 其他可选项

1. 为 R3 提供 ReactiveMarbles.ObservableEvents.SourceGenerator，this.Events() 为前端框架事件生成事件流
2. 使用 System.Drawing.Common + CSWin32 实现 Splash 窗口。
	* 比起纯原生 win32 实现可能性能相对差，考虑完全分离。（使用 benchmark 验证）
	* 分离发布包，SplashScreenGdiPlus.Drawing, SplashScreenGdiPlus.Win32。两个仓库两个包。
