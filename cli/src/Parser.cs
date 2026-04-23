namespace EasySaveConsole
{
    public class Parser
    {
        public record ParsedCommand(ProgramAction Action, List<int>? SaveIds);

        public static ParsedCommand Parse(string[] args)
        {
            if (args == null || args.Length == 0) return new ParsedCommand(ProgramAction.InteractiveMode, null);

            ProgramAction action = ProgramAction.SaveAction; // No option with arguments is a save
            List<int>? saveIds = null;

            foreach (string arg in args)
            {
                if (arg.StartsWith("-")) action = ParseOption(arg);
                else saveIds = ParseArguments(arg);
            }
            return new ParsedCommand(action, saveIds);
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
                case "--save": return ProgramAction.SaveAction;
                case "-h": case "--help": return ProgramAction.Help;
                case "-v": case "--version": return ProgramAction.Version;
                default: return ProgramAction.InteractiveMode;
            }
        }
    }
}