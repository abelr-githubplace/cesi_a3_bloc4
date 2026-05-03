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

        private static SaveState BuildState(Guid id, string name) => new SaveState
        {
            Id = id,
            Name = name,
            SourcePath = "C:/src",
            DestinationPath = "C:/dst",
            LastActionTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Status = Status.Inactive,
            ActiveStateInfo = null,
        };

        private static Guid GuidFromInt(int n) => new Guid(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        private static SaveState BuildState(int id, string name) => BuildState(GuidFromInt(id), name);

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

            sm.Save(BuildState(Guid.NewGuid(), "JobOne"));

            string json = File.ReadAllText(_stateFile);
            StringAssert.Contains(json, "JobOne");
        }

        [TestMethod]
        public void Save_SameName_UpdatesExistingEntry()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            var id = Guid.NewGuid();
            sm.Save(BuildState(id, "Job"));
            sm.Save(BuildState(id, "Job") with { Status = Status.Active });

            var saves = sm.GetSaves();
            Assert.AreEqual(1, saves.Count, "Saving the same name twice must not duplicate the entry");
        }

        [TestMethod]
        public void GetSaves_ReturnsOneEntryPerSave()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(Guid.NewGuid(), "A"));
            sm.Save(BuildState(Guid.NewGuid(), "B"));

            var saves = sm.GetSaves();

            Assert.AreEqual(2, saves.Count);
            CollectionAssert.AreEquivalent(
                new[] { "A", "B" },
                saves.Select(s => s.SaveName).ToArray());
        }

        [TestMethod]
        public void Get_ExistingFile_LoadsPriorState()
        {
            var id = Guid.NewGuid();
            var first = StateManager.StateManager.Get(_stateFile);
            first.Save(BuildState(id, "Persisted"));

            ResetSingleton();
            var reloaded = StateManager.StateManager.Get(_stateFile);

            var saves = reloaded.GetSaves();
            Assert.AreEqual(1, saves.Count);
            Assert.AreEqual("Persisted", saves[0].SaveName);
            Assert.AreEqual(id, saves[0].SaveId);
        }

        [TestMethod]
        public void NewGuid_GeneratesUniqueIds()
        {
            var ids = new HashSet<Guid>();
            for (int i = 0; i < 1000; i++) ids.Add(Guid.NewGuid());

            Assert.AreEqual(1000, ids.Count, "Guid.NewGuid must never produce a duplicate");
        }

        [TestMethod]
        public void Save_SameId_DifferentName_UpdatesExistingEntry()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            var id = Guid.NewGuid();

            sm.Save(BuildState(id, "OldName"));
            sm.Save(BuildState(id, "RenamedJob"));

            var saves = sm.GetSaves();
            Assert.AreEqual(1, saves.Count, "Same Id must upsert, even when the name changes");
            Assert.AreEqual("RenamedJob", saves[0].SaveName);
            Assert.AreEqual(id, saves[0].SaveId);
        }

        [TestMethod]
        public void Save_PersistsIdAsCanonicalGuidString()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            var id = Guid.NewGuid();
            sm.Save(BuildState(id, "Job"));

            string json = File.ReadAllText(_stateFile);
            StringAssert.Contains(json, id.ToString(),
                "Id must be serialised in canonical 8-4-4-4-12 form");
        }

        [TestMethod]
        public void Get_ReloadedState_PreservesGuidExactly()
        {
            var id = Guid.NewGuid();
            var first = StateManager.StateManager.Get(_stateFile);
            first.Save(BuildState(id, "Roundtrip"));

            ResetSingleton();
            var reloaded = StateManager.StateManager.Get(_stateFile);

            var save = reloaded.GetSaves().Single();
            Assert.AreEqual(id, save.SaveId);
            Assert.AreNotEqual(Guid.Empty, save.SaveId);
        }

        [TestMethod]
        public void Delete_ExistingState_RemovesEntryAndReturnsTrue()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(1, "ToDelete"));
            sm.Save(BuildState(2, "Keep"));

            bool removed = sm.Delete("ToDelete");

            Assert.IsTrue(removed);
            var remaining = sm.GetSaves().Select(s => s.SaveName).ToArray();
            CollectionAssert.AreEqual(new[] { "Keep" }, remaining);
        }

        [TestMethod]
        public void Delete_UnknownState_ReturnsFalseAndDoesNotMutate()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(1, "Only"));

            bool removed = sm.Delete("nope");

            Assert.IsFalse(removed);
            Assert.AreEqual(1, sm.GetSaves().Count);
        }

        [TestMethod]
        public void Delete_PersistsToDisk()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(1, "Persisted"));
            sm.Delete("Persisted");

            ResetSingleton();
            var reloaded = StateManager.StateManager.Get(_stateFile);
            Assert.AreEqual(0, reloaded.GetSaves().Count);
        }

        [TestMethod]
        public void Find_ReturnsState_OrNullWhenAbsent()
        {
            var sm = StateManager.StateManager.Get(_stateFile);
            sm.Save(BuildState(42, "Hit"));

            Assert.IsNotNull(sm.Find("Hit"));
            Assert.AreEqual(GuidFromInt(42), sm.Find("Hit")!.Id);
            Assert.IsNull(sm.Find("Miss"));
        }
    }
}
