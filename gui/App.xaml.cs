using System.Globalization;
using System.Threading;
using System.Windows;

namespace EasySave.GUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            base.OnStartup(e);
        }
    }
}