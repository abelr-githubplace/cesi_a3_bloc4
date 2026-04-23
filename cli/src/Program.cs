using System.Globalization;
using EasySave.lang;
using EasyLog;
using SaveManager;

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
        public Command? Command { get; init; }
    }


    class Program
    {
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
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            var logger = Logger.Get("./save.log");
            var stateManager = StateManager.StateManager.Get("./state.json");
            var config = new SaveManager.Config { Logger = logger, StateManager = stateManager };

            List<SaveInfo> saveInfos = stateManager.GetSaves();

            Parser.ParsedCommand input_command = Parser.Parse(args);

            ProgramCommand command = new ProgramCommand { Action = input_command.Action };
            if (input_command.Action == ProgramAction.SaveAction && input_command.SaveIds != null) {
                SaveInfo[] saves = App.SaveInfosContext(input_command.SaveIds, saveInfos);
                command = new ProgramCommand
                {
                    Action = ProgramAction.SaveAction,
                    Command = new Command { SaveAction = SaveManager.Action.Save, Saves = saves, SaveType = SaveType.Complete }
                };
            }

            if (command.Action == ProgramAction.InteractiveMode) command = App.MainMenu(saveInfos);

            switch (command.Action)
            {
                case ProgramAction.Help: Console.WriteLine(_help); break;
                case ProgramAction.Version: Console.WriteLine(_version); break;
                case ProgramAction.SaveAction: Execute(command.Command, config); break;
            }
        }

        static void Execute(Command command, Config config)
        {
            Console.WriteLine($"\n--- {Messages.Saving} ---");

            var progresses = new List<Saver.Progress>();
            var bars = new List<ProgressBar>();

            for (int i = 0; i< command.Saves.Count(); i++)
            {
                Saver.Progress progress = new Saver.Progress();
                var bar = new ProgressBar(command.Saves[i].SaveName, progress);
            }

            bool success = SaveManager.SaveManager.Execute(command, progresses.ToArray(), config);

            var end_message = success ? $"{Messages.SaveFailed}" : $"{Messages.SaveSuccess}";
            Console.WriteLine($"\n--- {end_message} ---");
            Console.ReadKey();
        }
    }
}