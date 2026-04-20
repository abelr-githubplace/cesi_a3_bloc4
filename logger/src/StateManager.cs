using System;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public class StateManager
    {
        private readonly string stateFilePath;

        public StateManager()
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave"
            );

            Directory.CreateDirectory(basePath);

            stateFilePath = Path.Combine(basePath, "state.json");
        }

        public void UpdateState(BackupState state)
        {
            state.LastActionTime = DateTime.Now;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(state, options);
            File.WriteAllText(stateFilePath, json);
        }
    }
}