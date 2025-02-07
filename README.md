### 性能优化 Performance
- [ ] 搞明白 TouchControl.InitializeTouchControl() 中，订阅流产生流程以及源流重复触发的原因

### 行为调整 Improvement
- [ ] 保证 Touch 小圆点单一实例，多游戏不启动或附加
- [ ] Touch 监控配置文件变化响应通知

### 已知问题 Bug
- [ ] 按钮处于四边时更改窗口大小可能超出边界

### 框架相关问题
- [ ] 验证 WAS Shit x，退出 winui3 程序究竟是否是在窗口显示前的线程上调用 Exit() 导致的

### 疑惑
- [ ] Aot int.TryParse 为何首次执行耗时
- [ ] R3.WinUI 设置 Observable 导致 cpu 占用，是否需要检查原因或报告问题 

---

## Massive Work

### 重构 Refactor
- [ ] 使用源生成替代手写事件转 Observable *1

### 新功能 Feature
- [ ] 为系统安装虚拟鼠标设备
- [ ] Suspend 游戏进程 *2
- [ ] 买断支持者或捐赠者提供个性化配置及联网恢复 *3


### 第一阶段

- [x] 验证启动游戏到绑定游戏的整个生命周期流程，确保错误处理的正确方式。
- [x] 窗口确保，保证游戏的有效窗口查找逻辑。
- [x] Xaml 样式资源，按钮样式还原。

### 下一阶段

- [ ] Touch 大小需跟随窗口大小变化，可以考虑区分为俩个或多个大小
- [ ] 重写 ShellHandlerMenu，i18n。
- [ ] ViewModel 结构，Config 文件配置存储。

### 最终阶段

- [ ] 进一步为按钮提供业务逻辑。。Menu等。
- [ ] 其他工作，如 splash icon 像素点清洁，考虑 TachiChan 图标 renew。
- [ ] 重铸 Preference，待考虑原型设计。

### 发布

AOT发布，把程序先运行起来再尝试删除文件夹。我得到了约 40MB 的大小。

商店页展览用 掌机斜侧面拍照渲染。

### 详情

*1 为 R3 提供 R3.ObservableEvents.SourceGenerator，以 this.Events() 的形式为前端框架事件生成事件流。

*2 Suspend 游戏进程，Resume 游戏进程。在上面覆盖一个半透明窗口，放置功能按钮，如【恢复】【其他功能】(需要一个建议窗口，详细吸收用户意见)

*3 比如小圆点的旧样式可以作为会员提供，验证邮箱或手机号后，根据计算机相关值计算一个新值存储到配置文件

### 信息反馈与错误处理

任何意料之外的退出使用 Environment.Exit(1)
正常退出使用 Environment.Exit(0)

try-catch
Error 级别：意料之外的异常，需要打印出 Exception 本身，包括调用堆栈结构。
Info 级别：意料之中的异常处理，仅仅是为了在 Output 窗口中，指引解释 “引发的异常...” 的原因。如果没有 Output 窗口，甚至不需要。

反馈到用户的错误信息一定是需要 i18n 的。
