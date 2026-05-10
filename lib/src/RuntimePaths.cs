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
                // EASYSAVE_HOME lets integration tests / portable installs
                // redirect runtime files to a sandbox or USB stick instead of
                // the per-user LOCALAPPDATA default.
                string? overrideDir = Environment.GetEnvironmentVariable("EASYSAVE_HOME");
                string dir;
                if (!string.IsNullOrWhiteSpace(overrideDir))
                {
                    dir = overrideDir;
                }
                else
                {
                    string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    dir = Path.Combine(root, AppFolder);
                }
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string LogsDirectory
        {
            get
            {
                // When EASYSAVE_HOME is set (typically integration tests), keep
                // the daily log next to state.json/config.json so the test
                // harness only has to look in one place.
                bool hasOverride = !string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("EASYSAVE_HOME"));
                string dir = hasOverride ? BaseDirectory : Path.Combine(BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string ConfigFile => Path.Combine(BaseDirectory, "config.json");
        public static string StateFile => Path.Combine(BaseDirectory, "state.json");
    }
}
