using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using EasySave.lang;
using Save; // Namespace de votre bibliothèque EasySaveLibrary

namespace EasySave.CLI
{
    class Program
    {
        public static void Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");
            Command command = Parser.Parse(args);

            if (command.Action == ProgramAction.InteractiveMode)
            {
                App app = new App();
                command = app.MainMenu();
            }

            switch (command.Action)
            {
                case ProgramAction.Help:
                    Console.WriteLine(Messages.help);
                    break;
                case ProgramAction.Version:
                    Console.WriteLine("EasySave v1.0.0");
                    break;
                case ProgramAction.SaveAction:
                    if (command.JobIds != null && command.JobIds.Count > 0)
                    {
                        ExecuteJobs(command);
                    }
                    break;
            }
        }

        static void ExecuteJobs(Command command)
        {
            Console.WriteLine($"\n--- {Messages.Saving} ---");
            SaveManager manager = new SaveManager();

            List<Progress> progressList = manager.Execute(command);

            if (progressList != null)
            {
                foreach (Progress prog in progressList)
                {
                    ProgressBar bar = new ProgressBar(prog);
                    prog.Subscribe(bar);
                }
            }

            Console.WriteLine($"\n{Messages.Done}");
            Console.ReadKey();
        }
    }
}