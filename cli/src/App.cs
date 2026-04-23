using System.Globalization;
using EasySave.lang;
using SaveManager;

namespace EasySaveConsole
{
    public class App
    {
        public static ProgramCommand MainMenu(List<SaveManager.SaveInfo> previous_saves)
        {
            Console.Clear();
            Console.WriteLine($"[{Messages.MainMenuTitle}]\n" +
                "\n" +
                $"<1> {Messages.MainMenuSave}\n" +
                "\n" +
                $"<O> {Messages.MainMenuOptions}\n" +
                $"<Esc> {Messages.MainMenuExit}");

            while (true)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1:
                        SaveInfo[] saves = SaveMenu(previous_saves);
                        return new ProgramCommand
                        {
                            Action = ProgramAction.SaveAction,
                            Command = new Command { SaveAction = SaveManager.Action.Save, Saves = saves, SaveType = SaveTypeMenu() }
                        };
                    case ConsoleKey.O: OptionMenu(); break;
                    case ConsoleKey.Escape: return new ProgramCommand { Action = ProgramAction.Exit };
                    default: break;
                }
            }
        }

        public static SaveInfo[] SaveInfosContext(List<int> saveIds, List<SaveInfo> saveInfos)
        {
            var parsedSaveInfos = new List<SaveInfo>();
            foreach (int i in saveIds)
            {
                SaveInfo? saveInfo = null;
                if (i >= saveInfos.Count())
                {
                    string? name = null;
                    string? src = null;
                    string? dst = null;
                    Console.Write($"\n{Messages.SaveMenuAskSaveName}\n> ");
                    while (string.IsNullOrWhiteSpace(name)) name = Console.ReadLine();
                    Console.Write($"\n{Messages.SaveMenuAskSaveSrc}\n> ");
                    while (string.IsNullOrWhiteSpace(src)) src = Console.ReadLine();
                    Console.Write($"\n{Messages.SaveMenuAskSaveDst}\n> ");
                    while (string.IsNullOrWhiteSpace(dst)) dst = Console.ReadLine();
                    saveInfo = new SaveInfo { SaveName = name.Trim(), SourcePath = src.Trim(), DestinationPath = dst.Trim() };
                    saveInfos.Add(saveInfo);
                }
                else saveInfo = saveInfos[i];
                parsedSaveInfos.Add(saveInfo);
            }
            return parsedSaveInfos.ToArray();
        }

        private static SaveInfo[] SaveMenu(List<SaveInfo> saveInfos)
        {
            Console.Clear();
            Console.WriteLine($"[{Messages.SaveMenuTitle}]\n{Messages.SaveMenuDetails}");
            for (int i = 0; i < saveInfos.Count(); i++) Console.WriteLine($"<{i}> {saveInfos[i].SaveName}");
            Console.WriteLine();

            string? input = null;
            while (string.IsNullOrWhiteSpace(input)) input = Console.ReadLine();
            var saveIds = Parser.ParseArguments(input);
            return SaveInfosContext(saveIds , saveInfos);
        }

        private static SaveType? SaveTypeMenu()
        {
            Console.Clear();
            Console.WriteLine($"[{Messages.SaveTypeMenuTitle}]\n" +
                "\n" +
                $"<1> {Messages.SaveTypeComplete}\n" +
                $"<2> {Messages.SaveTypeDifferential}\n" +
                "\n" +
                $"<Esc> {Messages.ReturnToPreviousMenu}");

            while (true)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1: return SaveManager.SaveType.Complete;
                    case ConsoleKey.D2: return SaveManager.SaveType.Differential;
                    case ConsoleKey.Escape: return null;
                    default: break;
                }
            }
        }

        private static void OptionMenu()
        {
            Console.Clear();
            Console.WriteLine($"[{Messages.OptionMenuTitle}]\n" +
                "\n" +
                $"<1> {Messages.OptionMenuLanguage}\n" +
                "\n" +
                $"<Esc> {Messages.ReturnToPreviousMenu}");

            while (true)
            {
                var key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.D1: LanguageMenu(); break;
                    case ConsoleKey.Escape: return;
                    default: break;
                }
            }
        }

        private static void LanguageMenu()
        {
            Console.Clear();
            Console.WriteLine($"[{Messages.LanguageMenuTitle}]\n" +
                "\n" +
                "<1> English (US)\n" +
                "<2> Français\n" +
                "\n" +
                $"<Esc> {Messages.ReturnToPreviousMenu}");

            while (true)
            {
                var choice = Console.ReadKey();
                switch (choice.Key)
                {
                    case ConsoleKey.D1: Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US"); break;
                    case ConsoleKey.D2: Thread.CurrentThread.CurrentUICulture = new CultureInfo("fr-FR"); break;
                    case ConsoleKey.Escape: return;
                    default: break;
                }
            }
        }
    }
}