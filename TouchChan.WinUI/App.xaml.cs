using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using R3;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WindowsShortcutFactory;
using WinRT.Interop;

namespace TouchChan.WinUI;

public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();

#if !DEBUG
        UnhandledException += (sender, e) =>
        {
            Debug.WriteLine(e.Exception.Message);
            Environment.Exit(1);
        };
#endif
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        // WAS Shit 4: 在 desktop 下 LaunchActivatedEventArgs 接收不到命令行参数 #3368
        var arguments = Environment.GetCommandLineArgs()[1..];

        var isDefaultLaunch = arguments.Length == 0;

        var result = isDefaultLaunch switch
        {
            true => await LaunchPreference(),
            false => await StartMainWindow(arguments),
        };

        if (result is LaunchResult.Redirected or LaunchResult.Failed)
            Current.Exit();
    }

    private static void WhenGameProcessExit(EventArgs args) => Environment.Exit(0);

    private static async Task<LaunchResult> StartMainWindow(string[] arguments)
    {
        // return unknown arguments passing
        var pathOrPid = arguments[0];

        if (!int.TryParse(pathOrPid, out var processId))
        {
            var gamePath = pathOrPid;
            if (File.Exists(gamePath) && Path.GetExtension(gamePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                gamePath = WindowsShortcut.Load(gamePath).Path;
            }
            if (!File.Exists(gamePath))
            {
                await MessageBox.ShowAsync("error", "TachiChan");
                return LaunchResult.Failed;
            }

            var fileImage = "assets\\klee.png";
            var splash = WinUIEx.SimpleSplashScreen.ShowSplashScreenImage(fileImage);

            // start by locale emulator
            var aaa = Process.Start(gamePath);
            aaa.WaitForInputIdle();

            splash.Hide();

            // get process id
            processId = aaa.Id;
        }

        var process = Process.GetProcessById(processId);
        // NOTE: 设置为高 DPI 缩放时不支持非 DPI 感知的窗口
        var isDpiUnaware = OperatingSystem.IsWindowsVersionAtLeast(8, 1) && Win32.IsDpiUnaware(process.Id);

        process.EnableRaisingEvents = true;
        process.RxExited().Subscribe(WhenGameProcessExit);

        var uiThread = DispatcherQueue.GetForCurrentThread();
        _ = Task.Factory.StartNew(async () =>
        {
            while (process.HasExited is false)
            {
                // Begin 问题在于我如何得知窗口关闭了
                // 第一种方式，循环等待窗口关闭，去找新的窗口
                // 第二种方式，得到窗口关闭事件，去找新的窗口
                // 找新窗口
                ServiceLocator.InitializeWindowHandle(process.MainWindowHandle, isDpiUnaware);

                // QUES: 启动后，获得焦点无法放在最前面？是什么原因，需要重新激活焦点。今后再检查整个程序与窗口启动方式。（第二次又暂未观测到）
                uiThread.TryEnqueue(() =>
                {
                    // WAS Shit 7: 窗口存在内存泄漏 #9063
                    // TODO: 考虑将 MainWindow 设置到新的 GameWindowHandle，而不是关闭窗口
                    // TODO P0: 带来的新问题是，当他作为子窗口被关闭时，还可以附加到新的窗口上吗？待测试
                    var mainWindow = new MainWindow();
                    mainWindow.Activate();
                });
                await Task.Delay(-1);
                // await MainWindow/GameWindow Close
                // 要确保 窗口关闭后，有一段等待进程结束的结束期，不要再次进入查找窗口的循环
                // End
            }
        }, TaskCreationOptions.LongRunning);


        return LaunchResult.Success;
    }

    private static async Task<LaunchResult> LaunchPreference()
    {
        const string InstanceID = "13615F35-469B-4341-B3CE-121C694C042C";
        var mainInstance = AppInstance.FindOrRegisterForKey(InstanceID);
        if (!mainInstance.IsCurrent)
        {
            var appArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            await mainInstance.RedirectActivationToAsync(appArgs);
            return LaunchResult.Redirected;
        }

        var preference = new PreferenceWindow();
        preference.Activate();

        AppInstance.GetCurrent().RxActivated()
            .Subscribe(_ =>
            {
                // QUES: UI 线程中 preference.Activate() 似乎不启用
                var preferenceHandle = WindowNative.GetWindowHandle(preference);
                Win32.ActiveWindow(preferenceHandle);
            });

        return LaunchResult.Success;
    }

    enum LaunchResult
    {
        Success,
        Redirected,
        Failed,
    }
}
