using System.Globalization;
using EasySave.lang;
using EasyLog;
using Observer;

namespace EasySaveConsole
{
    public enum ProgramAction
    {
        SaveAction,
        ConfigChange,
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
        private const string _version = "EasySave v1.0.0";
        private const string _help = "Usage: EasySave.exe [OPTIONS] [ARGUMENTS]\n" +
            "\n" +
            "OPTIONS:\n" +
            "      --save      Save (default)\n" +
            "  -h, --help      Display this help message\n" +
            "  -v, --version   Version\n" +
            "\n" +
            "ARGUMENTS:\n" +
            "  N               One single save (from 1 to 5 included)\n" +
            "  N-M             Range of saves (from N to M)\n" +
            "  N;M             Multiple saves (N and M)";

        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(_default_lang);

            var logger = Logger.Get("./save.log");
            var stateManager = StateManager.StateManager.Get("./state.json");
            var config = new SaveManager.Config { Logger = logger, StateManager = stateManager };

            List<SaveManager.SaveInfo> saveInfos = stateManager.GetSaves();

            Parser.ParsedCommand input_command = Parser.Parse(args);

            // Interactive mode: loop on the main menu until the user explicitly exits (Esc)
            if (input_command.Action == ProgramAction.InteractiveMode)
            {
                while (true)
                {
                    ProgramCommand command = App.MainMenu(saveInfos);
                    if (command.Action == ProgramAction.Exit) break;
                    switch (command.Action)
                    {
                        case ProgramAction.Help: Console.WriteLine(_help); break;
                        case ProgramAction.Version: Console.WriteLine(_version); break;
                        case ProgramAction.SaveAction: Execute(command.Command, config); break;
                    }
                }
                return;
            }

            // Non-interactive mode (CLI args): run once and exit
            ProgramCommand argCommand = new ProgramCommand { Action = input_command.Action };
            if (input_command.Action == ProgramAction.SaveAction && input_command.SaveIds != null)
            {
                SaveManager.SaveInfo[] saves = App.SaveInfosContext(input_command.SaveIds, saveInfos);
                argCommand = new ProgramCommand
                {
                    Action = ProgramAction.SaveAction,
                    Command = new SaveManager.Command
                    {
                        SaveAction = SaveManager.Action.Save,
                        Saves = saves,
                        SaveType = input_command.SaveType
                    }
                };
            }

            switch (argCommand.Action)
            {
                case ProgramAction.Help: Console.WriteLine(_help); break;
                case ProgramAction.Version: Console.WriteLine(_version); break;
                case ProgramAction.SaveAction: Execute(argCommand.Command, config); break;
            }
        }

        static void Execute(SaveManager.Command command, SaveManager.Config config)
        {
            Console.WriteLine($"\n--- {Messages.Saving} ---\n");

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