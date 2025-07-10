using System.Diagnostics;
using System.IO;
using TouchChanX;
using Application = System.Windows.Application;

// Prepare game path from command line parameter

if (args.Length < 1)
    return;

var gamePath = args[0];

if (File.Exists(gamePath) && Path.GetExtension(gamePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
{
    if (ExtractLnkPath(gamePath) is not { } path)
        return;
    
    gamePath = path;
}

if (!File.Exists(gamePath))
    return;

// Start gathering game process and launch TachiChan window
// var thread = new Thread(() => DummyAction(gamePath));
// thread.SetApartmentState(ApartmentState.STA);
// thread.Start();

// Start splash screen
// splash.Run();

Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

var win = new MainWindow();
var hooker = new GameWindowHooker(0x6E0612);
hooker.Bind(win);
var app = new Application(); 
app.Run(win);
return;

static string? ExtractLnkPath(string path)
{
    try
    {
        return TouchChanX.IOHelper.ShortcutResolver.ExtractLnkPath(path);
    }
    catch (Exception e)
    {
        Debug.WriteLine(e);
        return null;
    }
}
