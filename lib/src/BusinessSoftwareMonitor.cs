using Config;
using EasyLog;
using System.Diagnostics;

namespace BusinessSoftware
{
    public class BusinessSoftwareMonitor
    {
        private static BusinessSoftwareMonitor? s_instance;
        private static readonly object s_lock = new();

        private bool _isBusinessRunning;
        public static readonly ManualResetEventSlim BusinessRunningEvent = new(true);
        private static readonly object s_businessRunningLock = new();

        private static bool s_monitor;
        private static readonly object s_monitorLock = new();

        private BusinessSoftwareMonitor()
        {
            s_monitor = true;
            _isBusinessRunning = false;
        }

        public static BusinessSoftwareMonitor Get()
        {
            if (s_instance == null)
            {
                lock(s_lock)
                {
                    s_instance ??= new BusinessSoftwareMonitor();
                }
            }
            return s_instance;
        }

        public void PauseBusinessRunning(string[] softwares)
        {
            lock (s_businessRunningLock)
            {
                if (_isBusinessRunning) return;
                _isBusinessRunning = true;
                BusinessRunningEvent.Reset();
            }
            ConfigManager.Get().Logger.Log(
                new LogBusinessSoftwareInterrupt { DateTime = DateTime.Now, Softwares = softwares }
                    .Format(ConfigManager.Get().GetLogFormatConfig())
            );
        }

        public void ResumeBusinessRunning()
        {
            lock (s_businessRunningLock)
            {
                if (!_isBusinessRunning) return;
                _isBusinessRunning = false;
                BusinessRunningEvent.Set();
            }
            ConfigManager.Get().Logger.Log(
                new LogBusinessSoftwareInterrupt { DateTime = DateTime.Now, Softwares = [] }
                    .Format(ConfigManager.Get().GetLogFormatConfig())
            );
        }

        public static List<string> GetRunningBusinessSoftware()
        {
            var running = new List<string>();
            foreach (var name in ConfigManager.Get().GetBusinessSoftwares())
            {
                Process[] processes = [];
                try
                {
                    processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0) running.Add(name);
                }
                catch { /* Process API can throw on platforms without permissions; treat as not running. */ }
                finally { foreach (var p in processes) p.Dispose(); }
            }
            return running;
        }

        public static bool IsAnyRunning() => GetRunningBusinessSoftware().Count > 0;

        public static void RunMonitor()
        {
            lock(s_monitorLock) { s_monitor = true; }
        }

        public static void StopMonitor()
        {
            lock (s_monitorLock) { s_monitor = false; }
        }

        public void Start()
        {
            lock(s_monitorLock) { s_monitor = true; }

            var local_monitor = false;
            while (true)
            {
                lock (s_monitorLock) { local_monitor = s_monitor; }
                while (local_monitor)
                {
                    List<string> softwares = GetRunningBusinessSoftware();
                    if (softwares.Count > 0) PauseBusinessRunning([..softwares]);
                    else ResumeBusinessRunning();
                    lock (s_monitorLock) { local_monitor = s_monitor; }
                }
            }
        }
    }
}
