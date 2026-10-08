using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    /// <summary>
    /// The save file on disk: atomic replace with a backup, recovery from a damaged file, clamped values. Every test
    /// works in a throwaway folder under the project's Temp/, never persistentDataPath (the player's real save).
    /// </summary>
    public sealed class SaveFileTests
    {
        string dir;

        [SetUp]
        public void MakeDir()
        {
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "savetests", System.Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(dir);
            Assert.AreNotEqual(Path.GetFullPath(Application.persistentDataPath), dir);
        }

        [TearDown]
        public void RemoveDir()
        {
            if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        string Main => Path.Combine(dir, SaveData.FileName);
        string Bak => Main + ".bak";

        static SaveData WithBest(int best)
        {
            var s = new SaveData();
            s.Stars[0] = 2; s.Plays[0] = 4; s.Best[0] = best;
            return s;
        }

        [Test]
        public void SavesRoundTripAndKeepThePreviousVersionAsBackup()
        {
            WithBest(100).SaveTo(dir);
            Assert.IsTrue(File.Exists(Main));
            Assert.IsFalse(File.Exists(Bak), "the first save has nothing to back up");
            WithBest(200).SaveTo(dir);
            WithBest(300).SaveTo(dir);
            Assert.AreEqual(300, SaveData.LoadFrom(dir).Best[0]);
            Assert.AreEqual(200, JsonUtility.FromJson<SaveData>(File.ReadAllText(Bak)).Best[0], "the backup is the save before the last one");
            Assert.IsFalse(File.Exists(Main + ".tmp"), "no temp file is left behind");
            Assert.AreEqual(2, SaveData.LoadFrom(dir).Stars[0]);
        }

        [Test]
        public void ATruncatedSaveLoadsTheBackupAndIsKeptAside()
        {
            WithBest(200).SaveTo(dir);
            WithBest(300).SaveTo(dir);
            // a crash halfway through writing the old way: the start of a valid file
            string full = File.ReadAllText(Main);
            File.WriteAllText(Main, full.Substring(0, full.Length / 2));
            var s = SaveData.LoadFrom(dir);
            Assert.AreEqual(200, s.Best[0], "fell back to the backup");
            Assert.IsFalse(File.Exists(Main), "the broken file was moved aside");
            Assert.AreEqual(1, Directory.GetFiles(dir, "save.unreadable-*.json").Length);
            // the next save mustn't push the good backup out
            s.SaveTo(dir);
            Assert.AreEqual(200, SaveData.LoadFrom(dir).Best[0]);
            Assert.AreEqual(200, JsonUtility.FromJson<SaveData>(File.ReadAllText(Bak)).Best[0]);
        }

        [Test]
        public void GarbageWithNoBackupStartsFreshAndKeepsTheGarbage()
        {
            File.WriteAllText(Main, "\0\0\0 not json");
            var s = SaveData.LoadFrom(dir);
            Assert.AreEqual(0, s.Best[0]);
            Assert.AreEqual(16, s.Stars.Length);
            var aside = Directory.GetFiles(dir, "save.unreadable-*.json");
            Assert.AreEqual(1, aside.Length);
            Assert.AreEqual("\0\0\0 not json", File.ReadAllText(aside[0]));
        }

        [Test]
        public void AnEmptyFileCountsAsUnreadable()
        {
            WithBest(200).SaveTo(dir);
            WithBest(300).SaveTo(dir);
            File.WriteAllText(Main, "");
            Assert.AreEqual(200, SaveData.LoadFrom(dir).Best[0]);
        }

        [Test]
        public void NoFileStartsFresh()
        {
            var s = SaveData.LoadFrom(dir);
            Assert.AreEqual(0, s.TotalStars());
            Assert.AreEqual(0, Directory.GetFiles(dir).Length, "loading writes nothing");
        }

        [Test]
        public void OutOfRangeValuesAreClamped()
        {
            File.WriteAllText(Main, "{\"Version\":1,\"Stars\":[9,-2,3],\"Best\":[-50],\"Plays\":[-1],\"Master\":4.0,\"Music\":-1.0,\"Zoom\":2.0,\"DailyBest\":-7,\"Graphics\":9}");
            var s = SaveData.LoadFrom(dir);
            Assert.AreEqual(3, s.Stars[0]);
            Assert.AreEqual(0, s.Stars[1]);
            Assert.AreEqual(3, s.Stars[2]);
            Assert.AreEqual(16, s.Stars.Length, "short arrays are padded");
            Assert.AreEqual(0, s.Best[0]);
            Assert.AreEqual(0, s.Plays[0]);
            Assert.AreEqual(1f, s.Master);
            Assert.AreEqual(0f, s.Music);
            Assert.AreEqual(1f, s.Zoom);
            Assert.AreEqual(0, s.DailyBest);
            Assert.AreEqual(GraphicsQuality.Low, s.Graphics);
            Assert.IsNotNull(s.SeenHints);
        }

        [Test]
        public void GraphicsLevelsRoundTripAndAnOldSaveIsHigh()
        {
            File.WriteAllText(Main, "{\"Version\":1,\"Master\":0.5}");   // a save from before the setting existed
            Assert.AreEqual(GraphicsQuality.High, SaveData.LoadFrom(dir).Graphics);
            var s = new SaveData { Graphics = GraphicsQuality.Balanced };
            s.SaveTo(dir);
            Assert.AreEqual(GraphicsQuality.Balanced, SaveData.LoadFrom(dir).Graphics);
            File.WriteAllText(Main, "{\"Version\":1,\"Graphics\":-4}");
            Assert.AreEqual(GraphicsQuality.High, SaveData.LoadFrom(dir).Graphics);
        }

        [Test]
        public void MuteInBackgroundRoundTripsAndAnOldSaveHasItOn()
        {
            File.WriteAllText(Main, "{\"Version\":1,\"Master\":0.5,\"Fullscreen\":true}");   // a save from before the setting existed
            var old = SaveData.LoadFrom(dir);
            Assert.IsTrue(old.MuteInBackground, "on unless the player turned it off");
            Assert.IsTrue(old.Fullscreen);
            Assert.AreEqual(0.5f, old.Master);
            Assert.IsTrue(new SaveData().MuteInBackground);
            new SaveData { MuteInBackground = false }.SaveTo(dir);
            Assert.IsFalse(SaveData.LoadFrom(dir).MuteInBackground);
            new SaveData { MuteInBackground = true }.SaveTo(dir);
            Assert.IsTrue(SaveData.LoadFrom(dir).MuteInBackground);
        }

        [Test]
        public void GraphicsPresetsOnlyGetCheaperFromHighToLow()
        {
            var high = GraphicsQuality.For(GraphicsQuality.High);
            Assert.AreEqual(4, high.Msaa, "HIGH is the look the game shipped with");
            Assert.AreEqual(4096, high.ShadowRes);
            Assert.AreEqual(1f, high.RenderScale);
            Assert.IsTrue(high.SoftShadows && high.Bloom);
            Assert.AreEqual(GraphicsQuality.Names.Length, GraphicsQuality.Low + 1);
            for (int i = GraphicsQuality.High; i < GraphicsQuality.Low; i++)
            {
                GraphicsQuality.Preset a = GraphicsQuality.For(i), b = GraphicsQuality.For(i + 1);
                Assert.LessOrEqual(b.Msaa, a.Msaa);
                Assert.LessOrEqual(b.ShadowRes, a.ShadowRes);
                Assert.LessOrEqual(b.RenderScale, a.RenderScale);
            }
        }
    }
}
