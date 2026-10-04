using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Loads the Blender-exported models from Resources/Models. Returns null when a model is missing so
    /// views can fall back to greybox geometry.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Prefab(string name)
        {
            if (cache.TryGetValue(name, out var go)) return go;
            go = Resources.Load<GameObject>("Models/" + name);
            cache[name] = go;
            return go;
        }

        public static GameObject Instantiate(string name)
        {
            var p = Prefab(name);
            if (p == null) return null;
            var go = Object.Instantiate(p);
            go.name = name;
            Remap(go);
            return go;
        }

        static readonly Dictionary<string, Material> resolved = new Dictionary<string, Material>();

        /// <summary>
        /// Swap the FBX's imported materials for shared URP ones built from their names:
        /// col_/met_/gls_/glo_ + RRGGBB (+ _sNN smoothness, _aNN alpha, _iNN emission x10).
        /// </summary>
        public static void Remap(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool castShadows = true;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i] = Resolve(mats[i].name);
                    if (mats[i].name.StartsWith("gls_") || mats[i].name.StartsWith("glo_")) castShadows &= mats.Length > 1;
                }
                r.sharedMaterials = mats;
                if (!castShadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static Material Resolve(string name)
        {
            name = name.Replace(" (Instance)", "");
            if (resolved.TryGetValue(name, out var m) && m) return m;
            var parts = name.Split('_');
            if (parts.Length < 2 || parts[1].Length != 6) return resolved[name] = Mats.Lit(Color.magenta);
            var c = Palette.Hex(System.Convert.ToUInt32(parts[1], 16));
            float s = -1f, a = -1f, e = -1f;
            for (int i = 2; i < parts.Length; i++)
            {
                if (parts[i].Length < 2 || !int.TryParse(parts[i].Substring(1), out int v)) continue;
                switch (parts[i][0]) { case 's': s = v / 100f; break; case 'a': a = v / 100f; break; case 'i': e = v / 10f; break; }
            }
            switch (parts[0])
            {
                case "met": m = Mats.Lit(c, s < 0 ? 0.72f : s, 0.9f); break;
                case "gls": m = Mats.Glass(new Color(c.r, c.g, c.b, a < 0 ? 0.3f : a), s < 0 ? 0.92f : s); break;
                case "glo": m = Mats.Glow(c, e < 0 ? 2f : e); break;
                default: m = Mats.Lit(c, s < 0 ? 0.35f : s, 0f); break;
            }
            m.name = name;
            resolved[name] = m;
            return m;
        }

        public static GameObject Floor(FloorId id) => Instantiate("Floor_" + id);
        public static GameObject Character(Kind k) => Instantiate("Char_" + k);
    }
}
