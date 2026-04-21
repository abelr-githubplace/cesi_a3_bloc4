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
                        case "--delete": result.Action = ActionType.Delete; break;
                        case "--restore": result.Action = ActionType.Restore; break;
                        case "--pause": result.Action = ActionType.Pause; break;
                        case "--cancel": result.Action = ActionType.Cancel; break;
                        case "--continue": result.Action = ActionType.Continue; break;
                        case "--free": result.Action = ActionType.Free; break;
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
            try
            {
                if (input.Contains("-"))
                {
                    var parts = input.Split('-');
                    int start = int.Parse(parts[0]);
                    int end = int.Parse(parts[1]);
                    for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++) ids.Add(i.ToString());
                }
                else if (input.Contains(";"))
                {
                    ids.AddRange(input.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)));
                }
                else
                {
                    ids.Add(input);
                }
            }
            catch { }
            return ids;
        }
    }
}