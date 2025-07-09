using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using TouchChanX;
using Application = System.Windows.Application;

Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
Thread.CurrentThread.SetApartmentState(ApartmentState.STA);

var win = new MainWindow();
// win.Left = -32000;
// win.Top = -32000;

var app = new Application();
app.Run(win);

record Wtf(string Abc, bool Aaa);
