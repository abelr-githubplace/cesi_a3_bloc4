using Xunit;
using Save;
using System.IO;
using System.Linq;
using System.Threading;

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

            var job = new CompleteSaveJob("TestJob", _testSource, _testDest);

            var toCopy = job.GetFilesToCopy();
            Assert.Single(toCopy);
            Assert.Equal(sourceFile, toCopy.First());

            job.Execute();

            Assert.True(File.Exists(destFile));
            Assert.Equal("Version 2", File.ReadAllText(destFile));
        }

        [Fact]
        public void DifferentialSaveJob_ShouldCopyOnlyModifiedOrNewFiles()
        {
            string newFileName = "new.txt";
            string modifiedFileName = "modified.txt";
            string unchangedFileName = "unchanged.txt";

            File.WriteAllText(Path.Combine(_testSource, newFileName), "New Content");

            string modSrc = Path.Combine(_testSource, modifiedFileName);
            string modDest = Path.Combine(_testDest, modifiedFileName);
            File.WriteAllText(modSrc, "Modified Content");
            File.WriteAllText(modDest, "Old Content");

            // Artificial delay to make sure LastWriteTime is older for the destination file
            Thread.Sleep(10);
            File.SetLastWriteTime(modDest, System.DateTime.Now.AddMinutes(-5));

            string unchangedSrc = Path.Combine(_testSource, unchangedFileName);
            string unchangedDest = Path.Combine(_testDest, unchangedFileName);
            File.WriteAllText(unchangedSrc, "Same Content");
            File.WriteAllText(unchangedDest, "Same Content");
            File.SetLastWriteTime(unchangedDest, File.GetLastWriteTime(unchangedSrc)); // Same timestamp

            var job = new DifferentialSaveJob("DiffJob", _testSource, _testDest);

            var toCopy = job.GetFilesToCopy().ToList();

            Assert.Equal(2, toCopy.Count);
            Assert.Contains(Path.Combine(_testSource, newFileName), toCopy);
            Assert.Contains(Path.Combine(_testSource, modifiedFileName), toCopy);

            job.Execute();

            Assert.True(File.Exists(Path.Combine(_testDest, newFileName)));
            Assert.Equal("Modified Content", File.ReadAllText(Path.Combine(_testDest, modifiedFileName)));
        }

        private class MockSaver : Saver
        {
            public MockSaver(string n, string s, string d) : base(n, s, d) {}
        }

        [Fact]
        public void Saver_ShouldExecuteMultipleJobs()
        {
            string sourceDir2 = Path.Combine(Path.GetTempPath(), "EasySaveSource2");
            string destDir2 = Path.Combine(Path.GetTempPath(), "EasySaveDest2");
            Directory.CreateDirectory(sourceDir2);
            Directory.CreateDirectory(destDir2);

            File.WriteAllText(Path.Combine(_testSource, "file1.txt"), "Data 1");
            File.WriteAllText(Path.Combine(sourceDir2, "file2.txt"), "Data 2");

            Saver saver = new MockSaver("TestSaver", _testSource, _testDest);
            saver.AddJob(new CompleteSaveJob("Job1", _testSource, _testDest));
            saver.AddJob(new CompleteSaveJob("Job2", sourceDir2, destDir2));

            Assert.Equal(2, saver.GetJobs().Count);

            saver.ExecuteAll();

            Assert.True(File.Exists(Path.Combine(_testDest, "file1.txt")));
            Assert.True(File.Exists(Path.Combine(destDir2, "file2.txt")));

            Directory.Delete(sourceDir2, true);
            Directory.Delete(destDir2, true);
        }

        [Fact]
        public void SaveJob_ShouldUpdateProgress_DuringExecution()
        {
            string file1 = Path.Combine(_testSource, "prog1.txt");
            string file2 = Path.Combine(_testSource, "prog2.txt");
            File.WriteAllText(file1, "A");
            File.WriteAllText(file2, "B");

            var job = new CompleteSaveJob("ProgJob", _testSource, _testDest);

            Assert.Equal(0f, job.GetProgress());

            job.Execute();

            Assert.Equal(100f, job.GetProgress());
        }
    }
}