using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyLog
{
    public sealed class StateManager
    {
        private static StateManager s_instance;
        private static readonly object s_lock = new object();

        private readonly string _outputFile;

        private StateManager(string outputFile)
        {
            _outputFile = outputFile;
        }

        public static StateManager Get(string outputFile)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new StateManager(outputFile);
                    }
                }
            }
            return s_instance;
        }

        // Validation du format UNC
        private static void ValidateUncPath(string path, string propertyName)
        {
            if (!string.IsNullOrEmpty(path) && !path.StartsWith(@"\\"))
            {
                throw new ArgumentException(
                    $"{propertyName} must be a valid UNC path (\\\\SERVER\\share\\file)"
                );
            }
        }

        // DTO utilisé pour la sérialisation JSON
        private class StateEntry
        {
            public string BackupName { get; set; }
            public DateTime LastActionTime { get; set; }
            public string Status { get; set; }
            public int TotalFiles { get; set; }
            public long TotalSize { get; set; }

            // Champs optionnels (Active uniquement)
            public int? FilesRemaining { get; set; }
            public long? SizeRemaining { get; set; }
            public int? Progress { get; set; }
            public string CurrentSourceFile { get; set; }
            public string CurrentTargetFile { get; set; }
        }

        public void UpdateState(BackupState state)
        {
            // Validation UNC mais uniquement si champs utilisés
            ValidateUncPath(state.CurrentSourceFile, nameof(state.CurrentSourceFile));
            ValidateUncPath(state.CurrentTargetFile, nameof(state.CurrentTargetFile));

            List<StateEntry> states;

            // Lire + désérialiser
            if (File.Exists(_outputFile))
            {
                string json = File.ReadAllText(_outputFile);
                states = JsonSerializer.Deserialize<List<StateEntry>>(json)
                         ?? new List<StateEntry>();
            }
            else
            {
                states = new List<StateEntry>();
            }

            // Supprimer l’état existant
            states.RemoveAll(s => s.BackupName == state.BackupName);

            // Construire le nouvel état
            StateEntry newState;

            if (state.Status == "Inactive")
            {
                //  JSON RÉDUIT
                newState = new StateEntry
                {
                    BackupName = state.BackupName,
                    LastActionTime = DateTime.Now,
                    Status = "Inactive",
                    TotalFiles = state.TotalFiles,
                    TotalSize = state.TotalSize
                };
            }
            else
            {
                //JSON 
                newState = new StateEntry
                {
                    BackupName = state.BackupName,
                    LastActionTime = DateTime.Now,
                    Status = "Active",
                    TotalFiles = state.TotalFiles,
                    TotalSize = state.TotalSize,
                    FilesRemaining = state.FilesRemaining,
                    SizeRemaining = state.SizeRemaining,
                    Progress = state.Progress,
                    CurrentSourceFile = state.CurrentSourceFile,
                    CurrentTargetFile = state.CurrentTargetFile
                };
            }

            // Ajouter
            states.Add(newState);

            // Sérialiser + écrire
            File.WriteAllText(
                _outputFile,
                JsonSerializer.Serialize(states, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                })
            );
        }
    }
}