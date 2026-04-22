using System;
using System.Collections.Generic;
using System.Linq;

namespace EasySave.CLI
{
    public enum ProgramAction
    {
        SaveAction,
        ConfigChange,
        InteractiveMode,
        Version,
        Help
    }

    public class Command
    {
        public ProgramAction Action { get; set; } = ProgramAction.InteractiveMode;
        public List<int> JobIds { get; set; } = new List<int>();
    }

    public class Parser
    {
        public static Command Parse(string[] args)
        {
            var command = new Command();

            if (args == null || args.Length == 0)
            {
                command.Action = ProgramAction.InteractiveMode;
                return command;
            }

            foreach (string arg in args)
            {
                if (arg.StartsWith("-") || arg.StartsWith("--"))
                {
                    command.Action = ParseOption(arg);
                }
                else
                {
                    command.JobIds.AddRange(ParseArguments(arg));
                }
            }
            return command;
        }

        public static List<int> ParseArguments(string arguments)
        {
            var ids = new List<int>();
            string input = arguments.Trim();

            try
            {
                if (input.Contains(";"))
                {
                    var segments = input.Split(';');
                    foreach (var segment in segments)
                        ids.AddRange(ParseArguments(segment));
                }
                else if (input.Contains("-"))
                {
                    var parts = input.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                            ids.Add(i);
                    }
                }
                else if (int.TryParse(input, out int id))
                {
                    ids.Add(id);
                }
            }
            catch { }

            return ids.Distinct().ToList();
        }

        private static ProgramAction ParseOption(string option)
        {
            switch (option.ToLower())
            {
                case "-s": case "--save": return ProgramAction.SaveAction;
                case "-c": case "--config": return ProgramAction.ConfigChange;
                case "-h": case "--help": return ProgramAction.Help;
                case "-v": case "--version": return ProgramAction.Version;
                default: return ProgramAction.InteractiveMode;
            }
        }
    }
}