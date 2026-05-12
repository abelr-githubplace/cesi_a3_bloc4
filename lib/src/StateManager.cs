using System.Text.Json;
using System.Text.Json.Serialization;

namespace State
{
    public enum Status { Active, Inactive, Paused }

    // One row per file currently being transferred. With V3 parallelism a
    // single Saver can have multiple files in flight at once, so we publish a
    // list rather than a single CurrentSourceFile/CurrentTargetFile pair.
    public record ActiveFileInfo
    {
        public required string Source { get; init; }
        public required string Target { get; init; }
    }

    public record ActiveStateInfo
    {
        public required int TotalFiles { get; init; }
        public required long TotalSize { get; init; }
        public required int FilesRemaining { get; init; }
        public required long SizeRemaining { get; init; }
        public required float Progress { get; init; }
        public required List<ActiveFileInfo> CurrentFiles { get; init; }
    }

    public record SaveState
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string SourcePath { get; init; }
        public required string DestinationPath { get; init; }
        public required DateTime LastActionTime { get; init; }
        public required Status Status { get; init; }
        public ActiveStateInfo? ActiveStateInfo { get; init; }
    }

    public sealed class StateManager
    {
        private static JsonSerializerOptions s_read_serializer = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        private static JsonSerializerOptions s_write_serializer = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
        };

        private static StateManager? s_instance;
        private static readonly object s_lock = new();

        private string _output;
        private readonly List<SaveState> _states;
        private static readonly object s_rwLock = new();

        private StateManager(string output)
        {
            _output = output;

            if (File.Exists(output))
            {
                string json = File.ReadAllText(output);
                if (string.IsNullOrEmpty(json)) _states = new List<SaveState>();
                else _states = JsonSerializer.Deserialize<List<SaveState>>(json, s_read_serializer)
                    ?? new List<SaveState>();
                return;
            }
            _states = new List<SaveState>();
            Write();
        }

        public static StateManager Get(string output)
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    s_instance ??= new StateManager(output);
                }
            }
            return s_instance;
        }

        public List<SaveManager.SaveInfo> GetSaves()
        {
            var saveInfos = new List<SaveManager.SaveInfo>();
            foreach (var state in _states)
            {
                saveInfos.Add(new SaveManager.SaveInfo {
                    SaveId = state.Id,
                    SaveName = state.Name,
                    SourcePath = state.SourcePath,
                    DestinationPath = state.DestinationPath
                });
            }
            return saveInfos;
        }

        public void ModifyOutput(string output)
        {
            lock(s_rwLock) { _output = output; }
        }

        public void Save(SaveState state)
        {
            lock (s_rwLock)
            {
                int existingIndex = _states.FindIndex(s => s.Id == state.Id);
                if (existingIndex >= 0) _states[existingIndex] = state;
                else _states.Add(state);
            }
            Write();
        }

        public bool Delete(Guid id)
        {
            lock (s_rwLock)
            {
                int existingIndex = _states.FindIndex(s => s.Id == id);
                if (existingIndex < 0) return false;
                _states.RemoveAt(existingIndex);
            }
            Write();
            return true;
        }

        public SaveState? Find(Guid id)
        {
            lock (s_rwLock) { return _states.Find(s => s.Id == id); }
        }

        private void Write()
        {
            lock(s_rwLock)
            {
                var json = JsonSerializer.Serialize(_states, s_write_serializer);
                File.WriteAllText(_output, json);
            }
        }
    }
}