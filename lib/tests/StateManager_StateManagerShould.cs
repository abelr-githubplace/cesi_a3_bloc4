using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StateManager;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class StateManager_StateManagerShould
    {
        private string _workDir = null!;
        private string _stateFile = null!;

        [TestInitialize]
        public void Setup()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "easysave-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workDir);
            _stateFile = Path.Combine(_workDir, "state.json");
            ResetSingleton();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ResetSingleton();
            if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true);
        }

        private static void ResetSingleton()
        {
            var field = typeof(StateManager.StateManager)
                .GetField("s_instance", BindingFlags.Static | BindingFlags.NonPublic);
            field!.SetValue(null, null);
        }

        private static SaveState BuildState(uint id, string name) => new SaveState
        {
            Id = id,
            Name = name,
            SourcePath = "C:/src",
            DestinationPath = "C:/dst",
            LastActionTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = Status.Inactive,
            ActiveStateInfo = null,
        };

        [TestMethod]
        public void Get_FirstCall_CreatesOutputFile()
        {
            Assert.IsFalse(File.Exists(_stateFile));

            StateManager.StateManager.Get(_stateFile);

            Assert.IsTrue(File.Exists(_stateFile), "State file must be created when missing");
        }

        [TestMethod]
        public void Get_ReturnsSameInstance()
        {
            var a = StateManager.StateManager.Get(_stateFile);
            var b = StateManager.StateManager.Get(_stateFile);

            Assert.AreSame(a, b, "Get must return the singleton instance");
        }

        [TestMethod]
        public void Save_NewState_PersistsToDisk()
        {
            var sm = StateManager.StateManager.Get(_stateFile);

            sm.Save(BuildState(1, "JobOne"));

            string json = File.ReadAllText(_stateFile);
            StringAssert.Contains(json, "JobOne");
        }

        [TestMethod]
        public void Save_SameName_UpdatesExistingEntry()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(1, "Job"));
            sm.Save(BuildState(1, "Job") with { Status = Status.Active });

            var saves = sm.GetSaves();
            Assert.AreEqual(1, saves.Count, "Saving the same name twice must not duplicate the entry");
        }

        [TestMethod]
        public void GetSaves_ReturnsOneEntryPerSave()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(1, "A"));
            sm.Save(BuildState(2, "B"));

            var saves = sm.GetSaves();

            Assert.AreEqual(2, saves.Count);
            CollectionAssert.AreEquivalent(
                new[] { "A", "B" },
                saves.Select(s => s.SaveName).ToArray());
        }

        [TestMethod]
        public void Get_ExistingFile_LoadsPriorState()
        {
            var first = StateManager.StateManager.Get(_stateFile);
            first.Save(BuildState(7, "Persisted"));

            ResetSingleton();
            var reloaded = StateManager.StateManager.Get(_stateFile);

            var saves = reloaded.GetSaves();
            Assert.AreEqual(1, saves.Count);
            Assert.AreEqual("Persisted", saves[0].SaveName);
            Assert.AreEqual(7u, saves[0].SaveId);
        }
    }
}
