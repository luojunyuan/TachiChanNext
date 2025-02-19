### 疑惑
- [ ] Aot int.TryParse 为何首次执行耗时
- [x] ~~R3.WinUI 设置 Observable 导致 cpu 占用，是否需要检查原因或报告问题~~

### 行为调整 Improvement
- [ ] 保证 Touch 小圆点单一实例，多游戏不启动或附加
- [ ] Touch 监控配置文件变化响应通知

---

## Massive Work

### 新功能 Feature
- [ ] 为系统安装虚拟鼠标设备
- [ ] Suspend 游戏进程 *2
- [ ] ~~买断支持者或捐赠者提供个性化配置及联网恢复 *3~~


### 第一阶段

- [x] 验证启动游戏到绑定游戏的整个生命周期流程，确保错误处理的正确方式。
- [x] 窗口确保，保证游戏的有效窗口查找逻辑。
- [x] XAML 样式资源，按钮样式还原。

### 下一阶段

- [x] Touch 大小需跟随窗口大小变化，可以考虑区分为俩个或多个大小
- [ ] 重写 ShellHandlerMenu，i18n。
- [ ] ViewModel 结构，Config 文件配置存储。

### 最终阶段

- [ ] 进一步为按钮提供业务逻辑。。Menu等。
- [ ] 其他工作，如 splash icon 像素点清洁，考虑 TachiChan 图标 renew。
- [ ] 重铸 Preference，待考虑原型设计。

### 关于右键菜单

Preference 设置中自动检查 LE 的注册地址，默认以灰色使用安装地址，也可以自设路径。不提供安装，如果路径不存在红色提示。不标明 LE，自定义第三方工具启动。

上下文菜单中， Custom start... ，如果没有安装LE或设置路劲，灰色不允许点击，tooltip提示请先设置自定义启动工具，自定义启动...

### 发布

- [ ] AOT发布，把程序先运行起来再尝试删除文件夹。我得到了约 40MB 的大小。正式打包不考虑清理，不用管
- [ ] 商店页展览用 掌机斜侧面拍照渲染。

### 详情

*1 需要 hook 父窗口，或者 hook 本身的窗口事件。WM_PARENTNOTIFY（父窗口消息？）WM_DESTROY，WM_NCDESTROY 等，WH_CBT WindowHook，拦截子窗口的 HCBT_DESTROYWND。需要在子窗口接收到销毁事件前，把它移动出来，先隐藏再 SetParent nint.Zero 到桌面。最后再按正常流程设置到新窗口上去。

可能的调用顺序： WM_CLOSE -> DestroyWindow(parentHwnd) -> DestroyWindow(childHwnd) -> 发送 WM_DESTROY ->
每个子窗口收到 WM_DESTROY WM_NCDESTROY(hWnd将被释放) fire EVENT_OBJECT_DESTROY -> Parent 自己本身的 WM_DESTROY WM_NCDESTROY 释放 hWnd

可能的解决方法：在 WM_PARENTNOTIFY 监听 WM_DESTROY

原本以为是 WAS Shit x 由线程问题引起的 Exit 异常，其实不是。只是子窗口退出异常。

*2 Suspend 游戏进程，Resume 游戏进程。在上面覆盖一个半透明窗口，放置功能按钮，如【恢复】【其他功能】(需要一个建议窗口，详细吸收用户意见)。一些游戏可能直接电源键睡眠，无法恢复，而先 Suspend 反而可以正常恢复也说不定。

*3 比如小圆点的旧样式可以作为会员提供，验证邮箱或手机号后，根据计算机相关值计算一个新值存储到配置文件

*5 曾经可以应该也只是在内部做了刷新处理，而不是真正解决同步顺序问题。

### 信息反馈与错误处理

任何意料之外的退出使用 Environment.Exit(1)
正常退出使用 Environment.Exit(0)

try-catch
Error 级别：意料之外的异常，需要打印出 Exception 本身，包括调用堆栈结构。
Info 级别：意料之中的异常处理，仅仅是为了在 Output 窗口中，指引解释 “引发的异常...” 的原因。如果没有 Output 窗口，甚至不需要。

反馈到用户的错误信息一定是需要 i18n 的。
