using Saver;

namespace SaveManager
{
	public enum Action { CompleteSave, DifferentialSave }

	public record Config
	{
		public required EasyLog.LogFormat LogFormat { get; init; }
		public required EasyLog.Logger Logger { get; init; }
        public required StateManager.StateManager StateManager { get; init; }
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
            if (command.Saves.Length != progresses.Length) return false;
			var savers = new List<Saver.Saver>();
			for (int i = 0; i < command.Saves.Length; i++)
				savers.Add(new Saver.Saver(command.Saves[i], command.SaveAction, progresses[i], config));
			foreach (var saver in savers) saver.Start();
			return true;
		}
    }
}