using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using EasySave.lang;

namespace EasySave.CLI
{
    public enum SaveType { Complete, Differential }

    public class App
    {
        public Command MainMenu()
        {
            bool exit = false;
            while (!exit)
            {
                Console.WriteLine($"\n[{Messages.MenuTitle}]");
                Console.WriteLine(Messages.MenuSave);
                Console.WriteLine(Messages.MenuOption);
                Console.WriteLine(Messages.MenuExit);

                var key = Console.ReadKey(true);
                switch (key.KeyChar)
                {
                    case '1':
                        List<string> idsStr = SaveMenu();
                        List<int> ids = new List<int>();
                        foreach (var idStr in idsStr)
                        {
                            if (int.TryParse(idStr, out int id)) ids.Add(id);
                        }
                        return new Command { Action = ProgramAction.SaveAction, JobIds = ids };
                    case '2':
                        OptionMenu();
                        break;
                    case 'Q':
                    case 'q':
                        exit = true;
                        break;
                }
            }
            return new Command { Action = ProgramAction.InteractiveMode };
        }

        private List<string> SaveMenu()
        {
            Console.Clear();
            Console.WriteLine(Messages.PromptSelection);
            Console.WriteLine(Messages.Explanation);

            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();

            var parsedIdsStr = new List<string>();
            var parsedInts = Parser.ParseArguments(input);
            foreach (var i in parsedInts) parsedIdsStr.Add(i.ToString());

            return parsedIdsStr;
        }

        private SaveType SaveTypeMenu()
        {
            return SaveType.Complete;
        }

        private void OptionMenu()
        {
            bool back = false;
            while (!back)
            {
                Console.Clear();
                Console.WriteLine($"\n[{Messages.OptionTitle}]");
                Console.WriteLine(Messages.OptLang);
                Console.WriteLine(Messages.OptBack);

                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape) break;

                switch (key.KeyChar)
                {
                    case '1': LanguageMenu(); break;
                    case 'R': case 'r': back = true; break;
                }
            }
        }

        private void LanguageMenu()
        {
            Console.WriteLine("\n 1. English | 2. Français");
            var choice = Console.ReadLine();
            string culture = (choice == "2") ? "fr-FR" : "en-US";
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
        }
    }
}