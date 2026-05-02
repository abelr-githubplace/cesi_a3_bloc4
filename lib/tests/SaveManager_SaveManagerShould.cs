using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using Saver;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class SaveManager_SaveManagerShould
    {
        private string _workDir = null!;
        private Config _config = null!;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "easysave-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
            ResetSingleton(typeof(StateManager.StateManager));
            ResetSingleton(typeof(EasyLog.Logger));
            _config = new Config
            {
                Logger = EasyLog.Logger.Get(Path.Combine(_workDir, "save.log")),
                StateManager = StateManager.StateManager.Get(Path.Combine(_workDir, "state.json")),
                LogFormat = EasyLog.LogFormat.JSON,
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

        private SaveInfo MakeSave(string name)
        {
            string src = Path.Combine(_workDir, name + "-src");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "f.txt"), "data");
            return new SaveInfo
            {
                SaveId = 1,
                SaveName = name,
                SourcePath = src,
                DestinationPath = Path.Combine(_workDir, name + "-dst"),
            };
        }

        [TestMethod]
        public void Execute_MismatchedProgressCount_ReturnsFalse()
        {
            var command = new Command
            {
                SaveAction = SaveManager.Action.CompleteSave,
                Saves = new[] { MakeSave("only") },
            };

            bool ok = SaveManager.SaveManager.Execute(command, new Progress[] { }, _config);

            Assert.IsFalse(ok, "Length mismatch between saves and progresses must abort");
        }

        [TestMethod]
        public void Execute_SaveAction_RunsAllSavesAndReturnsTrue()
        {
            var saves = new[] { MakeSave("a"), MakeSave("b") };
            var progresses = new[] { new Progress(), new Progress() };
            var command = new Command
            {
                SaveAction = SaveManager.Action.CompleteSave,
                Saves = saves,
            };

            bool ok = SaveManager.SaveManager.Execute(command, progresses, _config);

            Assert.IsTrue(ok);
            Assert.IsTrue(File.Exists(Path.Combine(saves[0].DestinationPath, "f.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(saves[1].DestinationPath, "f.txt")));
            Assert.AreEqual(100f, progresses[0].GetProgress());
            Assert.AreEqual(100f, progresses[1].GetProgress());
        }
    }
}
