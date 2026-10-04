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
            return go;
        }

        public static GameObject Floor(FloorId id) => Instantiate("Floor_" + id);
        public static GameObject Character(Kind k) => Instantiate("Char_" + k);
    }
}
