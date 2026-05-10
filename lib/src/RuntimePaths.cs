using System;
using System.IO;

namespace RuntimePaths
{
    // Single source of truth for where EasySave puts its runtime data on disk.
    // Cahier des charges 1.0 forbids ad-hoc locations like c:\temp\, so we
    // anchor everything under a per-user folder that exists on every Windows
    // install and that the app can always write to without admin rights.
    public static class RuntimePaths
    {
        private const string AppFolder = "ProSoft\\EasySave";

        public static string BaseDirectory
        {
            get
            {
                string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string dir = Path.Combine(root, AppFolder);
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogsDirectory
        {
            get
            {
                string dir = Path.Combine(BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string ConfigFile => Path.Combine(BaseDirectory, "config.json");
        public static string StateFile => Path.Combine(BaseDirectory, "state.json");
    }
}
