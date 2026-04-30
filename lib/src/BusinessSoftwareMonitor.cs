using System.Diagnostics;

namespace BusinessSoftwareMonitor
{
    public static class BusinessSoftwareMonitor
    {
        public static List<string> GetRunningBusinessSoftware(IEnumerable<string> watchedProcesses)
        {
            var running = new List<string>();
            foreach (var name in watchedProcesses)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                try
                {
                    var processes = Process.GetProcessesByName(name);
                    if (processes.Length > 0) running.Add(name);
                    foreach (var p in processes) p.Dispose();
                }
                catch
                {
                    // Process API can throw on platforms without permissions; treat as not running.
                }
            }
            return running;
        }

        public static bool IsAnyRunning(IEnumerable<string> watchedProcesses)
        {
            return GetRunningBusinessSoftware(watchedProcesses).Count > 0;
        }
    }
}
