using Xunit;
using Save;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace EasySaveLibrary.Tests
{
    public class TestSaver
    {
        private readonly string _testSource = Path.Combine(Path.GetTempPath(), "EasySaveSource");
        private readonly string _testDest = Path.Combine(Path.GetTempPath(), "EasySaveDest");

        public TestSaver()
        {
            if (Directory.Exists(_testSource)) Directory.Delete(_testSource, true);
            if (Directory.Exists(_testDest)) Directory.Delete(_testDest, true);
            Directory.CreateDirectory(_testSource);
            Directory.CreateDirectory(_testDest);
        }

        [Fact]
        public void CompleteSaveJob_ShouldCopyAllFiles()
        {
            string fileName = "test.txt";
            string sourceFile = Path.Combine(_testSource, fileName);
            string destFile = Path.Combine(_testDest, fileName);

            File.WriteAllText(sourceFile, "Version 2");
            long fileSize = new FileInfo(sourceFile).Length;

            var job = new CompleteSaveJob("TestJob", sourceFile, destFile, fileSize);

            job.Execute();

            Assert.True(File.Exists(destFile));
            Assert.Equal("Version 2", File.ReadAllText(destFile));
        }

        [Fact]
        public void DifferentialSaveJob_ShouldCopyOnlyModifiedOrNewFiles()
        {
      
            string modifiedFileName = "modified.txt";
            string modSrc = Path.Combine(_testSource, modifiedFileName);
            string modDest = Path.Combine(_testDest, modifiedFileName);

            File.WriteAllText(modSrc, "Modified Content");
            File.WriteAllText(modDest, "Old Content");
            File.SetLastWriteTime(modDest, DateTime.Now.AddDays(-1));

            var jobMod = new DifferentialSaveJob("DiffJob_Mod", modSrc, modDest, new FileInfo(modSrc).Length);
            long copiedMod = jobMod.Execute();

            Assert.True(copiedMod > 0, "File should have been copied");
            Assert.Equal("Modified Content", File.ReadAllText(modDest));

  
            string unchangedFileName = "unchanged.txt";
            string unchangedSrc = Path.Combine(_testSource, unchangedFileName);
            string unchangedDest = Path.Combine(_testDest, unchangedFileName);

            File.WriteAllText(unchangedSrc, "Same Content");
            File.WriteAllText(unchangedDest, "Same Content");
            File.SetLastWriteTime(unchangedDest, DateTime.Now.AddDays(1)); 

            var jobUnchanged = new DifferentialSaveJob("DiffJob_Unchanged", unchangedSrc, unchangedDest, new FileInfo(unchangedSrc).Length);
            long copiedUnchanged = jobUnchanged.Execute();

            Assert.Equal(new FileInfo(unchangedSrc).Length, copiedUnchanged); 

            Assert.Equal("Same Content", File.ReadAllText(unchangedDest));
        }

        private class MockSaver : Saver
        {
            public MockSaver(string n, string s, string d) : base(n, s, d) {}

            protected override SaveJob CreateJob(string sourceFile, string destFile, long fileSize)
            {
                return new CompleteSaveJob(Name, sourceFile, destFile, fileSize);
            }
        }

        [Fact]
        public void Saver_ShouldExecuteMultipleJobs()
        {
            File.WriteAllText(Path.Combine(_testSource, "file1.txt"), "Data 1");
            File.WriteAllText(Path.Combine(_testSource, "file2.txt"), "Data 2");

            Saver saver = new MockSaver("TestSaver", _testSource, _testDest);


            saver.ExecuteAll();

            Assert.Equal(2, saver.GetJobs().Count);
            Assert.True(File.Exists(Path.Combine(_testDest, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(_testDest, "file2.txt")));
        }

        [Fact]
        public void Saver_ShouldUpdateProgress_DuringExecution()
        {
            File.WriteAllText(Path.Combine(_testSource, "prog1.txt"), "A");
            File.WriteAllText(Path.Combine(_testSource, "prog2.txt"), "B");

            Saver saver = new MockSaver("ProgSaver", _testSource, _testDest);

            Assert.Equal(0f, saver.GetProgress().GetProgress());

            saver.ExecuteAll();

            Assert.Equal(100f, saver.GetProgress().GetProgress());
        }
    }
}
