using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

        //  Vérification UNC
        private static void ValidateUncPath(string path, string propertyName)
        {
            if (!string.IsNullOrEmpty(path) && !path.StartsWith(@"\\"))
            {
                throw new ArgumentException(
                    $"{propertyName} must be a valid UNC path (\\\\SERVER\\share\\file)"
                );
            }
        }

        public void UpdateState(BackupState state)
        {
            // Validation des chemins UNC si présents
            ValidateUncPath(state.CurrentSourceFile, nameof(state.CurrentSourceFile));
            ValidateUncPath(state.CurrentTargetFile, nameof(state.CurrentTargetFile));

            List<JsonElement> states = new();

            if (File.Exists(_outputFile))
            {
                string json = File.ReadAllText(_outputFile);
                states = JsonSerializer.Deserialize<List<JsonElement>>(json)
                         ?? new List<JsonElement>();
            }

            states = states
                .Where(s =>
                    !s.TryGetProperty("BackupName", out var name) ||
                    name.GetString() != state.BackupName)
                .ToList();

            object stateToWrite;

            if (state.Status == "Inactive")
            {
                stateToWrite = new
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
                stateToWrite = new
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

            states.Add(
                JsonSerializer.Deserialize<JsonElement>(
                    JsonSerializer.Serialize(stateToWrite))
            );

            File.WriteAllText(
                _outputFile,
                JsonSerializer.Serialize(states, new JsonSerializerOptions
                {
                    WriteIndented = true
                })
            );
        }
    }
}