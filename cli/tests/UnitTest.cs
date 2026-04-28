using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using EasySaveConsole;
using System.Collections.Generic;
using System;
using System.IO;

namespace EasySaveConsole.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class SaveInfosContextTests
    {
        private List<SaveInfo> _existingSaves = new();

        [TestInitialize]
        public void Setup()
        {
            _existingSaves.Clear();
            _existingSaves.Add(new SaveInfo { SaveId = 1, SaveName = "Save1", SourcePath = "C:\\src1", DestinationPath = "D:\\dst1" });
            _existingSaves.Add(new SaveInfo { SaveId = 2, SaveName = "Save2", SourcePath = "C:\\src2", DestinationPath = "D:\\dst2" });
        }

        [TestMethod]
        public void SaveInfosContext_ValidExistingIds_ShouldReturnExistingSaves()
        {
            var userInputs = new List<int> { 1, 2 };
            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                var result = App.SaveInfosContext(userInputs, _existingSaves);

                Assert.AreEqual(2, result.Length);
                Assert.AreEqual("Save1", result[0].SaveName);
                Assert.AreEqual("Save2", result[1].SaveName);
            }
            finally
            {
                RestoreConsole();
            }
        }

        [TestMethod]
        public void SaveInfosContext_InvalidIds_ShouldPromptUntilValid()
        {
            var userInputs = new List<int> { 0, 6 };

            var consoleInput = new StringReader("1\n2\n");
            Console.SetIn(consoleInput);

            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                var result = App.SaveInfosContext(userInputs, _existingSaves);

                Assert.AreEqual(2, result.Length);
                Assert.AreEqual("Save1", result[0].SaveName);
                Assert.AreEqual("Save2", result[1].SaveName);
            }
            finally
            {
                RestoreConsole();
            }
        }

        [TestMethod]
        public void SaveInfosContext_NewSaveId_ShouldPromptForDetailsAndCreateSave()
        {
            var userInputs = new List<int> { 3 };

            var simulatedInput = "NouvelleSave\nC:\\NouveauDossier\\Src\nD:\\NouveauDossier\\Dst\n";
            var consoleInput = new StringReader(simulatedInput);
            Console.SetIn(consoleInput);

            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                var result = App.SaveInfosContext(userInputs, _existingSaves);

                Assert.AreEqual(1, result.Length);
                Assert.AreEqual(3u, result[0].SaveId);
                Assert.AreEqual("NouvelleSave", result[0].SaveName);
            }
            finally
            {
                RestoreConsole();
            }
        }

        [TestMethod]
        public void SaveInfosContext_EmptyInputsWhileCreating_ShouldPromptUntilNotEmpty()
        {
            var userInputs = new List<int> { 3 };

            var simulatedInput = "   \n\nValidName\n\nC:\\Src\nD:\\Dst\n";
            var consoleInput = new StringReader(simulatedInput);
            Console.SetIn(consoleInput);

            var consoleOutput = new StringWriter();
            Console.SetOut(consoleOutput);

            try
            {
                var result = App.SaveInfosContext(userInputs, _existingSaves);

                Assert.AreEqual("ValidName", result[0].SaveName);
                Assert.AreEqual("C:\\Src", result[0].SourcePath);
            }
            finally
            {
                RestoreConsole();
            }
        }

        private void RestoreConsole()
        {
            var standardIn = new StreamReader(Console.OpenStandardInput());
            Console.SetIn(standardIn);

            var standardOut = new StreamWriter(Console.OpenStandardOutput());
            standardOut.AutoFlush = true;
            Console.SetOut(standardOut);
        }
    }
}