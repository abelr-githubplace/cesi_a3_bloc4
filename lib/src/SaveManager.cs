
using Saver;

namespace SaveManager
{
	public enum Action { Save, Delete, Restore }
	public enum SaveType { Complete, Differential }

	public record Config
	{
		public required EasyLog.Logger Logger { get; init; }
        public required StateManager.StateManager StateManager { get; init; }
    }

	public record Command
	{
		public required Action SaveAction { get; init; }
		public required SaveInfo[] Saves { get; init; }
		public SaveType? SaveType { get; init; }
		// Delete-only: when true, also wipe DestinationPath on disk.
		// Defaults to false so a delete is reversible (state-only by default).
		public bool DeleteFiles { get; init; }
	}

	public record SaveInfo
	{
		public required uint SaveId { get; init; }
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
				case Action.Save: return Save(command.Saves, command.SaveType, progresses, config);
				case Action.Delete: return Delete(command.Saves, command.DeleteFiles, config);
				case Action.Restore: return Restore(command.Saves, progresses);
				default: return false;
			}
		}

		private static bool Save(Command command, Progress[] progresses, Config config)
		{
            if (saves.Length != progresses.Length) return false;
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

		private static bool Delete(SaveInfo[] saves, bool deleteFiles, Config config)
		{
			bool allOk = true;
			foreach (var save in saves)
			{
				bool removed = config.StateManager.Delete(save.SaveName);
				if (!removed) allOk = false;

				if (deleteFiles)
				{
					try
					{
						if (File.Exists(save.DestinationPath)) File.Delete(save.DestinationPath);
						else if (Directory.Exists(save.DestinationPath)) Directory.Delete(save.DestinationPath, true);
					}
					catch (IOException) { allOk = false; }
					catch (UnauthorizedAccessException) { allOk = false; }
				}
			}
			return allOk;
		}

		private static bool Restore(SaveInfo[] saves, Progress[] progresses)
		{
			if (saves.Length != progresses.Length) return false;
			var restorers = new List<Saver.Restorer>();
			for (int i = 0; i < saves.Length; i++)
				restorers.Add(new Saver.Restorer(saves[i], progresses[i]));
			foreach (var r in restorers) r.Start();
			return true;
		}

		// Non-blocking factory for callers that need control handles (Pause/Resume/Stop)
		// while saves run on a background thread — the future GUI is the intended user.
		public static Saver.Saver[] CreateSavers(SaveInfo[] saves, SaveType? saveType, Progress[] progresses, Config config)
		{
			if (saves.Length != progresses.Length) return Array.Empty<Saver.Saver>();
			var savers = new Saver.Saver[saves.Length];
			for (int i = 0; i < saves.Length; i++)
				savers[i] = new Saver.Saver(saves[i], saveType ?? SaveType.Complete, progresses[i], config);
			return savers;
		}

		public static Saver.Restorer[] CreateRestorers(SaveInfo[] saves, Progress[] progresses)
		{
			if (saves.Length != progresses.Length) return Array.Empty<Saver.Restorer>();
			var restorers = new Saver.Restorer[saves.Length];
			for (int i = 0; i < saves.Length; i++)
				restorers[i] = new Saver.Restorer(saves[i], progresses[i]);
			return restorers;
		}
    }
}
