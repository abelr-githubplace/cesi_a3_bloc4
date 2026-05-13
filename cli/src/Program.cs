using System.Globalization;
using EasySave.lang;
using EasyLog;
using Config;

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
        private const string _version = "EasySave v2.0";
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
            var config = ConfigManager.Get();
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(config.GetLanguageConfig());

            List<SaveManager.SaveInfo> saveInfos = config.State.GetSaves();
            Parser.ParsedCommand input_command = Parser.Parse(args); // FIXME: add config change

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
                        case ProgramAction.CompleteSave: Execute(command.Command); break;
                        case ProgramAction.DifferentialSave: Execute(command.Command); break;
                    }
                }
                return;
            }

            ProgramCommand argCommand = new() { Action = input_command.Action };
            if (
                (input_command.Action == ProgramAction.CompleteSave || input_command.Action == ProgramAction.DifferentialSave)
                && input_command.SaveIds != null
               )
            {
                SaveManager.SaveInfo[] saves = App.SaveInfosContext(input_command.SaveIds, saveInfos);
                argCommand = new ProgramCommand
                {
                    Action = input_command.Action,
                    Command = new SaveManager.Command {
                        SaveAction = input_command.Action == ProgramAction.DifferentialSave
                            ? SaveManager.Action.DifferentialSave
                            : SaveManager.Action.CompleteSave,
                        Saves = saves,
                    }
                };
            }

            switch (argCommand.Action)
            {
                case ProgramAction.Help: Console.WriteLine(_help); break;
                case ProgramAction.Version: Console.WriteLine(_version); break;
                case ProgramAction.CompleteSave: Execute(argCommand.Command); break;
                case ProgramAction.DifferentialSave: Execute(argCommand.Command); break;
                default: break;
            }
        }

        static void Execute(SaveManager.Command command)
        {
            try { Console.Clear(); } catch { }
            var progresses = new List<Progress.Progress>();
            var bars = new List<ProgressBar>();

            for (int i = 0; i < command.Saves.Length; i++)
            {
                Progress.Progress progress = new();
                var bar = new ProgressBar(command.Saves[i].SaveName, i, progress);
                progresses.Add(progress);
                bars.Add(bar);
                bar.Update(); // First render
            }

            var res = SaveManager.SaveManager.Execute(command, [..progresses]);

            string end_message;
            if (res.IsOk) end_message = $"{Messages.SaveSuccess}";
            else end_message = $"{Messages.SaveFailed}";
            Console.WriteLine($"\n--- {end_message} ---");
            if (!Console.IsInputRedirected) Console.ReadKey();
            try { Console.Clear(); } catch { }
        }
    }
}