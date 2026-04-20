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
        protected long FileSize;
        protected Priority Priority;

        protected SaveJob(string source, string destination)
        {
            SourceFile = source;
            DestinationFile = destination;
        }

        public long Execute()
        {
            long expectedBytes = GetTotalBytesToCopy();
            long copiedBytes = CopyFiles();

            if (copiedBytes != expectedBytes)
            {
                throw new Exception($"Erreur de sauvegarde : {expectedBytes} octets attendus, mais {copiedBytes} octets copiés.");
            }

            return copiedBytes;
        }

        protected abstract long GetTotalBytesToCopy();
        protected abstract long CopyFiles();
    }

    public class CompleteSaveJob : SaveJob
    {
        public CompleteSaveJob(string source, string destination) 
            : base(source, destination)
        {
        }

        protected override long GetTotalBytesToCopy()
        {
            throw new NotImplementedException("Calcul de la taille totale à implémenter.");
        }

        protected override long CopyFiles()
        {
            throw new NotImplementedException("Implémentation de la sauvegarde complète à migrer ici.");
        }
    }

    public class DifferentialSaveJob : SaveJob
    {
        public DifferentialSaveJob(string source, string destination) 
            : base(source, destination)
        {
        }

        protected override long GetTotalBytesToCopy()
        {
            throw new NotImplementedException("Calcul de la taille totale à implémenter.");
        }

        protected override long CopyFiles()
        {
            throw new NotImplementedException("Implémentation de la sauvegarde différentielle à migrer ici.");
        }
    }
}