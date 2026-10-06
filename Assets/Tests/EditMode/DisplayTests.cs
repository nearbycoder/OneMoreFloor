using NUnit.Framework;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class DisplayTests
    {
        [TestCase(3840, 2160, 1600, 900)]
        [TestCase(1920, 1080, 1600, 900)]
        [TestCase(1366, 768, 1229, 691)]
        [TestCase(1280, 800, 1152, 648)]
        [TestCase(1280, 720, 1152, 648)]
        [TestCase(1024, 600, 921, 518)]
        public void WindowFitsTheDisplay(int dw, int dh, int w, int h)
        {
            var s = GameRoot.WindowSize(dw, dh);
            Assert.AreEqual(new Vector2Int(w, h), s);
            Assert.LessOrEqual(s.x, dw * 0.9f + 0.5f);
            Assert.LessOrEqual(s.y, dh * 0.9f + 0.5f);
        }
    }
}
