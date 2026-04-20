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
        public void DifferentialSaver_ShouldOnlySelectNewerFiles()
        {
            string fileName = "test.txt";
            string sourceFile = Path.Combine(_testSource, fileName);
            string destFile = Path.Combine(_testDest, fileName);

            File.WriteAllText(sourceFile, "Version 2");
            File.WriteAllText(destFile, "Version 1");
            File.SetLastWriteTime(destFile, System.DateTime.Now.AddDays(-1));

            var saver = new DifferentialSaver("Test", _testSource, _testDest);

            saver.StartSave();

            Assert.True(File.Exists(Path.Combine(_testDest, fileName)));
        }
    }
}