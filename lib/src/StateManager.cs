using System.Text.Json;
using System.Text.Json.Serialization;

namespace StateManager
{
    public enum Status { Active, Inactive }

    public record ActiveStateInfo
    {
        public required int TotalFiles { get; init; }
        public required long TotalSize { get; init; }
        public required int FilesRemaining { get; init; }
        public required long SizeRemaining { get; init; }
        public required float Progress { get; init; }
        public required string CurrentSourceFile { get; init; }
        public required string CurrentTargetFile { get; init; }
    }

    public record SaveState
    {
        public required string Name { get; init; }
        public required string SourcePath { get; init; }
        public required string DestinationPath { get; init; }
        public required DateTime LastActionTime { get; init; }
        public required Status Status { get; init; }
        public ActiveStateInfo? ActiveStateInfo { get; init; }
    }

    public sealed class StateManager
    {
        private static StateManager s_instance;
        private static readonly object s_lock = new object();

        private List<SaveState> _states;

        private readonly string _outputFile;

        private StateManager(string outputFile)
        {
            _outputFile = outputFile;

            if (File.Exists(outputFile))
            {
                string json = File.ReadAllText(outputFile);
                if (string.IsNullOrEmpty(json)) _states = new List<SaveState>();
                else _states = JsonSerializer.Deserialize<List<SaveState>>(json) ?? new List<SaveState>();
            }
            else
            {
                File.Create(outputFile);
                _states = new List<SaveState>();
            }
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

        public List<SaveManager.SaveInfo> GetSaves()
        {
            var saveInfos = new List<SaveManager.SaveInfo>();
            foreach (var state in _states)
            {
                saveInfos.Add(new SaveManager.SaveInfo { SaveName = state.Name, SourcePath = state.SourcePath, DestinationPath = state.DestinationPath });
            }
            return saveInfos;
        }

        public void Save(SaveState state)
        {
            _states.RemoveAll(s => s.Name == state.Name); // Maybe optimized
            _states.Add(state);
            Write();
        }

        private void Write()
        {
            File.WriteAllText(
                _outputFile,
                JsonSerializer.Serialize(
                    _states,
                    new JsonSerializerOptions { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }
                )
            );
        }
    }
}