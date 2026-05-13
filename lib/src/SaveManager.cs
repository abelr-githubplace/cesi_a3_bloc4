using Result;
using Save;
using Restore;
using Config;

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
	}

	public static class SaveManager
	{
		public static Result<Empty, IEnumerable<string>> Execute(Command command, Progress.Progress[] progresses)
		{
			return command.SaveAction switch
			{
				Action.CompleteSave or Action.DifferentialSave => Save(command.Saves, command.SaveAction, progresses),
				Action.Restore => Restore(command.Saves, command.SaveAction, progresses),
                Action.Delete => Delete(command.Saves),
                _ => new Err<Empty, IEnumerable<string>>(["Unsupported command, failed to execute"])
			};
		}

		private static Result<Empty, IEnumerable<string>> Save(SaveInfo[] saves, Action saveAction, Progress.Progress[] progresses)
		{
            if (saves.Length != progresses.Length) return new Err<Empty, IEnumerable<string>>(["Number of progress instances do not match the number of saves"]);
			List<Saver> savers = [];
			for (int i = 0; i < saves.Length; i++) savers.Add(new Saver(saves[i], saveAction, progresses[i]));
			foreach (var saver in savers) saver.Start();
			return Empty.EmptyOk<IEnumerable<string>>();
		}

		private static Result<Empty, IEnumerable<string>> Restore(SaveInfo[] saves, Action saveAction, Progress.Progress[] progresses)
		{
            if (saves.Length != progresses.Length) return new Err<Empty, IEnumerable<string>>(["Number of progress instances do not match the number of saves"]);

            List<Restorer> restorers = [];
			for (int i = 0; i < saves.Length; i++) restorers.Add(new Restorer(saves[i], saveAction, progresses[i]));
			foreach (var r in restorers) r.Start();
            return Empty.EmptyOk<IEnumerable<string>>();
        }

		private static Result<Empty, IEnumerable<string>> Delete(SaveInfo[] saves)
		{
			List<string> errors = [];
			foreach (var save in saves)
			{
				bool removed = ConfigManager.Get().State.Delete(save.SaveId);
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
