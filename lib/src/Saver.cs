using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EasySaveLibrary.Save
{
    public abstract class Saver
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public SaveType Type { get; protected set; }

        protected Saver(string name, string source, string destination)
        {
            Name = name;
            Source = source;
            Destination = destination;
        }

        public abstract void Start();
        public abstract List<FileInfo> GetFilesToCopy();

        protected void CopyFile(FileInfo file, string targetPath)
        {
            string destFile = Path.Combine(targetPath, Path.GetRelativePath(Source, file.FullName));
            string destDir = Path.GetDirectoryName(destFile);

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Ici, on appellerait le logger et le StateManager avant/après la copie
            file.CopyTo(destFile, true);
        }
    }

    public class CompleteSaver : Saver
    {
        public CompleteSaver(string name, string source, string destination) 
            : base(name, source, destination) 
        {
            Type = SaveType.Complete;
        }

        public override List<FileInfo> GetFilesToCopy()
        {
            var dir = new DirectoryInfo(Source);
            return dir.GetFiles("*", SearchOption.AllDirectories).ToList();
        }

        public override void Start()
        {
            var files = GetFilesToCopy();
            foreach (var file in files)
            {
                CopyFile(file, Destination);
            }
        }
    }

    public class DifferentialSaver : Saver
    {
        public DifferentialSaver(string name, string source, string destination) 
            : base(name, source, destination) 
        {
            Type = SaveType.Differential;
        }

        public override List<FileInfo> GetFilesToCopy()
        {
            var sourceDir = new DirectoryInfo(Source);
            var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);
            
            return files.Where(f => {
                string relativePath = Path.GetRelativePath(Source, f.FullName);
                string destFilePath = Path.Combine(Destination, relativePath);   
                if (!File.Exists(destFilePath)) return true;
                return f.LastWriteTime > File.GetLastWriteTime(destFilePath);
            }).ToList();
        }

        public override void Start()
        {
            var files = GetFilesToCopy();
            foreach (var file in files)
            {
                CopyFile(file, Destination);
            }
        }
    }

    public enum SaveType { Complete, Differential }
}
