using NUnit.Framework;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;

namespace OneMoreFloor.Tests
{
    public class LargerTextTests
    {
        [Test]
        public void LargerTextRaisesTheFloorEverywhereAndOffPutsItBack()
        {
            var root = new GameObject("LargerTextTest");
            try
            {
                var t = UiKit.Text("T", root.transform, "SHIFT ENDS IN", 15, Color.white, UiKit.Signage);
                t.characterSpacing = 6f;
                TextFloor.Large = false;
                TextFloor.ApplyAll(1f);
                Assert.AreEqual(15f, t.fontSize, 0.01f, "1920x1080, default: as asked");
                TextFloor.Large = true;
                TextFloor.ApplyAll(1f);
                Assert.AreEqual(TextFloor.FloorFor(UiKit.Signage, 1f), t.fontSize, 0.01f, "1920x1080, larger text");
                Assert.AreEqual(TextFloor.LargeCapPx, t.fontSize * t.font.faceInfo.capLine / t.font.faceInfo.pointSize, 0.01f);
                Assert.AreEqual(6f, t.characterSpacing, 0.01f, "it fits its box, so it keeps its letter spacing");
                TextFloor.Large = false;
                TextFloor.ApplyAll(1f);
                Assert.AreEqual(15f, t.fontSize, 0.01f, "off again");
            }
            finally
            {
                TextFloor.Large = false;
                Object.DestroyImmediate(root);
                TextFloor.ApplyAll(1f);
            }
        }

        [Test]
        public void ALabelTooWideForItsBoxGivesUpLetterSpacingOnlyWithLargerText()
        {
            var root = new GameObject("LargerTextTest");
            try
            {
                float deck = TextFloor.ScaleFor(1280, 800);
                var t = UiKit.Text("T", root.transform, "BEST STREAK", 15, Color.white, UiKit.Signage, TextAlignmentOptions.Center, new Vector2(170, 24));   // the time card's stat label
                t.characterSpacing = 6f;
                TextFloor.Large = false;
                TextFloor.ApplyAll(deck);
                Assert.AreEqual(6f, t.characterSpacing, 0.01f, "the default floor never touches spacing");
                TextFloor.Large = true;
                TextFloor.ApplyAll(deck);
                Assert.Less(t.characterSpacing, 6f);
                Assert.GreaterOrEqual(t.characterSpacing, 0f);
                Assert.LessOrEqual(t.GetPreferredValues(t.text).x, 170f, "squeezed to fit");
                TextFloor.Large = false;
                TextFloor.ApplyAll(deck);
                Assert.AreEqual(6f, t.characterSpacing, 0.01f, "spacing comes back");
            }
            finally
            {
                TextFloor.Large = false;
                Object.DestroyImmediate(root);
                TextFloor.ApplyAll(1f);
            }
        }

        /// <summary>
        /// Every card the forecast can show fits with LARGER TEXT on a Steam Deck (the tightest case): on its one line, or
        /// else on the two lines of the tall card the panel switches to.
        /// </summary>
        [Test]
        public void EveryForecastCardFitsWithLargerTextOnADeck()
        {
            var root = new GameObject("LargerTextTest");
            try
            {
                float deck = TextFloor.ScaleFor(1280, 800);
                TextFloor.Large = true;
                var body = PanelUi.CardBody(root.transform);
                TextFloor.ApplyAll(deck);
                Assert.Greater(body.fontSize, 19.5f, "the floor applies");
                float box = body.rectTransform.rect.width;
                var floors = (FloorId[])System.Enum.GetValues(typeof(FloorId));
                var cards = new System.Collections.Generic.List<Card>();
                foreach (var a in floors)
                {
                    cards.Add(new Card { Type = CardType.Rise, A = a });
                    cards.Add(new Card { Type = CardType.Sink, A = a });
                    foreach (var b in floors) if (a != b) cards.Add(new Card { Type = CardType.Swap, A = a, B = b });
                }
                for (int from = 0; from < 9; from++)
                {
                    cards.Add(new Card { Type = CardType.Roll, From = from, Len = 3, Up = true });
                    cards.Add(new Card { Type = CardType.Roll, From = from, Len = 3, Up = false });
                    cards.Add(new Card { Type = CardType.Flip, From = from, Len = 5 });
                }
                cards.Add(Card.Calm());
                int oneLine = 0, twoLines = 0;
                float tallest = 0f;
                string worst = "";
                foreach (var c in cards)
                {
                    body.text = PanelUi.Describe(c);
                    if (PanelUi.FitsOneLine(body)) { oneLine++; continue; }
                    twoLines++;
                    body.textWrappingMode = TextWrappingModes.Normal;   // as on the tall card
                    float h = body.GetPreferredValues(body.text, box, 0f).y;
                    body.textWrappingMode = TextWrappingModes.NoWrap;
                    if (h > tallest) { tallest = h; worst = body.text; }
                }
                Debug.Log($"[LargerText] at {body.fontSize:0.0} units: {oneLine} cards fit one line, {twoLines} need two (tallest {tallest:0.0} of {PanelUi.TallBodyH})");
                Assert.LessOrEqual(tallest, PanelUi.TallBodyH, worst);
                Assert.Greater(tallest, body.fontSize * 1.5f, "measured over two lines");
            }
            finally
            {
                TextFloor.Large = false;
                Object.DestroyImmediate(root);
                TextFloor.ApplyAll(1f);
            }
        }
    }
}
