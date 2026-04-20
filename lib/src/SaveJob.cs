using System;
using System;
using System.Linq;

namespace Save
{
    public enum Priority
    {
        High,
        Medium,
        Low
    }

    public abstract class SaveJob
    {
        protected string SourceFile;
        protected string DestinationFile;
        protected int FileSize;
        protected Priority Priority;

        protected SaveJob(string name, string source, string destination)
        {
            SourceFile = source;
            DestinationFile = destination;
        }

        public abstract void Execute();
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string name, string source, string destination) 
            : base(name, source, destination)
        {
        }

        public override void Execute()
        {
            throw new NotImplementedException("Implémentation de la sauvegarde complète à migrer ici.");
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        public DifferentialSaveJob(string name, string source, string destination) 
            : base(name, source, destination)
        {
        }

        public override void Execute()
        {
            throw new NotImplementedException("Implémentation de la sauvegarde différentielle à migrer ici.");
        }
    }
}