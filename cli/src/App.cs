using System.Globalization;
using EasySave.lang;
using EasyLog;
using Config;
using SaveManager;
using Sanitize;

namespace EasySaveConsole
{
    public class App
    {
        private static void Clear() {
            try { Console.Clear(); } catch (IOException) { }
        }

        public static ProgramCommand MainMenu(List<SaveManager.SaveInfo> previous_saves)
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.MainMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.MainMenuCompleteSave}\n" +
                    $"<2> {Messages.MainMenuDifferentialSave}\n" +
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
                            SaveInfo[] saves_1 = SaveMenu(previous_saves);
                            Clear();
                            return new ProgramCommand
                            {
                                Action = ProgramAction.CompleteSave,
                                Command = new Command { SaveAction = SaveManager.Action.CompleteSave, Saves = saves_1 }
                            };
                        case ConsoleKey.D2:
                            SaveInfo[] saves_2 = SaveMenu(previous_saves);
                            Clear();
                            return new ProgramCommand
                            {
                                Action = ProgramAction.DifferentialSave,
                                Command = new Command { SaveAction = SaveManager.Action.DifferentialSave, Saves = saves_2 }
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
                bool parse_fail = true;

                while (parse_fail || id < 1 || id > 5)
                {
                    Console.Write($"\n[{Messages.InvalidSaveIndex}]\n> ");
                    string? input = Console.ReadLine();
                    parse_fail = !int.TryParse(input, out id);
                }

                int index = id - 1;
                Clear();
                Console.WriteLine($"[{Messages.SaveInfosMenuTitle} {id}]\n");

                SaveInfo saveInfo;
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

                    saveInfo = new SaveInfo { SaveId = Guid.NewGuid(), SaveName = name.Trim(), SourcePath = src.Trim(), DestinationPath = dst.Trim() };
                    saveInfos.Add(saveInfo);
                }
                else
                {
                    saveInfo = saveInfos[index];
                }

                parsedSaveInfos.Add(saveInfo);
            }
            return [..parsedSaveInfos];
        }

        private static SaveInfo[] SaveMenu(List<SaveInfo> saveInfos)
        {
            Clear();
            Console.WriteLine($"[{Messages.SaveMenuTitle}]\n{Messages.SaveMenuDetails}");
            for (int i = 0; i < saveInfos.Count; i++) Console.WriteLine($"<{i + 1}> {saveInfos[i].SaveName}");
            Console.WriteLine();

            string? input = null;
            while (string.IsNullOrWhiteSpace(input)) input = Console.ReadLine();
            var saveIds = Parser.ParseArguments(input);
            return SaveInfosContext(saveIds, saveInfos);
        }

        private static void OptionMenu()
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.OptionMenuTitle}]\n" +
                    "\n" +
                    $"<1> {Messages.OptionMenuLanguage}\n" +
                    $"<2> {Messages.OptionMenuLogFormat}\n" +
                    $"<3> {Messages.OptionMenuLogOutput}\t\t -> {ConfigManager.Get().GetLogOutput()}\n" +
                    $"<4> {Messages.OptionMenuStateOutput}\t\t -> {ConfigManager.Get().GetStateOutput()}\n" +
                    $"<5> {Messages.OptionMenuBusinessSoftware}\n" +
                    $"<6> {Messages.OptionMenuExtensionsToEncrypt}\n" +
                    "\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    string? output;
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: LanguageMenu(); reload = true; break;
                        case ConsoleKey.D2: LogFormatMenu(); reload = true; break;
                        case ConsoleKey.D3:
                            Console.Write($"{Messages.OptionMenuLogOutputQuestion} > ");
                            output = PathSanitizer.Sanitize(Console.ReadLine());
                            if (!string.IsNullOrEmpty(output)) ConfigManager.Get().ModifyLogOutput(output);
                            reload = true;
                            break;
                        case ConsoleKey.D4:
                            Console.Write($"{Messages.OptionMenuStateOutputQuestion} > ");
                            output = PathSanitizer.Sanitize(Console.ReadLine());
                            if (!string.IsNullOrEmpty(output)) ConfigManager.Get().ModifyStateOutput(output);
                            reload = true;
                            break;
                        case ConsoleKey.D5: BusinessSoftwareMenu(); reload = true; break;
                        case ConsoleKey.D6: ExtensionsToEncryptMenu(); reload = true; break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }

        private static void BusinessSoftwareMenu()
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.BusinessSoftwareMenuTitle}]\n");

                var watched = ConfigManager.Get().GetBusinessSoftwares();
                Console.WriteLine($"{Messages.BusinessSoftwareMenuList}");
                if (watched.Count == 0) Console.WriteLine($"\t{Messages.EmptyList}");
                else for (int i = 0; i < watched.Count; i++) Console.WriteLine($"\t- {watched[i]}");

                Console.WriteLine($"\n" +
                    $"<1> {Messages.BusinessSoftwareMenuAdd}\n" +
                    $"<2> {Messages.BusinessSoftwareMenuRemove}\n" +
                    $"\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1:
                            Console.Write($"\n{Messages.BusinessSoftwareAskAdd}\n> ");
                            string? toAdd = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(toAdd)) ConfigManager.Get().AddBusinessSoftwares([toAdd]);
                            reload = true; break;
                        case ConsoleKey.D2:
                            Console.Write($"\n{Messages.BusinessSoftwareAskRemove}\n> ");
                            string? toRemove = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(toRemove)) ConfigManager.Get().RemoveBusinessSoftwares([toRemove]);
                            reload = true; break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }

        private static void ExtensionsToEncryptMenu()
        {
            while (true)
            {
                Clear();
                Console.WriteLine($"[{Messages.ExtensionsToEncryptMenuTitle}]\n");

                var watched = ConfigManager.Get().GetEncryptionExtensions();
                Console.WriteLine($"{Messages.ExtensionsToEncryptMenuList}");
                if (watched.Count == 0) Console.WriteLine($"\t{Messages.EmptyList}");
                else for (int i = 0; i < watched.Count; i++) Console.WriteLine($"\t- {watched[i]}");

                Console.WriteLine($"\n" +
                    $"<1> {Messages.ExtensionsToEncryptMenuAdd}\n" +
                    $"<2> {Messages.ExtensionsToEncryptMenuRemove}\n" +
                    $"\n" +
                    $"<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1:
                            Console.Write($"\n{Messages.ExtensionsToEncryptAskAdd}\n> ");
                            string? toAdd = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(toAdd)) ConfigManager.Get().AddEncryptionExtensions([toAdd]);
                            reload = true; break;
                        case ConsoleKey.D2:
                            Console.Write($"\n{Messages.ExtensionsToEncryptAskRemove}\n> ");
                            string? toRemove = Console.ReadLine();
                            if (!string.IsNullOrWhiteSpace(toRemove)) ConfigManager.Get().RemoveEncryptionExtensions([toRemove]);
                            reload = true; break;
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
            string[] langs = [ "en-US", "en-GB", "fr-FR" ];
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
                        case ConsoleKey.D1:
                            Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[0]);
                            ConfigManager.Get().SetLanguage(langs[0]);
                            reload = true;
                            break;
                        case ConsoleKey.D2:
                            Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[1]);
                            ConfigManager.Get().SetLanguage(langs[1]);
                            reload = true;
                            break;
                        case ConsoleKey.D3:
                            Thread.CurrentThread.CurrentUICulture = new CultureInfo(langs[2]);
                            ConfigManager.Get().SetLanguage(langs[2]);
                            reload = true;
                            break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }

        private static string SelectedLogFormat(string format)
        {
            var config = ConfigManager.Get();
            if (
                (format == "JSON" && config.GetLogFormatConfig() == LogFormat.JSON) ||
                (format == "XML" && config.GetLogFormatConfig() == LogFormat.XML) ||
                (format == "Text" && config.GetLogFormatConfig() == LogFormat.Text)
               )
            {
                return $"_{format}_";
            }
            else return format;
        }

        private static void LogFormatMenu()
        {
            LogFormat[] formats = [ LogFormat.JSON , LogFormat.XML, LogFormat.Text ];
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"[{Messages.LogFormatMenuTitle}]\n\n");
                for (int i = 0; i < formats.Length; i++) Console.WriteLine($"<{i + 1}> {SelectedLogFormat(formats[i].ToString())}");
                Console.WriteLine($"\n<Esc> {Messages.ReturnToPreviousMenu}");

                bool reload = false;
                while (!reload)
                {
                    var key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.D1: ConfigManager.Get().SetLogFormat(formats[0]); reload = true; break;
                        case ConsoleKey.D2: ConfigManager.Get().SetLogFormat(formats[1]); reload = true; break;
                        case ConsoleKey.D3: ConfigManager.Get().SetLogFormat(formats[2]); reload = true; break;
                        case ConsoleKey.Escape: return;
                        default: break;
                    }
                }
            }
        }
    }
}