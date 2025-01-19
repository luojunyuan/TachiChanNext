项目使用了现代风格的 csharp 编码风格，但是并不是很激进。
比如，使用了 Result 等函数式相关概念，但是缺少 match。
保留了项目中的 null，因为我暂时没有找到好的 Option 可以满足需要。比如取列表的第一个元素，实现一个 TryFirst 方法返回 Option 类型。

### 第一阶段

- [ ] 验证启动游戏到绑定游戏的整个生命周期流程，确保错误处理的正确方式。
- [ ] 窗口确保，保证游戏的有效窗口查找逻辑 (Magpie 方式)。
- [ ] Xaml 样式资源，按钮样式还原。

### 下一阶段

- [ ] 重写 ShellHandlerMenu，i18n。
- [ ] ViewModel 结构，Config 文件配置存储。

### 最终阶段

- [ ] 进一步为按钮提供业务逻辑。。Menu等。
- [ ] 其他工作，如 splash icon 像素点清洁，考虑 TachiChan 图标 renew。
- [ ] 重铸 Preference，待考虑原型设计。

### 发布

AOT发布，把程序先运行起来再尝试删除文件夹。我得到了约 40MB 的大小。

商店页展览用 掌机斜侧面拍照渲染。

### 其他可选项

1. 为 R3 提供 ReactiveMarbles.ObservableEvents.SourceGenerator，this.Events() 为前端框架事件生成事件流。
2. 使用 System.Drawing.Common + CSWin32 实现 Splash 窗口。
	* 比起纯原生 win32 实现可能性能相对差，考虑完全分离。（使用 benchmark 验证）
	* 分离发布包，SplashScreenGdiPlus.Drawing, SplashScreenGdiPlus.Win32。两个仓库两个包。
3. Suspend 游戏进程，Resume 游戏进程。在上面覆盖一个半透明窗口，放置功能按钮，如【恢复】【其他功能】(需要一个建议窗口，详细吸收用户意见)

### 错误处理

任何意料之外的退出目前是 Environment.Exit(1)
正常退出是 Environment.Exit(0)

try-catch
Error 级别：意料之外的异常，需要打印出 Exception 本身，包括调用堆栈结构。(暂且用 Trace 记录)
Info 级别：意料之中的异常处理，仅仅是为了在 Output 窗口中，指引解释 “引发的异常...” 的原因。如果没有 Output 窗口，甚至不需要。

反馈到用户的错误信息一定是需要 i18n 的。
