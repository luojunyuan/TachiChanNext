TachiChan 何去何从？

已经不太有兴趣写功能上架云云了。。

1. WinUI3 没办法去很好的协调菜单动画 width height，用 ScaleTransform 吧，不得不去作对抗，能感觉和 AssitiveTouch 有明显差距，但是也不是不能接受的缺陷。
2. backwards 到 wpf，通过 netfx polysharp，来打造无三方依赖纯粹原生项目。经过一通验证后，感觉 owned 应该才是最靠谱的做法，并且不再纠结于 dpi 越简易越好。

目前想法重新偏向于 wpf 了。如果是 WindowChrome 的话 wpf 性能已经足够好了，netfx 还能保证体积，向前到 win7 也完全适用。相较起来 winui 就看不到什么优势了。

wpf + netfx + WindowChrome + owned + SetWindowRgn + 0 dependency
