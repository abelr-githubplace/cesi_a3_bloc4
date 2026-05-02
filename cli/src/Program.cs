using System.Globalization;
using EasySave.lang;
using EasyLog;
using System.Data;

namespace EasySaveConsole
{
    public enum ProgramAction
    {
        CompleteSave, DifferentialSave,
        LogFormatXML, LogFormatJSON, LogFormatText,
        InteractiveMode,
        Version,
        Help,
        Exit
    }

    public record ProgramCommand
    {
        public required ProgramAction Action { get; init; }
        public SaveManager.Command? Command { get; init; }
    }


    class Program
    {
        private const string _default_lang = "en-US";
        private const string _version = "EasySave v1.1";
        private const string _help = "Usage: EasySave.exe [OPTIONS] [ARGUMENTS]\n" +
            "\n" +
            "OPTIONS:\n" +
            "      --save, --complete   Complete save (default)\n" +
            "      --differential       Differential save\n" +
            "      --log=[format]       Specify the log output format. Can be JSON (default), XML or Text\n" +
            "  -i, --interactive        Launch in interactive mode\n" +
            "  -h, --help               Display this help message\n" +
            "  -v, --version            Display the version\n" +
            "\n" +
            "ARGUMENTS:\n" +
            "  N                        One single save (from 1 to 5 included)\n" +
            "  N-M                      Range of saves (from N to M)\n" +
            "  N;M                      Multiple saves (N and M)";

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(_default_lang);

            var logger = Logger.Get("./save.log");
            var stateManager = StateManager.StateManager.Get("./state.json");
            List<SaveManager.SaveInfo> saveInfos = stateManager.GetSaves();

            Parser.ParsedCommand input_command = Parser.Parse(args);
            var config = new SaveManager.Config { Logger = logger, StateManager = stateManager, LogFormat = input_command.Format };

            if (input_command.Action == ProgramAction.InteractiveMode)
            {
                while (true)
                {
                    (SaveManager.Config new_config, ProgramCommand command) = App.MainMenu(config, saveInfos);
                    if (command.Action == ProgramAction.Exit) break;
                    switch (command.Action)
                    {
                        case ProgramAction.Help: Console.WriteLine(_help); break;
                        case ProgramAction.Version: Console.WriteLine(_version); break;
                        case ProgramAction.CompleteSave: Execute(command.Command, new_config); break;
                        case ProgramAction.DifferentialSave: Execute(command.Command, new_config); break;
                    }
                }
                return;
            }

            ProgramCommand argCommand = new ProgramCommand { Action = input_command.Action };
            if (
                (input_command.Action == ProgramAction.CompleteSave || input_command.Action == ProgramAction.DifferentialSave)
                && input_command.SaveIds != null
               )
            {
                SaveManager.SaveInfo[] saves = App.SaveInfosContext(input_command.SaveIds, saveInfos);
                argCommand = new ProgramCommand
                {
                    Action = ProgramAction.CompleteSave,
                    Command = new SaveManager.Command { SaveAction = SaveManager.Action.CompleteSave, Saves = saves }
                };
            }

            switch (argCommand.Action)
            {
                case ProgramAction.Help: Console.WriteLine(_help); break;
                case ProgramAction.Version: Console.WriteLine(_version); break;
                case ProgramAction.CompleteSave: Execute(argCommand.Command, config); break;
                case ProgramAction.DifferentialSave: Execute(argCommand.Command, config); break;
                default: break;
            }
        }

        static void Execute(SaveManager.Command command, SaveManager.Config config)
        {
            try { Console.Clear(); } catch { }
            Console.WriteLine($"\n--- {Messages.Saving} ---");

            var progresses = new List<Saver.Progress>();
            var bars = new List<ProgressBar>();

            for (int i = 0; i < command.Saves.Length; i++)
            {
                Saver.Progress progress = new Saver.Progress();
                var bar = new ProgressBar(command.Saves[i].SaveName, progress);
                progresses.Add(progress);
                bars.Add(bar);
            }
            bool success = SaveManager.SaveManager.Execute(command, progresses.ToArray(), config);

            var end_message = success ? $"{Messages.SaveSuccess}" : $"{Messages.SaveFailed}";
            Console.WriteLine($"\n--- {end_message} ---");
            if (!Console.IsInputRedirected) Console.ReadKey();
        }
    }
}