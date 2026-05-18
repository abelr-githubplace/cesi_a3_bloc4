using System.Windows;
using EasySave.Console.ViewModels;

namespace EasySave.Console;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow { DataContext = new AppVm() }.Show();
    }
}
