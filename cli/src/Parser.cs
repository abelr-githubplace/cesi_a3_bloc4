namespace EasySaveConsole
{
    public class Parser
    {
        public record ParsedCommand(ProgramAction Action, List<int>? SaveIds, SaveManager.SaveType SaveType);

        public static ParsedCommand Parse(string[] args)
        {
            if (args == null || args.Length == 0) return new ParsedCommand(ProgramAction.InteractiveMode, null, SaveManager.SaveType.Complete);

            ProgramAction action = ProgramAction.SaveAction; // No option with arguments is a save
            List<int>? saveIds = null;
            SaveManager.SaveType saveType = SaveManager.SaveType.Complete;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "-t" || arg == "--type")
                {
                    if (i + 1 < args.Length)
                    {
                        saveType = ParseSaveType(args[i + 1]);
                        i++;
                    }
                }
                else if (arg.StartsWith("--type=")) saveType = ParseSaveType(arg.Substring("--type=".Length));
                else if (arg.StartsWith("-")) action = ParseOption(arg);
                else saveIds = ParseArguments(arg);
            }
            return new ParsedCommand(action, saveIds, saveType);
        }

        private static SaveManager.SaveType ParseSaveType(string value)
        {
            string v = value.Trim().ToLowerInvariant();
            if (v == "differential" || v == "diff" || v == "d") return SaveManager.SaveType.Differential;
            return SaveManager.SaveType.Complete;
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