# TouchChan.SplashScreenGdiPlus

## 简介

基于 System.Drawing.Common (GDI+) 以极少的代码实现了一个展现透明图片的高性能 Splash 窗口。

核心代码只有 `DisplaySplash()` 50 行左右。

在 i7-8650U 的平台上 Aot 编译后，调用 `WithShowAndExecuteAsync` 开始到出现 Splash 的耗时约为 30ms 左右。

## 依赖

* System.Drawing.Common (GDI+ 的封装)
* CSWin32 (与 win32 api 交互，包括创建窗口等)

## 项目实现了

* 透明图片在 Primary Screen 上居中显示
* 高 dpi 下自动缩放图片，请使用以 96px 为倍数的图片，不低于 192*192 像素的图片
* 由 CSWin32 提供的 no marshaling P/Invoke 生成

## 可能存在的问题

使用先创建窗口，再设置 WS_EX_LAYERED，指定 #FF00800(Green) 颜色为透明通道的方式建立的透明窗口。实践中发现有出现透明效果有失效的情景的可能性。

## 大小占用

* 39kb .dll release build (包含 CSWin32)
* 475kb System.Drawing.Common