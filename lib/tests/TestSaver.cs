using Xunit;
using Save;
using System.IO;

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
    }
}