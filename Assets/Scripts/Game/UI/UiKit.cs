using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>Procedural sprites, fonts and small factories for runtime-built uGUI.</summary>
    public static class UiKit
    {
        static TMP_FontAsset display, signage, body, hand;
        public static TMP_FontAsset Display => display ? display : (display = Resources.Load<TMP_FontAsset>("Fonts/SDF/Limelight-Regular SDF"));
        public static TMP_FontAsset Signage => signage ? signage : (signage = Resources.Load<TMP_FontAsset>("Fonts/SDF/Bungee-Regular SDF"));
        public static TMP_FontAsset Body => body ? body : (body = Resources.Load<TMP_FontAsset>("Fonts/SDF/VarelaRound-Regular SDF"));
        public static TMP_FontAsset Hand => hand ? hand : (hand = Resources.Load<TMP_FontAsset>("Fonts/SDF/PatrickHand-Regular SDF"));

        static Sprite circle, ring, rounded, softCircle, pill, triangle;
        /// <summary>An anti-aliased triangle pointing up (rotate it for other directions).</summary>
        public static Sprite Triangle => triangle ? triangle : (triangle = MakeTriangle(96));
        public static Sprite Circle => circle ? circle : (circle = MakeCircle(128, 0f, false));
        public static Sprite Ring => ring ? ring : (ring = MakeCircle(128, 0.2f, false));
        public static Sprite SoftCircle => softCircle ? softCircle : (softCircle = MakeCircle(128, 0f, true));
        public static Sprite Rounded => rounded ? rounded : (rounded = MakeRounded(96, 30));
        public static Sprite Pill => pill ? pill : (pill = MakeRounded(128, 62));

        static Sprite MakeCircle(int size, float ringWidth, bool soft)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r)) / r;
                    float a;
                    if (soft) a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    else
                    {
                        a = Mathf.Clamp01((1f - d) * r);
                        if (ringWidth > 0f) a *= Mathf.Clamp01((d - (1f - ringWidth)) * r);
                    }
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeTriangle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            // apex at the top centre, base along the bottom, 2px inset; coverage from the distance to each edge
            Vector2 a = new Vector2(size * 0.5f, size - 3f), b = new Vector2(3f, 6f), c = new Vector2(size - 3f, 6f);
            float Edge(Vector2 p, Vector2 p0, Vector2 p1)
            {
                var e = p1 - p0;
                var n = new Vector2(-e.y, e.x).normalized;
                return Vector2.Dot(p - p0, n);
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float d = Mathf.Min(Edge(p, a, b), Mathf.Min(Edge(p, b, c), Edge(p, c, a)));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rt = Rect(name, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos = default, bool raycast = false)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TMP_FontAsset font = null,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Vector2 boxSize = default, Vector2 pos = default)
        {
            var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos,
                boxSize == default ? new Vector2(400, size * 1.4f) : boxSize);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ? font : Body;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>A 3D TextMeshPro label in the world (signs on floors, the neon).</summary>
        public static TextMeshPro WorldText(Transform parent, string text, Vector3 localPos, float size, Color color, TMP_FontAsset font)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var t = go.AddComponent<TextMeshPro>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(12f, 2f);
            return t;
        }

        public static void SetAnchorsPoint(RectTransform rt, Vector2 anchor)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
        }
    }
}
