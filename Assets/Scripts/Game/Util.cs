using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>World layout shared by every view (metres). Slot 0's floor top is at y = 0.</summary>
    public static class Layout
    {
        // Must match ArtSource/build_floors.py
        public const float SlotHeight = 2.8f;
        public const float SlabThickness = 0.3f;
        public const float HalfWidth = 7.6f;          // floor modules span -HalfWidth..HalfWidth
        public const float ShaftHalf = 1.9f;          // shaft opening
        public const float FrontZ = -2.1f;            // front edge of the floor slab
        public const float BackZ = 3.0f;              // back wall
        public const float CarZ = 0.15f;
        public const float CarWidth = 3.5f, CarHeight = 2.4f, CarDepth = 2.5f;
        public const float QueueZ = -1.05f;
        public const float QueueX0 = 2.75f, QueueStep = 0.82f;
        public const float PopOutZ = -4.2f;           // how far floors pull toward the camera when they shuffle

        public static float SlotY(float slot) => slot * SlotHeight;
        public static Vector3 QueueLocal(int index) => new Vector3(QueueX0 + QueueStep * index, 0f, QueueZ - 0.08f * (index % 2));
        public static readonly float[] RiderX = { -1.08f, -0.36f, 0.36f, 1.08f };
        public static Vector3 RiderLocal(int spot, int size)
        {
            float x = size == 2 && spot < RiderX.Length - 1 ? (RiderX[spot] + RiderX[spot + 1]) * 0.5f : RiderX[Mathf.Clamp(spot, 0, 3)];
            return new Vector3(x, 0.02f, -0.15f + 0.12f * (spot % 2));
        }
    }

    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t) { t = Mathf.Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f; }
        public static float OutBack(float t, float s = 1.70158f) { t = Mathf.Clamp01(t) - 1f; return 1f + t * t * ((s + 1f) * t + s); }
        public static float InBack(float t, float s = 1.70158f) { t = Mathf.Clamp01(t); return t * t * ((s + 1f) * t - s); }
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t == 0f || t == 1f) return t;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        /// <summary>Exponential approach factor for frame-rate independent smoothing.</summary>
        public static float Damp(float sharpness, float dt) => 1f - Mathf.Exp(-sharpness * dt);
    }

    /// <summary>A critically-damped-ish spring for juicy secondary motion.</summary>
    public struct Spring
    {
        public float Value, Velocity;
        public void Step(float target, float stiffness, float damping, float dt)
        {
            float a = (target - Value) * stiffness - Velocity * damping;
            Velocity += a * dt;
            Value += Velocity * dt;
        }
    }

    public static class Palette
    {
        public static Color Hex(uint rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);

        public static Color Floor(FloorId f) => Hex(Defs.Floor(f).Color);

        public static readonly Color Cream = Hex(0xF6EBD3);
        public static readonly Color Ink = Hex(0x2A1E2E);
        public static readonly Color Brass = Hex(0xD7A84A);
        public static readonly Color BrassDark = Hex(0x8C6424);
        public static readonly Color Oxblood = Hex(0x6E2A33);
        public static readonly Color Teal = Hex(0x2D6E73);
        public static readonly Color Good = Hex(0x6CCB5F);
        public static readonly Color Warn = Hex(0xF2B33D);
        public static readonly Color Bad = Hex(0xE5484D);

        public static Color KindColor(Kind k)
        {
            switch (k)
            {
                case Kind.Commuter: return Hex(0x4C6A92);
                case Kind.Houseplant: return Hex(0x3E9B4F);
                case Kind.Mirror: return Hex(0x3C7DD9);
                case Kind.Vampire: return Hex(0x2B2233);
                case Kind.Courier: return Hex(0xA4662C);
                case Kind.Swimmer: return Hex(0xF2C14E);
                case Kind.Kid: return Hex(0xF07F3C);
                case Kind.Tycoon: return Hex(0x6B4E9B);
            }
            return Color.gray;
        }
    }

    /// <summary>Runtime materials cloned from template assets in Resources/Materials (so shaders ship).</summary>
    public static class Mats
    {
        static readonly Dictionary<long, Material> lit = new Dictionary<long, Material>();
        static readonly Dictionary<long, Material> unlit = new Dictionary<long, Material>();
        static Material litTemplate, unlitTemplate, transparentTemplate;

        static Material LitTemplate => litTemplate ? litTemplate : (litTemplate = Resources.Load<Material>("Materials/Lit"));
        static Material UnlitTemplate => unlitTemplate ? unlitTemplate : (unlitTemplate = Resources.Load<Material>("Materials/Unlit"));
        static Material TransparentTemplate => transparentTemplate ? transparentTemplate : (transparentTemplate = Resources.Load<Material>("Materials/LitTransparent"));

        static long Key(Color c, float a, float b)
        {
            Color32 k = c;
            return ((long)k.r << 40) | ((long)k.g << 32) | ((long)k.b << 24) | ((long)k.a << 16) | ((long)(a * 100) << 8) | (long)(b * 100);
        }

        public static Material Lit(Color c, float smoothness = 0.35f, float metallic = 0f)
        {
            long key = Key(c, smoothness, metallic);
            if (lit.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitTemplate);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.enableInstancing = true;
            lit[key] = m;
            return m;
        }

        public static Material Glow(Color c, float intensity = 2f)
        {
            long key = Key(c, intensity / 10f, 0.99f);
            if (lit.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitTemplate);
            m.SetColor("_BaseColor", c);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", c * intensity);
            lit[key] = m;
            return m;
        }

        public static Material Unlit(Color c)
        {
            long key = Key(c, 0, 0);
            if (unlit.TryGetValue(key, out var m) && m) return m;
            m = new Material(UnlitTemplate);
            m.SetColor("_BaseColor", c);
            unlit[key] = m;
            return m;
        }

        /// <summary>A fresh (uncached) transparent lit material, for things that fade.</summary>
        public static Material Glass(Color c, float smoothness = 0.9f)
        {
            var m = new Material(TransparentTemplate);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smoothness);
            return m;
        }
    }

    public static class Prims
    {
        static Mesh cube, sphere, capsule, cylinder, quad;

        static Mesh Get(PrimitiveType t, ref Mesh cache)
        {
            if (cache) return cache;
            var go = GameObject.CreatePrimitive(t);
            cache = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return cache;
        }

        public static Mesh Cube => Get(PrimitiveType.Cube, ref cube);
        public static Mesh Sphere => Get(PrimitiveType.Sphere, ref sphere);
        public static Mesh Capsule => Get(PrimitiveType.Capsule, ref capsule);
        public static Mesh Cylinder => Get(PrimitiveType.Cylinder, ref cylinder);
        public static Mesh Quad => Get(PrimitiveType.Quad, ref quad);

        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion? rot = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool shadows = true)
            => Make(name, Cube, mat, parent, center, size, null, shadows);

        /// <summary>Box from min/max corners.</summary>
        public static GameObject BoxMinMax(string name, Transform parent, Vector3 min, Vector3 max, Material mat, bool shadows = true)
            => Make(name, Cube, mat, parent, (min + max) * 0.5f, max - min, null, shadows);
    }
}
