using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using State;

namespace Save
{
	public enum Action { Save }
	public enum SaveType { Complete, Differential }

	public record Command
	{
		public Action SaveAction { get; init; }
		public SaveInfo[] Saves { get; init; }
		public SaveType? SaveType { get; init; }
	}

	public record SaveInfo
	{
		public string SaveName { get; init; }
		public string SourcePath { get; init; }
		public string DestinationPath { get; init; }
	}

	public class SaveManager
	{
		public static void Execute(Command command, List<Progress> progresses)
		{
			switch (command.SaveAction)
			{
				case Action.Save: Save(command.Saves, command.SaveType, progresses); break;
				default: return;
			}
		}

		private static Saver? InitSaver(SaveInfo save, SaveType type)
		{
			switch (type)
			{
				case SaveType.Complete: return CompleteSaver(save);
                case SaveType.Differential: return DifferentialSaver(save);
				default: return null;
            }
		}

        public static void Save(List<SaveInfo> saves, SaveType saveType, List<Progress> progresses)
		{
			foreach (var save in saves) InitSaver(save.SourcePath, save.DestinationPath, saveType).StartSave();
		}
    }
}