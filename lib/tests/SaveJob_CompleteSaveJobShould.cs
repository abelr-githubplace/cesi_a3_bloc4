using Microsoft.VisualStudio.TestTools.UnitTesting;
using Job;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    public class SaveJob_CompleteSaveJobShould
    {
        private string _workDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "easysave-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true);
        }

        [TestMethod]
        public void Execute_SourceMissing_ReturnsZero()
        {
            string src = Path.Combine(_workDir, "missing.txt");
            string dst = Path.Combine(_workDir, "dst.txt");

            var job = new CompleteSaveFileJob(src, dst, 0, Priority.Low);
            long copied = job.Execute();

            Assert.AreEqual(0L, copied, "Missing source must yield zero copied bytes");
            Assert.IsFalse(File.Exists(dst), "Destination must not be created when source is missing");
        }

        [TestMethod]
        public void Execute_SourceExists_CopiesContentAndReturnsLength()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "nested", "dst.txt");
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("hello world");
            File.WriteAllBytes(src, payload);

            var job = new CompleteSaveFileJob(src, dst, payload.Length, Priority.High);
            long copied = job.Execute();

            Assert.AreEqual(payload.Length, copied, "Copied length should match the source size");
            CollectionAssert.AreEqual(payload, File.ReadAllBytes(dst), "Destination content must match source");
        }

        [TestMethod]
        public void Execute_OverwritesExistingDestination()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "dst.txt");
            File.WriteAllText(src, "new");
            File.WriteAllText(dst, "old-and-longer");

            var job = new CompleteSaveFileJob(src, dst, 3, Priority.Low);
            job.Execute();

            Assert.AreEqual("new", File.ReadAllText(dst), "Existing destination must be overwritten");
        }

        [TestMethod]
        public void Constructor_AssignsAllProperties()
        {
            var job = new CompleteSaveFileJob("a", "b", 42, Priority.High);

            Assert.AreEqual("a", job.SourceFile);
            Assert.AreEqual("b", job.DestinationFile);
            Assert.AreEqual(42L, job.FileSize);
            Assert.AreEqual(Priority.High, job.Priority);
        }
    }
}
