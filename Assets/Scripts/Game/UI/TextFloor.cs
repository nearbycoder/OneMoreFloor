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
    /// up nothing changes: the smallest text there has 10 px capitals. The LARGER TEXT setting raises the floor to
    /// <see cref="LargeCapPx"/> on every screen, and a one-line label it makes too wide for its box gives up some of its
    /// letter spacing (see <see cref="Fit"/> for labels that need shorter words).
    /// Code that changes a text's size later should use <see cref="SetSize"/>, so the floor applies at once (before
    /// anything measures the text).
    /// </summary>
    public sealed class TextFloor : MonoBehaviour
    {
        /// <summary>Smallest capitals any text may have by default, in screen pixels (Valve's guidance for the Steam Deck is 9).</summary>
        public const float DefaultCapPx = 9f;
        /// <summary>The floor with LARGER TEXT on: a fifth bigger than the smallest text at 1920x1080, a third bigger on a Deck.</summary>
        public const float LargeCapPx = 12f;
        /// <summary>The LARGER TEXT setting (applied to every tracked text at the end of the frame).</summary>
        public static bool Large;
        public static float MinCapPx => Large ? LargeCapPx : DefaultCapPx;
        /// <summary>The canvas's reference size and width/height match (GameRoot.BuildUi's CanvasScaler).</summary>
        public static readonly Vector2 Reference = new Vector2(1920, 1080);
        public const float Match = 0.6f;

        sealed class Entry
        {
            public TMP_Text T;
            public float Size, Min, Max;            // what the code asked for
            public float SetSize, SetMin, SetMax;   // what was last applied here
            public float Spacing, SetSpacing;       // letter spacing: asked for, and last applied
            public string FitText;                  // the squeeze below was worked out for this text, size and spacing
            public float FitSize, FitSpacing, Squeezed;
            public bool Ornament;                   // scale marks: the default floor only, never LARGER TEXT's
            public string PickFor, Pick;            // Fit's last answer, for this first wording at this size
            public float PickSize;
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
        public static float FloorFor(TMP_FontAsset font, float canvasScale) => FloorFor(font, canvasScale, MinCapPx);

        static float FloorFor(TMP_FontAsset font, float canvasScale, float capPx)
        {
            if (font == null || canvasScale <= 0f) return 0f;
            var face = font.faceInfo;
            float capPerUnit = face.pointSize > 0f && face.capLine > 0f ? face.capLine / face.pointSize : 0.72f;
            return capPx / (canvasScale * capPerUnit);
        }

        /// <summary>The smallest capitals (px) this text is kept to: <see cref="MinCapPx"/>, or the default for an ornament.</summary>
        public static float CapFloorOf(TMP_Text t) => t != null && byText.TryGetValue(t, out var e) && e.Ornament ? DefaultCapPx : MinCapPx;

        /// <summary>
        /// Marks a text as an ornament that would crowd its neighbours if LARGER TEXT enlarged it (the panel dial's scale
        /// numbers, packed round an arc, while the number under the needle says the same thing): it keeps the default floor.
        /// </summary>
        public static void Ornament(TMP_Text t)
        {
            if (t == null) return;
            if (!byText.TryGetValue(t, out var e)) { Track(t); e = byText[t]; }
            e.Ornament = true;
            Apply(e, Scale);
        }

        static float Scale => scale > 0f ? scale : (scale = ScaleFor(Screen.width, Screen.height));

        public static void Track(TMP_Text t)
        {
            if (t == null || byText.ContainsKey(t)) return;
            var e = new Entry { T = t, Size = t.fontSize, Min = t.fontSizeMin, Max = t.fontSizeMax, Spacing = t.characterSpacing, SetSpacing = t.characterSpacing };
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
            float floor = FloorFor(t.font, s, e.Ornament ? DefaultCapPx : MinCapPx);
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

            // LARGER TEXT: a one-line label the floor made too wide for its box gives up letter spacing (never below 0)
            if (!Mathf.Approximately(t.characterSpacing, e.SetSpacing)) e.Spacing = t.characterSpacing;
            float spacing = e.Spacing;
            bool raised = t.enableAutoSizing ? floor > e.Min + 0.05f : floor > e.Size + 0.05f;
            if (Large && raised && e.Spacing > 0f && t.textWrappingMode == TextWrappingModes.NoWrap && t.isActiveAndEnabled)
            {
                float size = t.enableAutoSizing ? t.fontSizeMin : t.fontSize;
                if (e.FitText != t.text || !Mathf.Approximately(e.FitSize, size) || !Mathf.Approximately(e.FitSpacing, e.Spacing))
                {
                    e.FitText = t.text;
                    e.FitSize = size;
                    e.FitSpacing = e.Spacing;
                    e.Squeezed = SpacingToFit(t, e.Spacing);
                }
                spacing = e.Squeezed;
            }
            if (!Mathf.Approximately(t.characterSpacing, spacing)) t.characterSpacing = spacing;
            e.SetSpacing = t.characterSpacing;
        }

        /// <summary>One-line width of a text at the size it will be drawn (an auto-sized one at its smallest).</summary>
        static float WidthOf(TMP_Text t, string text)
        {
            float w = t.GetPreferredValues(text).x;
            return t.enableAutoSizing && t.fontSizeMax > 0f ? w * t.fontSizeMin / t.fontSizeMax : w;
        }

        /// <summary>The letter spacing (from <paramref name="asked"/> down to 0) at which the text fits its box on one line.</summary>
        static float SpacingToFit(TMP_Text t, float asked)
        {
            float box = t.rectTransform.rect.width - 2f;
            float keep = t.characterSpacing;
            t.characterSpacing = asked;
            float full = WidthOf(t, t.text);
            float result = asked;
            if (full > box)
            {
                t.characterSpacing = 0f;
                float none = WidthOf(t, t.text);
                // spacing adds the same to every character, so the width is linear in it
                result = full - none > 0.01f ? Mathf.Clamp(asked * (box - none) / (full - none), 0f, asked) : asked;
            }
            t.characterSpacing = keep;
            return result;
        }

        /// <summary>
        /// The first of <paramref name="words"/> that fits the text's box on one line at the size it will be drawn (letter
        /// spacing squeezed out if need be): a label with shorter words for when LARGER TEXT leaves no room for the long
        /// ones. The last is the fallback.
        /// </summary>
        public static string Fit(TMP_Text t, params string[] words)
        {
            byText.TryGetValue(t, out var e);
            if (e != null) Apply(e, Scale);
            float size = t.enableAutoSizing ? t.fontSizeMin : t.fontSize;
            if (e != null && e.PickFor == words[0] && Mathf.Approximately(e.PickSize, size)) return e.Pick;
            float box = t.rectTransform.rect.width - 2f, keep = t.characterSpacing;
            t.characterSpacing = 0f;
            string pick = words[words.Length - 1];
            for (int i = 0; i < words.Length - 1; i++)
                if (WidthOf(t, words[i]) <= box) { pick = words[i]; break; }
            t.characterSpacing = keep;
            if (e != null) { e.PickFor = words[0]; e.PickSize = size; e.Pick = pick; }
            return pick;
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
