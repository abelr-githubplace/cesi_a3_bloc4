using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using EasySave.lang;

namespace EasySave.CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            CommandResult result = CommandParser.Parse(args);

            switch (result.Action)
            {
                case ActionType.Interactive:
                    ShowMainMenu();
                    break;
                case ActionType.Help:
                    ShowHelp();
                    break;
                case ActionType.Version:
                    Console.WriteLine("EasySave v1.0.0");
                    break;
                default:
                    ProcessDirectCommand(result);
                    break;
            }
        }

        static void ShowMainMenu()
        {
            bool exit = false;
            while (!exit)
            {
                Console.Clear();
                Console.WriteLine(Messages.MenuTitle);
                Console.WriteLine(Messages.MenuSave);
                Console.WriteLine(Messages.MenuDelete);
                Console.WriteLine(Messages.MenuPause);
                Console.WriteLine(Messages.MenuResume);
                Console.WriteLine(Messages.MenuRelease);
                Console.WriteLine(Messages.MenuOption);
                Console.WriteLine(Messages.MenuExit);

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1': RunSaveWorkflow(); break;
                    case '6': ShowOptionMenu(); break;
                    case '7': exit = true; break;
                }
            }
        }

        static void RunSaveWorkflow()
        {
            Console.Clear();
            Console.WriteLine(Messages.PromptSelection);
            Console.WriteLine(Messages.Explanation);
            Console.WriteLine($"\n[{Messages.EscKey}]");

            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return;

            var ids = CommandParser.Parse(new string[] { input }).JobIds;
            if (ids.Count > 0) ExecuteWork(ids, Messages.Save);
        }

        static void ShowOptionMenu()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine(Messages.OptionTitle);
                Console.WriteLine(Messages.OptLang);
                Console.WriteLine(Messages.OptColor);
                Console.WriteLine(Messages.OptBack);

                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;

                switch (key.KeyChar)
                {
                    case '1': ChangeLanguage(); break;
                    case '2': ChangeConsoleColor(); break;
                    case '3': back = true; break;
                }
            }
        }

        static void ChangeLanguage()
        {
            Console.Clear();
            Console.WriteLine("1. English | 2. Français");
            var choice = Console.ReadLine();
            string culture = (choice == "2") ? "fr-FR" : "en-US";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        }

        static void ChangeConsoleColor()
        {
            Console.Clear();
            Console.WriteLine(Messages.Color);
            var choice = Console.ReadKey(true);
            switch (choice.KeyChar)
            {
                case '1': Console.ForegroundColor = ConsoleColor.Cyan; break;
                case '2': Console.ForegroundColor = ConsoleColor.Green; break;
                case '3': Console.ForegroundColor = ConsoleColor.Yellow; break;
                default: Console.ResetColor(); break;
            }
            Console.Clear();
        }

        static void ProcessDirectCommand(CommandResult result)
        {
            if (result.JobIds.Count == 0)
            {
                Console.WriteLine(Messages.Error);
                return;
            }
            ExecuteWork(result.JobIds, result.Action.ToString());
        }

        static void ShowHelp() => Console.WriteLine(Messages.help);

        static void ExecuteWork(List<string> ids, string action)
        {
            SaveProgressBar bar = new SaveProgressBar();
            Console.WriteLine($"\n--- {action} ---");

            foreach (var id in ids)
            {
                Console.WriteLine($"\n> Job #{id}");
                for (int i = 0; i <= 100; i += 10)
                {
                    bar.Update(i);
                    Thread.Sleep(100);
                }
            }
            Console.WriteLine("\nDone.");
            Console.ReadKey();
        }
    }
}