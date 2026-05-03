using EasyLog;

namespace EasySaveConsole
{
    public class Parser
    {
        public record ParsedCommand(ProgramAction Action, LogFormat Format, List<int>? SaveIds);

        public static ParsedCommand Parse(string[] args)
        {
            var logFormat = LogFormat.JSON;
            var default_command = new ParsedCommand(ProgramAction.InteractiveMode, logFormat, null);
            if (args == null || args.Length == 0) return default_command;

            ProgramAction action = ProgramAction.CompleteSave; // Default action is complete save
            var i = 0;
            while (args[i].StartsWith("-"))
            {
                var new_action = ParseOption(args[i]);
                switch (new_action)
                {
                    case ProgramAction.LogFormatJSON: logFormat = LogFormat.JSON; break;
                    case ProgramAction.LogFormatXML: logFormat = LogFormat.XML; break;
                    case ProgramAction.LogFormatText: logFormat = LogFormat.Text; break;
                    case ProgramAction.InteractiveMode: return default_command;
                    default: action = new_action; break;
                }
                i++;
            }
            return new ParsedCommand(action, logFormat, ParseArguments(args[i]));
        }

        private static List<int> ParseRange(string range)
        {
            var str_ids = range.Split('-');
            List<int> ids = new List<int>();
            if (str_ids.Length == 2 && int.TryParse(str_ids[0], out int start) && int.TryParse(str_ids[1], out int end))
            {
                for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++) ids.Add(i);
            }
            return ids;
        }

        private static List<int> ParseSequence(string sequence)
        {
            var str_ids = sequence.Split(';');
            List<int> ids = new List<int>();
            foreach (var str_id in str_ids)
            {
                if (int.TryParse(str_id, out int id)) ids.Add(id);
            }
            return ids;
        }

        public static List<int> ParseArguments(string arguments)
        {
            string input = arguments.Trim();

            if (input.Contains(";")) return ParseSequence(input);
            if (input.Contains("-")) return ParseRange(input);

            var ids = new List<int>();
            if (int.TryParse(input, out int id)) ids.Add(id);
            return ids;
        }

        private static ProgramAction ParseOption(string option)
        {
            switch (option)
            {
                case "--save": return ProgramAction.CompleteSave;
                case "--complete": return ProgramAction.CompleteSave;
                case "--differential": return ProgramAction.DifferentialSave;
                case "--log=JSON": return ProgramAction.LogFormatJSON;
                case "--log=XML": return ProgramAction.LogFormatXML;
                case "--log=Text": return ProgramAction.LogFormatText;
                case "-h": case "--help": return ProgramAction.Help;
                case "-v": case "--version": return ProgramAction.Version;
                case "-i": case "--interactive": default: return ProgramAction.InteractiveMode;
            }
        }
    }
}