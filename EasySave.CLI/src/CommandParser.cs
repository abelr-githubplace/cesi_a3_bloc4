using System;
using System.Collections.Generic;
using System.Linq;

namespace EasySave.CLI
{
    public enum ActionType { Save, Delete, Restore, Pause, Cancel, Continue, Free, Help, Version, Interactive, Lang }

    public class CommandResult
    {
        public ActionType Action { get; set; } = ActionType.Save;
        public List<string> JobIds { get; set; } = new List<string>();
    }

    public static class CommandParser
    {
        public static CommandResult Parse(string[] args)
        {
            var result = new CommandResult();

            if (args == null || args.Length == 0)
            {
                result.Action = ActionType.Interactive;
                return result;
            }

            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    switch (arg.ToLower())
                    {
                        case "-s": case "--save": result.Action = ActionType.Save; break;
                        case "-h": case "--help": result.Action = ActionType.Help; break;
                        case "-v": case "--version": result.Action = ActionType.Version; break;
                        case "lang": result.Action = ActionType.Lang; break;
                    }
                }
                else
                {
                    result.JobIds.AddRange(ExtractIds(arg));
                }
            }
            return result;
        }

        private static List<string> ExtractIds(string input)
        {
            var ids = new List<string>();
            input = input.Trim();

            try
            {
                if (input.Contains(";"))
                {
                    var segments = input.Split(';');
                    foreach (var segment in segments)
                    {
                        ids.AddRange(ExtractIds(segment));
                    }
                }
                else if (input.Contains("-"))
                {
                    var parts = input.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                    {
                        for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++)
                            ids.Add(i.ToString());
                    }
                }
                else if (!string.IsNullOrWhiteSpace(input))
                {
                    ids.Add(input);
                }
            }
            catch {}

            return ids.Distinct().ToList();
        }
    }
}