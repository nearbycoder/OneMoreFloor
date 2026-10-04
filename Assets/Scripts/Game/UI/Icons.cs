using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>Blender-rendered icon sprites from Resources/Icons (null when missing, so callers fall back).</summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Icons/" + name);
            s = tex ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            cache[name] = s;
            return s;
        }

        public static Sprite Floor(FloorId f) => Get("floor_" + f.ToString().ToLowerInvariant());
        public static Sprite Kind(Kind k) => Get("kind_" + k.ToString().ToLowerInvariant());
        public static Sprite Badge(string name) => Get("badge_" + name);
    }
}
