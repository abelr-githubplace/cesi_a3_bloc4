using System.Globalization;
using System.Threading;
using System.Windows;
using BusinessSoftware;
using Config;

namespace EasySave.GUI
{
    public partial class App : Application
    {
        public App() { }

        protected override void OnExit(ExitEventArgs e)
        {
            // Tear down the global poll task so the process can exit cleanly.
            try { BusinessSoftwareWatcher.Get(ConfigManager.Get()).Dispose(); }
            catch { /* nothing useful to do this late */ }
            base.OnExit(e);
        }
    }
}
