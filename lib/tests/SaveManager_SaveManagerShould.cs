using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using State;
using EasyLog;
using Config;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class SaveManager_SaveManagerShould
    {
        private string _workDir = null!;
        private ConfigManager _config = null!;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "easysave-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
            ResetSingleton(typeof(StateManager));
            ResetSingleton(typeof(Logger));
            _config = ConfigManager.Get();
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

        private SaveInfo MakeSave(string name)
        {
            string src = Path.Combine(_workDir, name + "-src");
            Directory.CreateDirectory(src);
            File.WriteAllText(Path.Combine(src, "f.txt"), "data");
            return new SaveInfo
            {
                SaveId = Guid.NewGuid(),
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
                Saves = [MakeSave("only")],
            };

            var res = SaveManager.SaveManager.Execute(command, [], _config);

            Assert.IsTrue(res.IsErr, "Length mismatch between saves and progresses must abort");
        }

        [TestMethod]
        public void Execute_SaveAction_RunsAllSavesAndReturnsTrue()
        {
            var saves = new[] { MakeSave("a"), MakeSave("b") };
            var progresses = new[] { new Progress.Progress(), new Progress.Progress() };
            var command = new Command
            {
                SaveAction = SaveManager.Action.CompleteSave,
                Saves = saves,
            };

            var res = SaveManager.SaveManager.Execute(command, progresses, _config);

            Assert.IsTrue(res.IsOk, $"{string.Join(" | ",res.UnwrapErr())}");
            Assert.IsTrue(File.Exists(Path.Combine(saves[0].DestinationPath, "f.txt")));
            Assert.IsTrue(File.Exists(Path.Combine(saves[1].DestinationPath, "f.txt")));
            Assert.AreEqual(100f, progresses[0].GetProgress());
            Assert.AreEqual(100f, progresses[1].GetProgress());
        }

        [TestMethod]
        public void Execute_CompleteSave_BlockedWhenBusinessSoftwareRunning()
        {
            ResetSingleton(typeof(ConfigManager));
            var config = ConfigManager.Get();
            string ownProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            config.AddBusinessSoftwares([ownProcess]);
            var save = MakeSave("blocked");
            var command = new Command
            {
                SaveAction = SaveManager.Action.CompleteSave,
                Saves = [save],
            };

            var res = SaveManager.SaveManager.Execute(command, [new Progress.Progress()], config);

            Assert.IsTrue(res.IsOk, $"{string.Join(" | ", res.UnwrapErr())}");
            Assert.IsFalse(File.Exists(Path.Combine(save.DestinationPath, "f.txt")), "Save must not run when business software is detected");
            ResetSingleton(typeof(ConfigManager));
        }

        [TestMethod]
        public void Execute_DifferentialSave_BlockedWhenBusinessSoftwareRunning()
        {
            ResetSingleton(typeof(ConfigManager));
            var config = ConfigManager.Get();
            string ownProcess = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            config.AddBusinessSoftwares([ownProcess]);

            var save = MakeSave("diff");
            var command = new Command
            {
                SaveAction = SaveManager.Action.DifferentialSave,
                Saves = [save],
            };

            var res = SaveManager.SaveManager.Execute(command, [new Progress.Progress()], config);

            Assert.IsTrue(res.IsOk, $"{string.Join(" | ", res.UnwrapErr())}");
            Assert.IsFalse(File.Exists(Path.Combine(save.DestinationPath, "f.txt")), "Save must not run when business software is detected");
            ResetSingleton(typeof(ConfigManager));
        }
    }
}
