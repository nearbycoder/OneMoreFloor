using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Keeps text readable on small screens. The UI is laid out for 1920x1080 and the canvas scales it down on smaller
    /// screens (to about 0.71 on a Steam Deck's 1280x800), which took the smallest labels down to 7 px capitals. Every
    /// text made by <see cref="UiKit.Text"/> is tracked here, and any whose capitals would come out under
    /// <see cref="MinCapPx"/> on screen is drawn bigger, auto-sized ones by raising their minimum. At 1920x1080 and
    /// up nothing changes: the smallest text there has 10 px capitals.
    /// Code that changes a text's size later should use <see cref="SetSize"/>, so the floor applies at once (before
    /// anything measures the text).
    /// </summary>
    public sealed class TextFloor : MonoBehaviour
    {
        /// <summary>Smallest capitals any text may have, in screen pixels (Valve's guidance for the Steam Deck is 9).</summary>
        public const float MinCapPx = 9f;
        /// <summary>The canvas's reference size and width/height match (GameRoot.BuildUi's CanvasScaler).</summary>
        public static readonly Vector2 Reference = new Vector2(1920, 1080);
        public const float Match = 0.6f;

        sealed class Entry
        {
            public TMP_Text T;
            public float Size, Min, Max;            // what the code asked for
            public float SetSize, SetMin, SetMax;   // what was last applied here
        }

        static readonly List<Entry> entries = new List<Entry>();
        static readonly Dictionary<TMP_Text, Entry> byText = new Dictionary<TMP_Text, Entry>();
        static float scale = -1f;

        /// <summary>The canvas scale for a screen size, as the CanvasScaler works it out (log-lerp of width and height).</summary>
        public static float ScaleFor(float width, float height)
        {
            float lw = Mathf.Log(width / Reference.x, 2f), lh = Mathf.Log(height / Reference.y, 2f);
            return Mathf.Pow(2f, Mathf.Lerp(lw, lh, Match));
        }

        /// <summary>The smallest font size (canvas units) that gives <see cref="MinCapPx"/> capitals in this font at this scale.</summary>
        public static float FloorFor(TMP_FontAsset font, float canvasScale)
        {
            if (font == null || canvasScale <= 0f) return 0f;
            var face = font.faceInfo;
            float capPerUnit = face.pointSize > 0f && face.capLine > 0f ? face.capLine / face.pointSize : 0.72f;
            return MinCapPx / (canvasScale * capPerUnit);
        }

        static float Scale => scale > 0f ? scale : (scale = ScaleFor(Screen.width, Screen.height));

        public static void Track(TMP_Text t)
        {
            if (t == null || byText.ContainsKey(t)) return;
            var e = new Entry { T = t, Size = t.fontSize, Min = t.fontSizeMin, Max = t.fontSizeMax };
            entries.Add(e);
            byText[t] = e;
            Apply(e, Scale);
        }

        /// <summary>Applies the floor to a text now (after making it auto-sized, say) instead of at the end of the frame.</summary>
        public static void Refresh(TMP_Text t)
        {
            if (t != null && byText.TryGetValue(t, out var e)) Apply(e, Scale);
        }

        /// <summary>
        /// Whether the floor is drawing this text bigger than its code asked for (a small screen): the cue for a label that
        /// would no longer fit to use shorter words or wrap.
        /// </summary>
        public static bool Raised(TMP_Text t) =>
            t != null && byText.TryGetValue(t, out var e)
            // an auto-sized text only counts when it sits on the raised minimum (it would have gone smaller without it)
            && (t.enableAutoSizing ? t.fontSizeMin > e.Min + 0.05f && t.fontSize <= t.fontSizeMin + 0.05f : t.fontSize > e.Size + 0.05f);

        /// <summary>Sets a tracked text's size, with the floor applied straight away.</summary>
        public static void SetSize(TMP_Text t, float size)
        {
            t.fontSize = size;
            if (!byText.TryGetValue(t, out var e)) { Track(t); return; }
            e.Size = size;
            Apply(e, Scale);
        }

        static void Apply(Entry e, float s)
        {
            var t = e.T;
            // the code changed something since the last pass: that's the new request
            if (!t.enableAutoSizing && !Mathf.Approximately(t.fontSize, e.SetSize)) e.Size = t.fontSize;
            if (!Mathf.Approximately(t.fontSizeMin, e.SetMin)) e.Min = t.fontSizeMin;
            if (!Mathf.Approximately(t.fontSizeMax, e.SetMax)) e.Max = t.fontSizeMax;
            float floor = FloorFor(t.font, s);
            if (t.enableAutoSizing)
            {
                float min = Mathf.Max(e.Min, floor), max = Mathf.Max(e.Max, min);
                if (!Mathf.Approximately(t.fontSizeMin, min)) t.fontSizeMin = min;
                if (!Mathf.Approximately(t.fontSizeMax, max)) t.fontSizeMax = max;
            }
            else
            {
                float size = Mathf.Max(e.Size, floor);
                if (!Mathf.Approximately(t.fontSize, size)) t.fontSize = size;
            }
            e.SetSize = t.fontSize;
            e.SetMin = t.fontSizeMin;
            e.SetMax = t.fontSizeMax;
        }

        void LateUpdate() => ApplyAll(ScaleFor(Screen.width, Screen.height));

        /// <summary>Re-applies the floor to every tracked text for a canvas scale (each frame, and from tests).</summary>
        public static void ApplyAll(float canvasScale)
        {
            scale = canvasScale;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                if (e.T == null)
                {
                    entries.RemoveAt(i);
                    byText.Remove(e.T);
                    continue;
                }
                Apply(e, scale);
            }
        }
    }
}
