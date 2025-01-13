AOT发布，把程序先运行起来再尝试删除文件夹。我得到了约 40MB 的大小
仓库名改 tachichan
商店页展览用 掌机斜侧面拍照渲染

适用于 TouchChanNext 的一些编码风格规范

1. 使用 try-catch 语句在可能出现异常的方法上，不重构非业务性质的 try-catch 语句
2. 返回到用户的错误信息指明错误的原因，以及提示解决方案
3. 除了有意图的情况，catch 的异常需要记录 Exception
4. 一个类中，依赖于类中的其他方法，排列顺次处于被依赖的方法上方，没有太多依赖的顺次放调用者的下面

Log 重构使用可以匹配一键替换 Debug.WriteLine({}) 的方式

1. 为 R3 提供 ReactiveMarbles.ObservableEvents.SourceGenerator，this.Events() 生成事件流
2. 使用 System.Drawing.Common + CSWin32 实现 Splash 窗口
