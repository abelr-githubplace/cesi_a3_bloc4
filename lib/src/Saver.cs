using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Save
{
    public class SaveProgress
    {
    }

    public abstract class Saver
    {
        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }

        protected int TotalFilesSize { get; set; }
        protected SaveProgress SaveProgress { get; set; }

        protected Saver(string name, string sourcePath, string destinationPath)
        {
            Name = name;
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            SaveProgress = new SaveProgress();
        }

        public abstract void StartSave();

        public float GetProgress()
        {
            return 0f;
        }

        protected virtual List<string> GetChangedFiles()
        {
            return new List<string>();
        }

        protected void CopyFile(string sourceFilePath, string targetPath)
        {
            if (!File.Exists(sourceFilePath)) return;

            string destFile = Path.Combine(targetPath, Path.GetRelativePath(SourcePath, sourceFilePath));
            string destDir = Path.GetDirectoryName(destFile);

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(sourceFilePath, destFile, true);
        }
    }

    public class CompleteSaver : Saver
    {
        public CompleteSaver(string name, string source, string destination) 
            : base(name, source, destination) 
        {
        }

        protected override List<string> GetChangedFiles()
        {
            var dir = new DirectoryInfo(SourcePath);
            return dir.GetFiles("*", SearchOption.AllDirectories).Select(f => f.FullName).ToList();
        }

        public override void StartSave()
        {
            var files = GetChangedFiles();
            foreach (var file in files)
            {
                CopyFile(file, DestinationPath);
            }
        }
    }

    public class DifferentialSaver : Saver
    {
        public DifferentialSaver(string name, string source, string destination) 
            : base(name, source, destination) 
        {
        }

        protected override List<string> GetChangedFiles()
        {
            var sourceDir = new DirectoryInfo(SourcePath);
            var files = sourceDir.GetFiles("*", SearchOption.AllDirectories);

            return files.Where(f => {
                string relativePath = Path.GetRelativePath(SourcePath, f.FullName);
                string destFilePath = Path.Combine(DestinationPath, relativePath);   
                if (!File.Exists(destFilePath)) return true;
                return f.LastWriteTime > File.GetLastWriteTime(destFilePath);
            }).Select(f => f.FullName).ToList();
        }

        public override void StartSave()
        {
            var files = GetChangedFiles();
            foreach (var file in files)
            {
                CopyFile(file, DestinationPath);
            }
        }
    }

    public enum SaveType { Complete, Differential }
}
