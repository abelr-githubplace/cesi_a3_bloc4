using EasyLog;
using EasySave.lang;
using SaveManager;
using System.Globalization;

namespace EasySaveConsole
{
    public class App
    {
        private static void Clear() {
            try { Console.Clear(); } catch (IOException) { }
        }

        public static (Config, ProgramCommand) MainMenu(Config config, List<SaveInfo> previous_saves)
        {
            var new_config = config;
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
                            return (new_config, new ProgramCommand
                            {
                                Action = ProgramAction.SaveAction,
                                Command = new Command { SaveAction = SaveManager.Action.Save, Saves = saves, SaveType = SaveTypeMenu() }
                            });
                        case ConsoleKey.O: new_config = OptionMenu(new_config); reload = true; break;
                        case ConsoleKey.Escape: return (new_config, new ProgramCommand { Action = ProgramAction.Exit });
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
                    Console.Write($"\n[{Messages.InvalidSaveIndex}]\n> ");
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

        private static Config OptionMenu(Config config)
        {
            var new_config = config;
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.OptionMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.OptionMenuLanguage}\n" +
                    $"<2> {Messages.OptionMenuLogFormat}\n" +
                    "\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: LanguageMenu(); reload = true; break;
                        case ConsoleKey.D2: new_config = LogFormatMenu(new_config); reload = true; break;
                        case ConsoleKey.Escape: return new_config;
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

        private static string SelectedLogFormat(Config config, string format)
        {
            if (
                (format == "JSON" && config.LogFormat == LogFormat.JSON) ||
                (format == "XML" && config.LogFormat == LogFormat.XML) ||
                (format == "Text" && config.LogFormat == LogFormat.Text)
               )
            {
                return $"_{format}_";
            }
            else return format;
        }

        private static Config LogFormatMenu(Config config)
        {
            var new_config = config;
            LogFormat[] formats = { LogFormat.JSON , LogFormat.XML, LogFormat.Text };
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"[{Messages.LogFormatMenuTitle}]\n\n");
                for (int i = 0; i < formats.Length; i++) Console.WriteLine($"<{i + 1}> {SelectedLogFormat(new_config, formats[i].ToString())}");
                Console.WriteLine($"\n<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: new_config = config with { LogFormat = formats[0] }; reload = true; break;
                        case ConsoleKey.D2: new_config = config with { LogFormat = formats[1] }; reload = true; break;
                        case ConsoleKey.D3: new_config = config with { LogFormat = formats[2] }; reload = true; break;
                        case ConsoleKey.Escape: return new_config;
                        default: break;
                    }
                }
            }
        }
    }
}