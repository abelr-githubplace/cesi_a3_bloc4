using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public sealed class Logger
    {
        private static Logger _instance;
        private static readonly object _lock = new object();

        private readonly string logDirectory;

        // Constructeur privé
        private Logger()
        {
            logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "Logs"
            );

            Directory.CreateDirectory(logDirectory);
        }

        // Accès global thread‑safe
        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new Logger();
                        }
                    }
                }
                return _instance;
            }
        }

        public void WriteLog(LogInfo logInfo)
        {
            string filePath = Path.Combine(
                logDirectory,
                DateTime.Now.ToString("yyyy-MM-dd") + ".json"
            );

            List<LogInfo> logs;

            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                logs = JsonSerializer.Deserialize<List<LogInfo>>(existingJson)
                       ?? new List<LogInfo>();
            }
            else
            {
                logs = new List<LogInfo>();
            }

            logs.Add(logInfo);

            File.WriteAllText(
                filePath,
                JsonSerializer.Serialize(logs, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );
        }
    }
}