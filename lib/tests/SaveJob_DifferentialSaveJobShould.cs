using Microsoft.VisualStudio.TestTools.UnitTesting;
using Job;

namespace EasySaveLibrary.Tests
{
    [TestClass]
    public class SaveJob_DifferentialSaveJobShould
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
        public void Execute_DestinationMissing_FallsBackToFullCopy()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "dst.txt");
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("differential");
            File.WriteAllBytes(src, payload);

            var job = new DifferentialSaveFileJob(src, dst, dst + ".diff", payload.Length, Priority.Low);
            long copied = job.Execute();

            Assert.AreEqual(payload.Length, copied);
            Assert.IsTrue(File.Exists(dst), "Destination must be created when missing");
            Assert.IsFalse(File.Exists(dst + ".diff"), "Diff file must not be produced when there is no prior dest");
        }

        [TestMethod]
        public void Execute_IdenticalFiles_ProducesNoDiff()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "dst.txt");
            byte[] payload = System.Text.Encoding.UTF8.GetBytes("same content");
            File.WriteAllBytes(src, payload);
            File.WriteAllBytes(dst, payload);

            var job = new DifferentialSaveFileJob(src, dst, dst + ".diff", payload.Length, Priority.Low);
            long reported = job.Execute();

            Assert.AreEqual(payload.Length, reported, "Reported size should equal declared FileSize when up-to-date");
            Assert.IsFalse(File.Exists(dst + ".diff"), "No diff should be produced for identical files");
        }

        [TestMethod]
        public void Execute_DifferingFiles_ProducesDiffFile()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "dst.txt");
            File.WriteAllText(dst, new string('A', 256));
            File.WriteAllText(src, new string('A', 128) + new string('B', 128));

            var job = new DifferentialSaveFileJob(src, dst, dst + ".diff", new FileInfo(src).Length, Priority.Low);
            job.Execute();

            string diff = dst + ".diff";
            Assert.IsTrue(File.Exists(diff), "A .diff file must be generated for differing content");
            Assert.IsGreaterThan(0, new FileInfo(diff).Length, "Diff file must not be empty");
        }

        [TestMethod]
        public void Execute_DifferingFiles_UpdatesDestinationAndWritesDiffSidecar()
        {
            string src = Path.Combine(_workDir, "src.txt");
            string dst = Path.Combine(_workDir, "dst.txt");
            string original = new('A', 256);
            string updated = new string('A', 128) + new string('B', 128);
            File.WriteAllText(dst, original);
            File.WriteAllText(src, updated);

            var job = new DifferentialSaveFileJob(src, dst, dst + ".diff", new FileInfo(src).Length, Priority.Low);
            job.Execute();

            Assert.AreEqual(original, File.ReadAllText(dst), "Destination must not mirror the new source content after a differential save, differences are saved to a .diff file");
            Assert.IsTrue(File.Exists(dst + ".diff"), "A .diff sidecar must be produced so the prior version is restorable");
        }

        [TestMethod]
        public void GenerateDelta_DiffStartsWithMagicHeader()
        {
            byte[] dstBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
            byte[] srcBytes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 99, 99, 99, 99];

            byte[] diffBytes = XDelta.XDelta.Encode(srcBytes, dstBytes);

            Assert.IsGreaterThanOrEqualTo(4, diffBytes.Length, "Diff file must contain at least the magic header");
            int magic = BitConverter.ToInt32(diffBytes, 0);
            Assert.AreEqual(0x46464944, magic, "Magic header should be \"DIFF\"");
        }

        [TestMethod]
        public void GenerateDelta_TinyFiles_EmitSingleAddOp()
        {
            byte[] srcBytes =[9, 9, 9];
            byte[] dstBytes =[1];

            byte[] diffBytes = XDelta.XDelta.Encode(srcBytes, dstBytes);

            using var ms = new MemoryStream(diffBytes);
            using var reader = new BinaryReader(ms);
            int magic = reader.ReadInt32();
            int oldLen = reader.ReadInt32();
            int newLen = reader.ReadInt32();
            int opCount = reader.ReadInt32();

            Assert.AreEqual(0x46464944, magic);
            Assert.AreEqual(1, oldLen);
            Assert.AreEqual(3, newLen);
            Assert.AreEqual(1, opCount, "Tiny files take the degenerate one-Add path");
            Assert.AreEqual((byte)1, reader.ReadByte()); // Add tag
            Assert.AreEqual(3, reader.ReadInt32());      // length
            CollectionAssert.AreEqual(new byte[] { 9, 9, 9 }, reader.ReadBytes(3));
        }

        [TestMethod]
        public void GenerateDelta_IdenticalLargeBuffers_EmitSingleCopyOp()
        {
            byte[] payload = new byte[1024];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

            byte[] diffBytes = XDelta.XDelta.Encode(payload, payload);

            using var ms = new MemoryStream(diffBytes);
            using var reader = new BinaryReader(ms);
            reader.ReadInt32(); // magic
            reader.ReadInt32(); // oldLen
            reader.ReadInt32(); // newLen
            int opCount = reader.ReadInt32();

            Assert.AreEqual(1, opCount, "Identical buffers must collapse to a single Copy op");
            Assert.AreEqual((byte)0, reader.ReadByte(), "Op tag must be Copy");
            Assert.AreEqual(0, reader.ReadInt32(), "Copy must reference offset 0");
            Assert.AreEqual(payload.Length, reader.ReadInt32(), "Copy must span the full payload");
        }
    }
}
