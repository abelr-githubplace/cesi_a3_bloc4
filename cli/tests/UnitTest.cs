using Microsoft.VisualStudio.TestTools.UnitTesting;
using SaveManager;
using EasySaveConsole;
using System.Collections.Generic;

namespace EasySaveConsole.Tests
{
    [TestClass]
    public class AppTests
    {
        [TestMethod]
        public void SaveInfosContext_ShouldIgnoreZero_And_CorrectlyAlignIndexes()
        {
            var existingSaves = new List<SaveInfo>
            {
                new SaveInfo { SaveId = 1, SaveName = "SauvegardeA", SourcePath = "C:\\A", DestinationPath = "D:\\A" },
                new SaveInfo { SaveId = 2, SaveName = "SauvegardeB", SourcePath = "C:\\B", DestinationPath = "D:\\B" }
            };

            var userInputs = new List<int> { 0, 1, 2 };

            var result = App.SaveInfosContext(userInputs, existingSaves);

            Assert.AreEqual(2, result.Length); 
            Assert.AreEqual("SauvegardeA", result[0].SaveName); 
            Assert.AreEqual("SauvegardeB", result[1].SaveName); 
        }
    }
}