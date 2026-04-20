using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace EasyLog
{
    public class Logger
    {
        private readonly string logDirectory;

        public Logger()
        {
            logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave",
                "Logs"
            );

            Directory.CreateDirectory(logDirectory);
        }

        public void WriteLog(LogEntry entry)
        {
            string filePath = Path.Combine(
                logDirectory,
                DateTime.Now.ToString("yyyy-MM-dd") + ".json"
            );

            List<LogEntry> logs;

            if (File.Exists(filePath))
            {
                string existingJson = File.ReadAllText(filePath);
                logs = JsonSerializer.Deserialize<List<LogEntry>>(existingJson)
                       ?? new List<LogEntry>();
            }
            else
            {
                logs = new List<LogEntry>();
            }

            logs.Add(entry);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(logs, options);
            File.WriteAllText(filePath, json);
        }
    }
}