using Result;
using Save;
using Restore;

namespace SaveManager
{
	public enum Action { CompleteSave, DifferentialSave, Delete, Restore }

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
		// Canonical values: "Complete" or "Differential" (resource key names, not translated).
		public string SaveType { get; init; } = "Complete";
	}

	public class SaveManager
	{
		public static Result<Empty, IEnumerable<string>> Execute(Command command, Progress.Progress[] progresses, Config.ConfigManager configManager)
		{
			return command.SaveAction switch
			{
				Action.CompleteSave or Action.DifferentialSave => Save(command.Saves, command.SaveAction, progresses, configManager),
				Action.Restore => Restore(command.Saves, command.SaveAction, progresses, configManager),
                Action.Delete => Delete(command.Saves, configManager),
                _ => new Err<Empty, IEnumerable<string>>(["Unsupported command, failed to execute"])
			};
		}

		private static Result<Empty, IEnumerable<string>> Save(SaveInfo[] saves, Action saveAction, Progress.Progress[] progresses, Config.ConfigManager configManager)
		{
            if (saves.Length != progresses.Length) return new Err<Empty, IEnumerable<string>>(["Number of progress instances do not match the number of saves"]);
			List<Saver> savers = [];
			for (int i = 0; i < saves.Length; i++) savers.Add(new Saver(saves[i], saveAction, progresses[i], configManager));
			foreach (var saver in savers) saver.Start(IsBusinessSoftwareRunning(configManager));
			return Empty.EmptyOk<IEnumerable<string>>();
		}

        private static bool IsBusinessSoftwareRunning(Config.ConfigManager configManager)
        {
            var watched = configManager.GetBusinessSoftwares();
            if (watched.Count == 0) return false;
            return BusinessSoftware.BusinessSoftwareMonitor.IsAnyRunning(watched);
        }

		private static Result<Empty, IEnumerable<string>> Restore(SaveInfo[] saves, Action saveAction, Progress.Progress[] progresses, Config.ConfigManager configManager)
		{
            if (saves.Length != progresses.Length) return new Err<Empty, IEnumerable<string>>(["Number of progress instances do not match the number of saves"]);

            List<Restorer> restorers = [];
			for (int i = 0; i < saves.Length; i++) restorers.Add(new Restorer(saves[i], saveAction, progresses[i], configManager));
			foreach (var r in restorers) r.Start();
            return Empty.EmptyOk<IEnumerable<string>>();
        }

		private static Result<Empty, IEnumerable<string>> Delete(SaveInfo[] saves, Config.ConfigManager configManager)
		{
			List<string> errors = [];
			foreach (var save in saves)
			{
				bool removed = configManager.State.Delete(save.SaveId);
				if (!removed)
				{
					errors.Add($"Failed to remove {save.SaveName} ({save.SaveId})");
					continue;
				}

				try
				{
					if (File.Exists(save.DestinationPath)) File.Delete(save.DestinationPath);
					else if (Directory.Exists(save.DestinationPath)) Directory.Delete(save.DestinationPath, true);
				}
				catch { errors.Add($"File or Directory removal failed for save {save.SaveName} ({save.SaveId})"); }
			}
			return errors.Count == 0 ? Empty.EmptyOk<IEnumerable<string>>() : new Err<Empty, IEnumerable<string>>(errors);
		}
    }
}
