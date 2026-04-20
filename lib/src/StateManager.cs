using System;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public sealed class StateManager
    {
        private static StateManager _instance;
        private static readonly object _lock = new object();

        private readonly string stateFilePath;

        // Constructeur privé
        private StateManager()
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EasySave"
            );

            Directory.CreateDirectory(basePath);
            stateFilePath = Path.Combine(basePath, "state.json");
        }

        // Accès thread‑safe
        public static StateManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new StateManager();
                        }
                    }
                }
                return _instance;
            }
        }

        public void UpdateState(BackupState state)
        {
            BackupState updatedState;

            if (state.Status == "Inactive")
            {
                // État final 
                updatedState = state with
                {
                    LastActionTime = DateTime.Now,
                    Progress = 100,
                    FilesRemaining = 0,
                    SizeRemaining = 0,
                    CurrentSourceFile = null,
                    CurrentTargetFile = null
                };
            }
            else
            {
                // État actif
                updatedState = state with
                {
                    LastActionTime = DateTime.Now
                };
            }

            File.WriteAllText(
                stateFilePath,
                JsonSerializer.Serialize(updatedState, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );
        }
    }
}