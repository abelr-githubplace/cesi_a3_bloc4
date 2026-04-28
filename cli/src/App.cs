using EasySave.lang;
using SaveManager;
using System.ComponentModel.Design;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace EasySaveConsole
{
    public class App
    {
        private static void Clear() {
            try { Console.Clear(); } catch (System.IO.IOException) { }
        }

        public static ProgramCommand MainMenu(List<SaveManager.SaveInfo> previous_saves)
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.MainMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.MainMenuSave}\n" +
                    "\n" +
                    $"<O> {Messages.MainMenuOptions}\n" +
                    $"<Esc> {Messages.MainMenuExit}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.D1:
                            SaveInfo[] saves = SaveMenu(previous_saves);
                            Clear();
                            return new ProgramCommand
                            {
                                Action = ProgramAction.SaveAction,
                                Command = new Command { SaveAction = SaveManager.Action.Save, Saves = saves, SaveType = SaveTypeMenu() }
                            };
                        case ConsoleKey.O: OptionMenu(); reload = true; break;
                        case ConsoleKey.Escape: return new ProgramCommand { Action = ProgramAction.Exit };
                        default: break;
                    }
                }
            }
        }

        public static SaveInfo[] SaveInfosContext(List<int> saveIds, List<SaveInfo> saveInfos)
        {
            var parsedSaveInfos = new List<SaveInfo>();

            foreach (int item in saveIds)
            {
                int id = item;

                while (id < 1 || id > 5)
                {
                    Console.Write($"\n[{Messages.Invalid}]\n> ");
                    string? input = Console.ReadLine();
                    int.TryParse(input, out id);
                }

                int index = id - 1;
                Clear();
                Console.WriteLine($"[{Messages.SaveInfosMenuTitle} {id}]\n");

                SaveInfo? saveInfo = null;

                if (index >= saveInfos.Count)
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

                    saveInfo = new SaveInfo { SaveId = (uint)id, SaveName = name.Trim(), SourcePath = src.Trim(), DestinationPath = dst.Trim() };
                    saveInfos.Add(saveInfo);
                }
                else
                {
                    saveInfo = saveInfos[index];
                }

                parsedSaveInfos.Add(saveInfo);
            }
            return parsedSaveInfos.ToArray();
        }

        private static SaveInfo[] SaveMenu(List<SaveInfo> saveInfos)
        {
            Clear();
            Console.WriteLine($"[{Messages.SaveMenuTitle}]\n{Messages.SaveMenuDetails}");
            for (int i = 0; i < saveInfos.Count(); i++) Console.WriteLine($"<{i + 1}> {saveInfos[i].SaveName}");
            Console.WriteLine();

            string? input = null;
            while (string.IsNullOrWhiteSpace(input)) input = Console.ReadLine();
            var saveIds = Parser.ParseArguments(input);
            return SaveInfosContext(saveIds, saveInfos);
        }

        private static SaveType? SaveTypeMenu()
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.SaveTypeMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.SaveTypeComplete}\n" +
                    $"<2> {Messages.SaveTypeDifferential}\n" +
                    "\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: Clear(); return SaveType.Complete;
                        case ConsoleKey.D2: Clear(); return SaveType.Differential;
                        case ConsoleKey.Escape: return null;
                        default: break;
                    }
                }
            }
        }

        private static void OptionMenu()
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.OptionMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.OptionMenuLanguage}\n" +
                    "\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey();
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: LanguageMenu(); reload = true; break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }

        private static string SelectedLang(string lang)
        {
            if (lang == CultureInfo.CurrentUICulture.Name) return $"_{lang}_";
            else return lang;
        }

        private static void LanguageMenu()
        {
            string[] langs = { "en-US", "en-GB", "fr-FR" };
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.LanguageMenuTitle}]\n\n");
                for (int i = 0; i < langs.Length; i++) Console.WriteLine($"<{i + 1}> {SelectedLang(langs[i])}");
                Console.WriteLine($"\n<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var choice = Console.ReadKey();
                    switch (choice.Key)
                    {
                        case ConsoleKey.D1: Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[0]); reload = true; break;
                        case ConsoleKey.D2: Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[1]); reload = true; break;
                        case ConsoleKey.D3: Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[2]); reload = true; break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }
    }
}