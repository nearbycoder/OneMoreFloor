using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class TextFloorTests
    {
        static float Cap(TMP_FontAsset f) => f.faceInfo.capLine / f.faceInfo.pointSize;

        [Test]
        public void CanvasScaleMatchesTheScaler()
        {
            Assert.AreEqual(1f, TextFloor.ScaleFor(1920, 1080), 1e-4f);
            Assert.AreEqual(0.710f, TextFloor.ScaleFor(1280, 800), 0.005f);   // Steam Deck
            Assert.AreEqual(2f, TextFloor.ScaleFor(3840, 2160), 1e-4f);
        }

        [Test]
        public void TheFloorGivesNinePixelCapitalsAndLeavesFullHdAlone()
        {
            foreach (var font in new[] { UiKit.Display, UiKit.Signage, UiKit.Body, UiKit.Hand })
            {
                Assert.IsNotNull(font);
                float deck = TextFloor.ScaleFor(1280, 800);
                Assert.AreEqual(TextFloor.MinCapPx, TextFloor.FloorFor(font, deck) * deck * Cap(font), 0.01f, font.name);
                // the smallest size the UI asks for is 14, so at 1920x1080 and up the floor never applies
                Assert.Less(TextFloor.FloorFor(font, 1f), 14f, font.name);
            }
        }

        [Test]
        public void SmallTextGrowsOnSmallScreensAndCodeChangesAreKept()
        {
            var root = new GameObject("TextFloorTest");
            try
            {
                float deck = TextFloor.ScaleFor(1280, 800);
                var t = UiKit.Text("T", root.transform, "SHIFT ENDS IN", 15, Color.white, UiKit.Signage);
                TextFloor.ApplyAll(deck);
                Assert.AreEqual(TextFloor.FloorFor(UiKit.Signage, deck), t.fontSize, 0.01f);
                Assert.Greater(t.fontSize, 17f);
                TextFloor.ApplyAll(1f);
                Assert.AreEqual(15f, t.fontSize, 0.01f, "back to the asked-for size on a big screen");
                t.fontSize = 30f;   // the code asks for a new size later
                TextFloor.ApplyAll(deck);
                Assert.AreEqual(30f, t.fontSize, 0.01f);
                TextFloor.ApplyAll(1f);
                TextFloor.SetSize(t, 13f);   // applied at once, at the last scale seen
                Assert.AreEqual(Mathf.Max(13f, TextFloor.FloorFor(UiKit.Signage, 1f)), t.fontSize, 0.01f);
                TextFloor.ApplyAll(deck);
                Assert.AreEqual(TextFloor.FloorFor(UiKit.Signage, deck), t.fontSize, 0.01f);

                // auto-sized text: its minimum (and the maximum, if it has to) is raised instead
                var a = UiKit.Text("A", root.transform, "$20,660 TO STAR 1", 15, Color.white, UiKit.Body);
                a.enableAutoSizing = true;
                a.fontSizeMin = 11;
                a.fontSizeMax = 15;
                TextFloor.ApplyAll(deck);
                float floor = TextFloor.FloorFor(UiKit.Body, deck);
                Assert.AreEqual(floor, a.fontSizeMin, 0.01f);
                Assert.AreEqual(floor, a.fontSizeMax, 0.01f);
                TextFloor.ApplyAll(1f);
                Assert.AreEqual(15f, a.fontSizeMax, 0.01f);
                Assert.AreEqual(Mathf.Max(11f, TextFloor.FloorFor(UiKit.Body, 1f)), a.fontSizeMin, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(root);
                TextFloor.ApplyAll(1f);   // drops the destroyed texts
            }
        }
    }
}
