using Saver;

namespace SaveManager
{
	public enum Action { Save, Delete, Restore }
	public enum SaveType { Complete, Differential }

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
		public SaveType? SaveType { get; init; }
		// Delete-only: when true, also wipe DestinationPath on disk.
		// Defaults to false so a delete is reversible (state-only by default).
		public bool DeleteFiles { get; init; }
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
				case Action.Save: return Save(command.Saves, command.SaveType, progresses, config);
				case Action.Delete: return Delete(command.Saves, command.DeleteFiles, config);
				case Action.Restore: return Restore(command.Saves, progresses, config);
				default: return false;
			}
		}

		private static bool Save(SaveInfo[] saves, SaveType? saveType, Progress[] progresses, Config config)
		{
            if (saves.Length != progresses.Length) return false;
            SaveType effectiveType = saveType ?? SaveType.Complete;
            // 2.0: business software detected => any save type is blocked.
            // The stop must be consigned in the daily log.
            if (IsBusinessSoftwareRunning(config))
            {
                LogBusinessSoftwareBlock(saves, config);
                return false;
            }

			var savers = new List<Saver.Saver>();
			for (int i = 0; i < saves.Length; i++)
			{
				// Synchronous in-process path: the caller doesn't need to control
				// these savers, so each one gets throwaway publishers it can
				// subscribe to. They're never fired, the worker just runs to
				// completion.
				var pauser = new SaveInterrupt.Pauser();
				var stopper = new SaveInterrupt.Stopper();
				savers.Add(new Saver.Saver(saves[i], effectiveType, progresses[i], config, pauser, stopper));
			}
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

        // Cahier des charges 2.0 : "L'arrêt doit être consigné dans le fichier log".
        // Emits one log entry per save that was refused / interrupted because a
        // watched business software was running. TransferTime is set to -1 so the
        // log line is recognizable as an error/event rather than a transfer.
        public static void LogBusinessSoftwareBlock(IEnumerable<SaveInfo> saves, Config config)
        {
            if (config.AppConfig == null) return;
            var running = BusinessSoftwareMonitor.BusinessSoftwareMonitor
                .GetRunningBusinessSoftware(config.AppConfig.GetBusinessSoftware());
            if (running.Count == 0) return;

            string detected = string.Join(",", running);
            foreach (var save in saves)
            {
                config.Logger.Log(
                    new EasyLog.LogInfo
                    {
                        DateTime = DateTime.Now,
                        SaveName = save.SaveName,
                        SourceFile = save.SourcePath,
                        DestinationFile = save.DestinationPath,
                        Action = "BUSINESS_SOFTWARE_STOP:" + detected,
                        FileSize = 0,
                        TransferTime = -1,
                    }.Format(config.LogFormat)
                );
            }
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

		private static bool Restore(SaveInfo[] saves, Progress[] progresses, Config config)
		{
			if (saves.Length != progresses.Length) return false;
			var restorers = new List<Saver.Restorer>();
			for (int i = 0; i < saves.Length; i++)
				restorers.Add(new Saver.Restorer(saves[i], progresses[i], config));
			foreach (var r in restorers) r.Start();
			return true;
		}

		// Non-blocking factory for callers (the GUI) that need control handles
		// to Pause/Resume/Stop a save while it runs on a background thread.
		// Each Saver is paired with its own Pauser and Stopper; the GUI keeps
		// them around and fires signals when the user clicks the buttons.
		public static (Saver.Saver, SaveInterrupt.Pauser, SaveInterrupt.Stopper)[] CreateSavers(SaveInfo[] saves, SaveType? saveType, Progress[] progresses, Config config)
		{
			if (saves.Length != progresses.Length) return Array.Empty<(Saver.Saver, SaveInterrupt.Pauser, SaveInterrupt.Stopper)>();
			var triples = new (Saver.Saver, SaveInterrupt.Pauser, SaveInterrupt.Stopper)[saves.Length];
			for (int i = 0; i < saves.Length; i++)
			{
				var pauser = new SaveInterrupt.Pauser();
				var stopper = new SaveInterrupt.Stopper();
				var saver = new Saver.Saver(saves[i], saveType ?? SaveType.Complete, progresses[i], config, pauser, stopper);
				triples[i] = (saver, pauser, stopper);
			}
			return triples;
		}

		public static Saver.Restorer[] CreateRestorers(SaveInfo[] saves, Progress[] progresses, Config? config = null)
		{
			if (saves.Length != progresses.Length) return Array.Empty<Saver.Restorer>();
			var restorers = new Saver.Restorer[saves.Length];
			for (int i = 0; i < saves.Length; i++)
				restorers[i] = new Saver.Restorer(saves[i], progresses[i], config);
			return restorers;
		}
    }
}
