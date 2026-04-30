using Saver;

namespace SaveManager
{
	public enum Action { CompleteSave, DifferentialSave }

	public record Config
	{
		public required EasyLog.LogFormat LogFormat { get; init; }
		public required EasyLog.Logger Logger { get; init; }
        public required StateManager.StateManager StateManager { get; init; }
        public AppConfig.AppConfig? AppConfig { get; init; }
    }

	public record Command
	{
		public required Action SaveAction { get; init; }
		public required SaveInfo[] Saves { get; init; }
	}

	public record SaveInfo
	{
		public required Guid SaveId { get; init; }
		public required string SaveName { get; init; }
		public required string SourcePath { get; init; }
		public required string DestinationPath { get; init; }
	}

	public class SaveManager
	{
		public static bool Execute(Command command, Progress[] progresses, Config config)
		{
			switch (command.SaveAction)
			{
				case Action.CompleteSave: return Save(command, progresses, config);
				default: return false;
			}
		}

        private static bool Save(Command command, Progress[] progresses, Config config)
		{
            if (saves.Length != progresses.Length) return false;

            // Block Complete saves when business software is running; Differential saves are
            // explicitly allowed to continue per spec.
            SaveType effectiveType = saveType ?? SaveType.Complete;
            if (effectiveType == SaveType.Complete && IsBusinessSoftwareRunning(config))
                return false;

			var savers = new List<Saver.Saver>();
			for (int i = 0; i < saves.Length; i++)
				savers.Add(new Saver.Saver(saves[i], effectiveType, progresses[i], config));
			foreach (var saver in savers) saver.Start();
			return true;
		}

        public static bool IsBusinessSoftwareRunning(Config config)
        {
            if (config.AppConfig == null) return false;
            var watched = config.AppConfig.GetBusinessSoftware();
            if (watched.Count == 0) return false;
            return BusinessSoftwareMonitor.BusinessSoftwareMonitor.IsAnyRunning(watched);
        }
    }
}