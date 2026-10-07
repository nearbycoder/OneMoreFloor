using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Self-test: measures every visible text on the UI canvas as it's drawn. For each one, the height of its capitals
    /// in screen pixels (from the font's cap line, the size it was actually laid out at and every scale above it) and
    /// how far its text runs out of its box. Valve asks for text at least 9 px high on a Steam Deck's 1280x800 screen.
    /// </summary>
    public static class TextAudit
    {
        public struct Entry
        {
            public string Name, Text;
            public float CapPx, SpillPx;
            /// <summary>The text floor is drawing it bigger than its code asked for.</summary>
            public bool Raised;
        }

        /// <summary>Visible texts under <paramref name="root"/>: active, with characters, and at least half opaque.</summary>
        public static List<Entry> Measure(Transform root, Camera cam)
        {
            var list = new List<Entry>();
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(false))
            {
                if (!t.isActiveAndEnabled || string.IsNullOrWhiteSpace(t.text)) continue;
                if (Opacity(t) < 0.25f) continue;   // dimmed text (locked roster cards) is still read
                var info = t.textInfo;
                if (info == null || info.characterCount == 0) continue;
                // the smallest capitals actually laid out (auto-sizing and <size> tags included)
                float cap = float.MaxValue;
                for (int i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i];
                    if (!c.isVisible || c.fontAsset == null) continue;
                    var face = c.fontAsset.faceInfo;
                    cap = Mathf.Min(cap, c.pointSize * face.capLine / face.pointSize);
                }
                if (cap == float.MaxValue) continue;
                var rt = t.rectTransform;
                float perUnit = PixelsPerUnit(rt, cam);
                // spill: the laid-out text past its box, sideways (and downward when it wraps)
                var b = t.textBounds;
                var r = rt.rect;
                float spill = Mathf.Max(0f, Mathf.Max(r.xMin - b.min.x, b.max.x - r.xMax));
                if (t.textWrappingMode != TextWrappingModes.NoWrap) spill = Mathf.Max(spill, r.yMin - b.min.y);
                if (t.isTextTruncated) spill = Mathf.Max(spill, 99f);
                // and past the card or panel it sits on, when its box is wider than that (a parent with an image)
                if (rt.parent is RectTransform pr && pr.GetComponent<UnityEngine.UI.Image>() != null && pr.rect.width > 1f)
                {
                    Vector2 lo = pr.InverseTransformPoint(rt.TransformPoint(b.min)), hi = pr.InverseTransformPoint(rt.TransformPoint(b.max));
                    var p = pr.rect;
                    spill = Mathf.Max(spill, Mathf.Max(p.xMin - Mathf.Min(lo.x, hi.x), Mathf.Max(lo.x, hi.x) - p.xMax) * PixelsPerUnit(pr, cam) / perUnit);
                }
                list.Add(new Entry { Name = PathOf(rt), Text = Clip(t.GetParsedText()), CapPx = cap * perUnit, SpillPx = spill * perUnit, Raised = TextFloor.Raised(t) });
            }
            return list;
        }

        /// <summary>One log line per screen: the smallest capitals, every text under the floor, and every text that spills.</summary>
        public static string Report(string screen, List<Entry> entries, float floorPx, out float smallest, out int under, out int spills)
        {
            smallest = float.MaxValue;
            under = spills = 0;
            int raised = 0;
            var small = new List<Entry>();
            var spilled = new List<Entry>();
            foreach (var e in entries)
            {
                smallest = Mathf.Min(smallest, e.CapPx);
                if (e.CapPx < floorPx - 0.05f) small.Add(e);
                if (e.SpillPx > 2f) spilled.Add(e);
                if (e.Raised) raised++;
            }
            under = small.Count;
            spills = spilled.Count;
            small.Sort((a, c) => a.CapPx.CompareTo(c.CapPx));
            var sb = new System.Text.StringBuilder();
            sb.Append($"{screen}: {entries.Count} texts, smallest capitals {(entries.Count > 0 ? smallest : 0f):0.0} px, {under} under {floorPx:0} px, {spills} spilling, {raised} drawn bigger by the text floor");
            foreach (var e in small) sb.Append($"\n    small {e.CapPx:0.0} px  {e.Name}  '{e.Text}'");
            foreach (var e in spilled) sb.Append($"\n    spill {e.SpillPx:0.0} px  {e.Name}  '{e.Text}'");
            if (raised <= 6) foreach (var e in entries) if (e.Raised) sb.Append($"\n    bigger {e.CapPx:0.0} px  {e.Name}  '{e.Text}'");
            return sb.ToString();
        }

        static float Opacity(TMP_Text t)
        {
            float a = t.color.a * t.alpha;
            for (var p = t.transform; p != null; p = p.parent)
            {
                var g = p.GetComponent<CanvasGroup>();
                if (g == null) continue;
                a *= g.alpha;
                if (g.ignoreParentGroups) break;
            }
            return a;
        }

        static float PixelsPerUnit(RectTransform rt, Camera cam)
        {
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(Vector3.zero));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(Vector3.up));
            return (b - a).magnitude;
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            for (int i = 0; i < 2 && t.parent != null; i++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static string Clip(string s)
        {
            s = s.Replace("\n", " / ");
            return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
        }
    }
}
