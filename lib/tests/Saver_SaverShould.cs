using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using Saver;
using StateManager;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class Saver_SaverShould
    {
        private string _workDir = null!;
        private string _src = null!;
        private string _dst = null!;
        private string _stateFile = null!;
        private string _logFile = null!;
        private Config _config = null!;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "easysave-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
            _src = Path.Combine(_workDir, "src");
            _dst = Path.Combine(_workDir, "dst");
            Directory.CreateDirectory(_src);
            _stateFile = Path.Combine(_workDir, "state.json");
            _logFile = Path.Combine(_workDir, "save.log");

            ResetSingleton(typeof(StateManager.StateManager));
            ResetSingleton(typeof(EasyLog.Logger));

            _config = new Config
            {
                Logger = EasyLog.Logger.Get(_logFile),
                StateManager = StateManager.StateManager.Get(_stateFile),
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResetSingleton(typeof(StateManager.StateManager));
            ResetSingleton(typeof(EasyLog.Logger));
            if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true);
        }

        private static void ResetSingleton(Type type)
        {
            var field = type.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field!.SetValue(null, null);
        }

        private static SaveInfo Info(string name, string source, string destination) => new SaveInfo
        {
            SaveId = 1,
            SaveName = name,
            SourcePath = source,
            DestinationPath = destination,
        };

        [TestMethod]
        public void Constructor_DirectorySource_IndexesEveryFile()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "AA");
            File.WriteAllText(Path.Combine(_src, "b.txt"), "BBB");

            var saver = new Saver.Saver(Info("Job", _src, _dst), SaveType.Complete, new Progress(), _config);

            Assert.AreEqual(2, saver.FilesWithSizes.Count);
            Assert.AreEqual(5L, saver.TotalSize);
        }

        [TestMethod]
        public void Constructor_FileSource_HandlesSingleFile()
        {
            string lone = Path.Combine(_workDir, "lone.txt");
            File.WriteAllText(lone, "hello");

            var saver = new Saver.Saver(Info("Job", lone, _dst), SaveType.Complete, new Progress(), _config);

            Assert.AreEqual(1, saver.FilesWithSizes.Count);
            Assert.AreEqual(5L, saver.TotalSize);
        }

        [TestMethod]
        public void Constructor_MissingSource_LeavesEmptyJobList()
        {
            string missing = Path.Combine(_workDir, "nope");

            var saver = new Saver.Saver(Info("Job", missing, _dst), SaveType.Complete, new Progress(), _config);

            Assert.AreEqual(0, saver.FilesWithSizes.Count);
            Assert.AreEqual(0L, saver.TotalSize);
        }

        [TestMethod]
        public void Start_CompleteSave_CopiesFilesAndPushesProgressToHundred()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "first");
            File.WriteAllText(Path.Combine(_src, "b.txt"), "second");
            var progress = new Progress();
            var saver = new Saver.Saver(Info("Job", _src, _dst), SaveType.Complete, progress, _config);

            saver.Start();

            Assert.IsTrue(File.Exists(Path.Combine(_dst, "a.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(_dst, "b.txt")));
            Assert.AreEqual(100f, progress.GetProgress());
        }

        [TestMethod]
        public void Start_EmptySource_CompletesWithoutWritingDestination()
        {
            var progress = new Progress();
            var saver = new Saver.Saver(Info("Job", _src, _dst), SaveType.Complete, progress, _config);

            saver.Start();

            Assert.IsFalse(Directory.Exists(_dst), "No jobs means no destination should be created");
            string json = File.ReadAllText(_stateFile);
            StringAssert.Contains(json, "Inactive", "Final state must still be persisted");
        }

        [TestMethod]
        public void Start_CompleteSave_WritesLogEntries()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "first");
            var saver = new Saver.Saver(Info("Job", _src, _dst), SaveType.Complete, new Progress(), _config);

            saver.Start();

            Assert.IsTrue(File.Exists(_logFile));
            string log = File.ReadAllText(_logFile);
            StringAssert.Contains(log, "Job");
            StringAssert.Contains(log, "a.txt");
        }

        [TestMethod]
        public void Start_PersistsInactiveStateAtCompletion()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "x");
            var saver = new Saver.Saver(Info("Job", _src, _dst), SaveType.Complete, new Progress(), _config);

            saver.Start();

            string json = File.ReadAllText(_stateFile);
            StringAssert.Contains(json, "Inactive");
        }
    }
}
