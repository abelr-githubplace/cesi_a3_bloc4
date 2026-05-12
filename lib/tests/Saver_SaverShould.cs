using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using Save;
using Config;
using State;
using EasyLog;

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
        private ConfigManager _config = null!;

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

            ResetSingleton(typeof(StateManager));
            ResetSingleton(typeof(Logger));

            _config = ConfigManager.Get();
            _config.ModifyLogOutput(_logFile);
            _config.ModifyStateOutput(_stateFile);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResetSingleton(typeof(StateManager));
            ResetSingleton(typeof(Logger));
            if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true);
        }

        private static void ResetSingleton(Type type)
        {
            var field = type.GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field!.SetValue(null, null);
        }

        private static SaveInfo Info(string name, string source, string destination) => new SaveInfo
        {
            SaveId = Guid.NewGuid(),
            SaveName = name,
            SourcePath = source,
            DestinationPath = destination,
        };

        [TestMethod]
        public void Constructor_DirectorySource_IndexesEveryFile()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "AA");
            File.WriteAllText(Path.Combine(_src, "b.txt"), "BBB");

            var saver = new Saver(Info("Job", _src, _dst), SaveManager.Action.CompleteSave, new Progress.Progress(), _config);

            Assert.HasCount(2, saver.FilesWithSizes);
            Assert.AreEqual(5L, saver.TotalSize);
        }

        [TestMethod]
        public void Constructor_FileSource_HandlesSingleFile()
        {
            string lone = Path.Combine(_workDir, "lone.txt");
            File.WriteAllText(lone, "hello");

            var saver = new Saver(Info("Job", lone, _dst), SaveManager.Action.CompleteSave, new Progress.Progress(), _config);

            Assert.HasCount(1, saver.FilesWithSizes);
            Assert.AreEqual(5L, saver.TotalSize);
        }

        [TestMethod]
        public void Constructor_MissingSource_LeavesEmptyJobList()
        {
            string missing = Path.Combine(_workDir, "nope");

            var saver = new Saver(Info("Job", missing, _dst), SaveManager.Action.CompleteSave, new Progress.Progress(), _config);

            Assert.HasCount(0, saver.FilesWithSizes);
            Assert.AreEqual(0L, saver.TotalSize);
        }

        [TestMethod]
        public void Start_CompleteSave_CopiesFilesAndPushesProgressToHundred()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "first");
            File.WriteAllText(Path.Combine(_src, "b.txt"), "second");
            var progress = new Progress.Progress();
            var saver = new Saver(Info("Job", _src, _dst), SaveManager.Action.CompleteSave, progress, _config);

            saver.Start(false);

            Assert.IsTrue(File.Exists(Path.Combine(_dst, "a.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(_dst, "b.txt")));
            Assert.AreEqual(100f, progress.GetProgress());
        }

        [TestMethod]
        public void Start_EmptySource_CompletesWithoutWritingDestination()
        {
            var progress = new Progress.Progress();
            var saver = new Saver(Info("Job", _src, _dst), SaveManager.Action.CompleteSave, progress, _config);

            saver.Start(false);

            Assert.IsFalse(Directory.Exists(_dst), "No jobs means no destination should be created");
            string json = File.ReadAllText(_stateFile);
            Assert.Contains("Inactive", json, "Final state must still be persisted");
        }

        [TestMethod]
        public void Start_CompleteSave_WritesLogEntries()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "first");
            var saver = new Saver(Info("Job", _src, _dst), SaveManager.Action.CompleteSave, new Progress.Progress(), _config);

            saver.Start(false);

            Assert.IsTrue(File.Exists(_logFile));
            string log = File.ReadAllText(_logFile);
            Assert.Contains("Job", log);
            Assert.Contains("a.txt", log);
        }

        [TestMethod]
        public void Start_PersistsInactiveStateAtCompletion()
        {
            File.WriteAllText(Path.Combine(_src, "a.txt"), "x");
            var saver = new Saver(Info("Job", _src, _dst), SaveManager.Action.CompleteSave, new Progress.Progress(), _config);

            saver.Start(false);

            string json = File.ReadAllText(_stateFile);
            Assert.Contains("Inactive", json);
        }
    }
}
