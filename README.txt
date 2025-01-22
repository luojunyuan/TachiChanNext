### 效率

性能极佳的一次启动例子

```
Main 0 ms  App Start
Main 7 ms  Warm Up AOT
Main 0 ms  InitializeComponent
Main 47 ms App OnLaunched Start
Main 0 ms  StartMainWindowAsync Start
Main 0 ms  PrepareValidGamePath
Main 0 ms  MainWindow
Async 0 ms  GetOrLaunchGameWithSplashAsync Start
Async 5 ms  GetWindowProcessByPathAsync
Async 1 ms  Splash
Async 34 ms Splash Showed
Async 0 ms  LaunchGameAsync Start
Async 15 ms Process.Start
Async 0 ms  Start search process
Main 95 ms MainWindow Activated
Async LaunchGameAsync End 21 (1276ms)
Main processResult got (1239ms)
Async 0 ms  GameWindowBindingAsync Start
Main 1 ms  StartMainWindowAsync End
Async 0 ms  Start FindRealWindowHandleAsync
Async 0 ms  FindGoodWindowHandleAsync
Async 5 ms  Subscribe
Async Window Destoyred (12967ms)
Async 0 ms  process.Refresh
```

直接命中Process的情况

```
Main 0 ms  App Start
Main 7 ms  Warm Up AOT
Main 0 ms  InitializeComponent
Main 27 ms App OnLaunched Start
Main 0 ms  StartMainWindowAsync Start
Main 0 ms  PrepareValidGamePath
Main 0 ms  MainWindow
Async 0 ms  GetOrLaunchGameWithSplashAsync Start
Async 5 ms  GetWindowProcessByPathAsync
Async 18 ms TryRestoreWindow
Main 73 ms MainWindow Activated
Main processResult got (0ms)
Async 0 ms  GameWindowBindingAsync Start
Async 0 ms  Start FindRealWindowHandleAsync
Async 0 ms  FindGoodWindowHandleAsync
Main 0 ms  StartMainWindowAsync End
Async 4 ms  Subscribe
Async Window Destoyred (5350ms)
Async 0 ms  process.Refresh
```


根据 branch aot messure，需要注意的点有
AOT预热 10ms
从启动到 App-OnLaunched 37-58ms
MainWindow 自身的激活 100ms 
（因为和搜索进程并行进行所以可能对实际开销有所影响，并且在游戏未启动前提下后者时间远远大于前者所以基本可以忽略）
[ Async
搜索是否已经存在进程 108ms
splash 23-45ms
Process.Start game 20ms
GetWindowProcessByPathAsync 循环搜索进程，消费1404ms 
（这一段启动是没办法控制的，因为要等待进程以及MainWindowHandle的启动）
（可以内部再打详情）
] 1452ms
FindRealWindowHandleAsync 直接命中默认MainWindowHandle 3ms
Preference 窗口激活 168ms

---

i7-8650U
Warmup 25ms
App-OnLaunched 31-39ms
ActivePreference 201-234ms
ActiveMainWindow 57-63ms
[ Async
GetWindowProcessByPathAsync 58-63ms
Splash 25-34ms
Process.Start 19ms 
search loop 1782ms
] 1833ms
找到handle后的行为和订阅 21-35ms

首次在某一计算机启动时，超长消耗
Process.Start 
