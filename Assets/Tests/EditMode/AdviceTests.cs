using System;
using NUnit.Framework;
using OneMoreFloor.Core;

namespace OneMoreFloor.Tests
{
    public class AdviceTests
    {
        [Test]
        public void EveryComplaintCauseHasWordsAndATip()
        {
            foreach (Outcome o in Enum.GetValues(typeof(Outcome)))
            {
                if (!Advice.IsComplaint(o)) continue;
                Assert.IsNotEmpty(Advice.Label(o, 1), o.ToString());
                Assert.IsNotEmpty(Advice.Label(o, 3), o.ToString());
                Assert.IsNotEmpty(Advice.Tip(o), o.ToString());
            }
            Assert.IsFalse(Advice.IsComplaint(Outcome.Delivered));
            Assert.IsFalse(Advice.IsComplaint(Outcome.WentWithFloor));
        }

        [Test]
        public void SummaryAndBiggestCause()
        {
            var by = new int[Enum.GetValues(typeof(Outcome)).Length];
            Assert.AreEqual("", Advice.Summary(by));
            Assert.IsNull(Advice.Biggest(by));
            by[(int)Outcome.StormedOff] = 2;
            by[(int)Outcome.Poofed] = 3;
            Assert.AreEqual("2 took the stairs  ·  3 vampires met the sun", Advice.Summary(by));
            Assert.AreEqual(Outcome.Poofed, Advice.Biggest(by));
        }

        [Test]
        public void ComplaintCausesAddUpInRealShifts()
        {
            // every complaint the sim raises is one of the listed causes
            foreach (var def in ShiftCatalog.All)
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    var sim = Bot.PlayOut(def, seed * 101, Bot.Human(seed, 0f));
                    int sum = 0;
                    foreach (var o in Advice.Causes) sum += sim.ComplaintsBy[(int)o];
                    Assert.AreEqual(sim.Complaints, sum, $"{def.Id} seed {seed}");
                }
        }
    }
}
