using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// The Shuffleton's UI look, painted procedurally at startup: dark enamel panels in bevelled brass
    /// frames, gold and enamel buttons, recessed tracks, soft shadows, and gilded display type.
    /// </summary>
    public static class Deco
    {
        public static readonly Color Gold = Palette.Hex(0xF2C66B);
        public static readonly Color GoldLight = Palette.Hex(0xFFE9B0);
        public static readonly Color Muted = Palette.Hex(0xB9A58C);

        static Sprite frame, frameSmall, paper, gold, enamel, mask, shadow, recess, knob, vignette, scrim, diamond;

        /// <summary>Dark enamel panel with a bevelled brass rim and an inset hairline (9-sliced).</summary>
        public static Sprite Frame => frame ? frame : (frame = Paint(176, 176, 30, 56, FrameShade(5f, 12f)));
        /// <summary>Tighter frame for small cards and tooltips.</summary>
        public static Sprite FrameSmall => frameSmall ? frameSmall : (frameSmall = Paint(120, 120, 18, 34, FrameShade(3.5f, 8f)));
        /// <summary>Cream card stock with a thin brass edge.</summary>
        public static Sprite Paper => paper ? paper : (paper = Paint(120, 120, 18, 34, PaperShade));
        public static Sprite GoldFace => gold ? gold : (gold = Paint(128, 128, 22, 40, GoldShade));
        public static Sprite EnamelFace => enamel ? enamel : (enamel = Paint(128, 128, 22, 40, EnamelShade));
        /// <summary>Plain white shape matching the button faces (hover sheen, masks).</summary>
        public static Sprite Mask => mask ? mask : (mask = Paint(128, 128, 22, 40, (d, u, v) => Color.white));
        public static Sprite Shadow => shadow ? shadow : (shadow = Paint(160, 160, 48, 70, ShadowShade, 26f));
        public static Sprite Recess => recess ? recess : (recess = Paint(64, 64, 32, 31, RecessShade));
        /// <summary>A round recessed well (portrait backdrops).</summary>
        public static Sprite Well => well ? well : (well = Paint(128, 128, 64, 0, (d, u, v) =>
        {
            var c = RecessShade(d * 0.5f, u, v);
            c = Color.Lerp(c, BrassAt(v * 0.7f + 0.2f, 1f - Mathf.Abs(d + 2.5f) / 2.5f), Mathf.Clamp01(d + 4.5f));
            return c;
        }));
        static Sprite well;
        public static Sprite GoldPill => goldPill ? goldPill : (goldPill = Paint(64, 64, 32, 31, (d, u, v) =>
        {
            var c = Color.Lerp(Palette.Hex(0xB97D26), Palette.Hex(0xFFDB85), Mathf.Pow(v, 0.9f));
            c = Color.Lerp(c, Palette.Hex(0xFFF4D2), Mathf.Clamp01(1f - Mathf.Abs(v - 0.72f) * 7f) * 0.5f);
            c = Color.Lerp(c, Palette.Hex(0x6A420F), Mathf.Clamp01(d + 1.5f) * 0.7f);
            c.a = 1f;
            return c;
        }));
        static Sprite goldPill;
        public static Sprite Knob => knob ? knob : (knob = Paint(96, 96, 48, 0, KnobShade));
        /// <summary>A rubber-stamp border: a heavy outer ring and a thin inner ring, white for tinting.</summary>
        public static Sprite StampRing => stampRing ? stampRing : (stampRing = Paint(96, 96, 16, 30, (d, u, v) =>
        {
            float a = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(d + 3.5f) / 3f), Mathf.Clamp01(1.2f - Mathf.Abs(d + 10.5f)));
            float grain = Mathf.PerlinNoise(u * 23f, v * 23f) * 0.3f + 0.7f;
            return new Color(1, 1, 1, a * grain);
        }));
        static Sprite stampRing;
        public static Sprite Diamond => diamond ? diamond : (diamond = MakeDiamond(48));
        public static Sprite Vignette => vignette ? vignette : (vignette = MakeVignette(128));
        public static Sprite Scrim => scrim ? scrim : (scrim = MakeScrim(256));

        // ------------------------------------------------------------------ painters

        delegate Color Shade(float d, float u, float v);

        /// <summary>Paint a rounded rect: <paramref name="shade"/> gets the signed distance to the edge (negative inside) and uv.</summary>
        static Sprite Paint(int w, int h, float radius, int border, Shade shade, float outset = 0f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var half = new Vector2(w * 0.5f - outset, h * 0.5f - outset);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2(x + 0.5f - w * 0.5f, y + 0.5f - h * 0.5f);
                    float d = RoundedDist(p, half, radius);
                    var c = shade(d, (x + 0.5f) / w, (y + 0.5f) / h);
                    if (outset <= 0f) c.a *= Mathf.Clamp01(0.5f - d);
                    px[y * w + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        static float RoundedDist(Vector2 p, Vector2 half, float r)
        {
            var q = new Vector2(Mathf.Abs(p.x) - (half.x - r), Mathf.Abs(p.y) - (half.y - r));
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        /// <summary>Polished brass: dark at the bottom, bright at the top, with a ridge highlight.</summary>
        public static Color BrassAt(float v, float ridge)
        {
            var c = Color.Lerp(Palette.Hex(0x7A4C14), Palette.Hex(0xF7D88C), Mathf.Clamp01(v));
            c = Color.Lerp(c, Palette.Hex(0xFFF4D0), Mathf.Clamp01(ridge) * 0.45f);
            c.a = 1f;
            return c;
        }

        static Shade FrameShade(float rim, float hairline) => (d, u, v) =>
        {
            float t = Mathf.Clamp01((d + rim) / rim);
            float ridge = 1f - Mathf.Abs(t - 0.4f) * 2.4f;
            var brass = BrassAt(v * 0.75f + 0.2f, ridge);
            brass = Color.Lerp(brass, Palette.Hex(0x4A2E0C), Mathf.Clamp01((t - 0.8f) * 4f) * 0.6f);
            var enamel = Color.Lerp(Palette.Hex(0x170F1B), Palette.Hex(0x35253D), Mathf.Pow(v, 1.4f));
            enamel = Color.Lerp(enamel, Palette.Hex(0x4A3652), Mathf.Clamp01((v - 0.86f) * 6f) * 0.35f);
            var c = enamel;
            float line = Mathf.Clamp01(1.1f - Mathf.Abs(d + hairline));
            c = Color.Lerp(c, BrassAt(v * 0.6f + 0.3f, 0.2f), line * 0.75f);
            c = Color.Lerp(c, Palette.Hex(0x0C070E), Mathf.Clamp01(1f - Mathf.Abs(d + rim + 1f) / 1.2f));
            c = Color.Lerp(c, brass, Mathf.Clamp01(d + rim + 0.5f));
            c.a = 1f;
            return c;
        };

        static Color PaperShade(float d, float u, float v)
        {
            var paperC = Color.Lerp(Palette.Hex(0xEADCBF), Palette.Hex(0xFBF3E2), v);
            var c = paperC;
            float line = Mathf.Clamp01(1.1f - Mathf.Abs(d + 7f));
            c = Color.Lerp(c, Palette.Hex(0xC59A4E), line * 0.55f);
            c = Color.Lerp(c, BrassAt(v * 0.7f + 0.2f, 0.3f), Mathf.Clamp01(d + 3f + 0.5f));
            c.a = 1f;
            return c;
        }

        static Color GoldShade(float d, float u, float v)
        {
            var face = Color.Lerp(Palette.Hex(0xC2862C), Palette.Hex(0xFFD877), Mathf.Pow(v, 0.8f));
            float spec = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.72f, v)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 0.97f, v)));
            face = Color.Lerp(face, Palette.Hex(0xFFF2C8), spec * 0.35f);
            var c = face;
            c = Color.Lerp(c, Palette.Hex(0x9C6A22), Mathf.Clamp01(1.0f - Mathf.Abs(d + 8f)) * 0.55f);
            c = Color.Lerp(c, Palette.Hex(0xFFF6D6), Mathf.Clamp01(1.0f - Mathf.Abs(d + 3.8f)) * Mathf.Lerp(0.15f, 0.9f, v));
            c = Color.Lerp(c, Color.Lerp(Palette.Hex(0x4A2C0A), Palette.Hex(0xA87430), v), Mathf.Clamp01(d + 3f + 0.5f));
            c.a = 1f;
            return c;
        }

        static Color EnamelShade(float d, float u, float v)
        {
            var face = Color.Lerp(Palette.Hex(0x1D1424), Palette.Hex(0x3D2B47), Mathf.Pow(v, 1.2f));
            var c = face;
            c = Color.Lerp(c, BrassAt(v * 0.6f + 0.3f, 0.1f), Mathf.Clamp01(1.0f - Mathf.Abs(d + 7.5f)) * 0.5f);
            c = Color.Lerp(c, Palette.Hex(0x0C070E), Mathf.Clamp01(1.0f - Mathf.Abs(d + 4f)));
            float t = Mathf.Clamp01((d + 3f) / 3f);
            c = Color.Lerp(c, BrassAt(v * 0.75f + 0.2f, 1f - Mathf.Abs(t - 0.4f) * 2.4f), Mathf.Clamp01(d + 3f + 0.5f));
            c.a = 1f;
            return c;
        }

        static Color ShadowShade(float d, float u, float v)
        {
            float k = Mathf.Clamp01(1f - (d + 6f) / 30f);
            return new Color(0.03f, 0.01f, 0.04f, k * k * (3f - 2f * k) * 0.7f);
        }

        static Color RecessShade(float d, float u, float v)
        {
            var c = Color.Lerp(Palette.Hex(0x09050B), Palette.Hex(0x22172A), Mathf.Pow(1f - v, 1.5f) * 0.4f + Mathf.Clamp01(-d / 14f) * 0.6f);
            c = Color.Lerp(c, BrassAt(0.5f, 0.3f), Mathf.Clamp01(d + 2f) * Mathf.Lerp(0.85f, 0.25f, v));
            c.a = 1f;
            return c;
        }

        static Color KnobShade(float d, float u, float v)
        {
            var n = new Vector2(u - 0.5f, v - 0.5f) * 2f;
            float lit = Mathf.Clamp01(0.55f + Vector2.Dot(n, new Vector2(-0.35f, 0.6f)) * 0.7f);
            var c = BrassAt(lit, Mathf.Clamp01(1f - (n - new Vector2(-0.3f, 0.45f)).magnitude * 2.2f));
            c = Color.Lerp(c, Palette.Hex(0x4A2C0A), Mathf.Clamp01(1f - Mathf.Abs(d + 1.5f) / 1.5f) * 0.8f);
            c = Color.Lerp(c, BrassAt(1f - lit, 0f), Mathf.Clamp01(1f - Mathf.Abs(d + 12f) / 1.2f) * 0.5f);
            c.a = 1f;
            return c;
        }

        static Sprite MakeDiamond(int s)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - s * 0.5f), dy = Mathf.Abs(y + 0.5f - s * 0.5f);
                    float d = (dx + dy) - s * 0.46f;
                    var c = BrassAt(1f - (y + 0.5f) / s * 0.6f + (x < s / 2 ? 0.1f : -0.1f), 0.3f);
                    c.a = Mathf.Clamp01(0.5f - d);
                    px[y * s + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeVignette(int s)
        {
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float r = new Vector2((x + 0.5f) / s - 0.5f, (y + 0.5f) / s - 0.5f).magnitude * 2f;
                    float a = Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 1.3f, r)));
                    px[y * s + x] = new Color(1, 1, 1, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeScrim(int w)
        {
            var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * 4];
            for (int x = 0; x < w; x++)
            {
                float k = (x + 0.5f) / w;
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 1f, k));
                for (int y = 0; y < 4; y++) px[y * w + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------ builders

        /// <summary>
        /// A framed enamel panel with a drop shadow: a container holding the shadow then the face, so anything
        /// parented to the returned rect afterwards draws on top of the face.
        /// </summary>
        public static RectTransform Panel(string name, Transform parent, Vector2 size, Vector2 pos, bool small = false, Sprite sprite = null)
            => Panel(name, parent, size, pos, out _, small, sprite);

        public static RectTransform Panel(string name, Transform parent, Vector2 size, Vector2 pos, out Image face, bool small = false, Sprite sprite = null)
        {
            var root = UiKit.Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var sh = UiKit.Image("Shadow", root, Shadow, Color.white, size).rectTransform;
            Fill(sh);
            sh.sizeDelta = new Vector2(60, 60);
            sh.anchoredPosition = new Vector2(0, -12);
            face = UiKit.Image("Face", root, sprite ?? (small ? FrameSmall : Frame), Color.white, size);
            Fill(face.rectTransform);
            return root;
        }

        /// <summary>Stretch a child to fill its parent.</summary>
        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>A brass hairline rule with a diamond in the middle.</summary>
        public static RectTransform Divider(Transform parent, float width, Vector2 pos, float alpha = 1f)
        {
            var root = UiKit.Rect("Divider", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(width, 14));
            var c = new Color(1, 1, 1, alpha);
            float seg = width * 0.5f - 16f;
            var l = UiKit.Image("L", root, null, new Color(0.84f, 0.66f, 0.29f, 0.85f * alpha), new Vector2(seg, 2), new Vector2(-(seg * 0.5f + 14f), 0));
            var r = UiKit.Image("R", root, null, new Color(0.84f, 0.66f, 0.29f, 0.85f * alpha), new Vector2(seg, 2), new Vector2(seg * 0.5f + 14f, 0));
            UiKit.Image("D", root, Diamond, c, new Vector2(14, 14));
            UiKit.Image("DL", root, Diamond, c, new Vector2(6, 6), new Vector2(-width * 0.5f + 3f, 0));
            UiKit.Image("DR", root, Diamond, c, new Vector2(6, 6), new Vector2(width * 0.5f - 3f, 0));
            return root;
        }

        /// <summary>Full-screen dimmer that darkens the edges more than the middle.</summary>
        public static Image Dim(Transform parent, float alpha)
        {
            var rt = UiKit.Stretch("Dim", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Vignette;
            img.color = new Color(0.05f, 0.03f, 0.07f, alpha);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>
        /// Soft drop shadow under the glyphs. The fonts use TMP's mobile SDF shader, whose underlay and outline are
        /// shader_features: a build only keeps keyword sets that some material in it uses, so
        /// Resources/Fonts/SDF/Text Shadow Variant.mat exists to keep UNDERLAY_ON on its own (TMP's Outline and
        /// Drop Shadow materials keep the other two). TextVariantTests checks that every set asked for here is kept.
        /// </summary>
        public static T Shadowed<T>(T t, float alpha = 0.6f, float offset = 0.9f, float softness = 0.35f) where T : TMP_Text
        {
            var m = t.fontMaterial;
            m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.03f, 0.01f, 0.05f, alpha));
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -offset);
            m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, softness);
            m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.1f);
            t.fontMaterial = m;
            return t;
        }

        /// <summary>Gilded display type: a warm gold gradient over a dark outline with a soft shadow.</summary>
        public static T Gilded<T>(T t, float outline = 0.12f) where T : TMP_Text
        {
            t.color = Color.white;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(GoldLight, GoldLight, Palette.Hex(0xE2A646), Palette.Hex(0xE2A646));
            Shadowed(t, 0.7f, 1f, 0.4f);
            if (outline > 0f) Outlined(t, outline, new Color32(70, 38, 12, 255));
            return t;
        }

        /// <summary>A dark edge around the glyphs. Setting outlineWidth alone doesn't draw one: the shader needs OUTLINE_ON.</summary>
        public static T Outlined<T>(T t, float width, Color32 color) where T : TMP_Text
        {
            t.outlineWidth = width;
            t.outlineColor = color;
            var m = t.fontMaterial;
            m.EnableKeyword(ShaderUtilities.Keyword_Outline);
            t.fontMaterial = m;
            return t;
        }

        /// <summary>Small caps label in brass with generous tracking.</summary>
        public static TextMeshProUGUI Label(string name, Transform parent, string text, float size, Vector2 box, Vector2 pos,
            TextAlignmentOptions align = TextAlignmentOptions.Left, Color? color = null)
        {
            var t = UiKit.Text(name, parent, text, size, color ?? Gold, UiKit.Signage, align, box, pos);
            t.characterSpacing = 6f;
            return t;
        }
    }
}
