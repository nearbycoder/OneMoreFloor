using NUnit.Framework;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class NavRepeatTests
    {
        static readonly Vector2Int Down = new Vector2Int(0, -1);

        /// <summary>Holds a direction for <paramref name="seconds"/> of frames <paramref name="frame"/> long; returns the steps.</summary>
        static int Hold(ref NavRepeat r, Vector2Int dir, float seconds, float frame = 1f / 60f)
        {
            int steps = 0;
            for (float t = 0f; t < seconds - 1e-4f; t += frame) if (r.Step(dir, frame)) steps++;
            return steps;
        }

        [Test]
        public void HeldKeyRepeatsAfterTheDelayThenQuickly()
        {
            var r = new NavRepeat();
            Assert.AreEqual(1, Hold(ref r, Down, 0.30f), "a short press steps once");
            r = new NavRepeat();
            Assert.AreEqual(2, Hold(ref r, Down, 0.45f), "held past 0.38 s it steps again");
            r = new NavRepeat();
            Assert.AreEqual(4, Hold(ref r, Down, 0.70f), "then every 0.12 s");
        }

        [Test]
        public void AStalledFrameDoesNotCountAsHolding()
        {
            var r = new NavRepeat();
            Assert.IsTrue(r.Step(Down, 1f / 60f), "the press steps");
            Assert.IsFalse(r.Step(Down, 0.5f), "a 0.5 s frame with the key still down");
            Assert.IsFalse(r.Step(Down, 1f / 60f));
            Assert.IsFalse(r.Step(Vector2Int.zero, 1f / 60f), "released");
            Assert.IsTrue(r.Step(Down, 1f / 60f), "pressed again");
        }

        [Test]
        public void ChangingDirectionStepsAtOnce()
        {
            var r = new NavRepeat();
            Assert.IsTrue(r.Step(Down, 1f / 60f));
            Assert.IsTrue(r.Step(new Vector2Int(1, 0), 1f / 60f));
            Assert.IsFalse(r.Step(new Vector2Int(1, 0), 1f / 60f));
        }
    }
}
