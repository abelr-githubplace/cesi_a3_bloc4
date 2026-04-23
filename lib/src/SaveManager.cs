using Saver;

namespace SaveManager
{
	public enum Action { Save }
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
	}

	public record SaveInfo
	{
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
				default: return false;
			}
		}

        private static bool Save(SaveInfo[] saves, SaveType? saveType, Progress[] progresses, Config config)
		{
			if (saves.Length != progresses.Length) return false;
            for (int i = 0; i < saves.Count(); i++)
				new Saver.Saver(saves[i], saveType ?? SaveType.Complete, progresses[i], config).Start();
			return true;
		}
    }
}