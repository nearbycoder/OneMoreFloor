using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreFloor
{
    /// <summary>
    /// The city between the hotel and the skyline: a back street, rows of low-rise blocks with lit and dark windows,
    /// and ground that runs on into the haze, so the skyline stands on something. Built in code from boxes merged into
    /// a handful of meshes (one per material), so it adds a few draw calls rather than hundreds of objects.
    /// </summary>
    public static class City
    {
        sealed class Batch
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            /// <summary>A box without its bottom (nothing sees it) and, unless asked, without its back.</summary>
            public void Box(Vector3 min, Vector3 max, bool back = false)
            {
                Quad(new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z), Vector3.back);
                Quad(new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), Vector3.up);
                Quad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), Vector3.left);
                Quad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.right);
                if (back) Quad(new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), Vector3.forward);
            }

            /// <summary>A flat rectangle facing the camera (-z) at depth z.</summary>
            public void Front(float x0, float y0, float x1, float y1, float z) =>
                Quad(new Vector3(x0, y0, z), new Vector3(x1, y0, z), new Vector3(x1, y1, z), new Vector3(x0, y1, z), Vector3.back);

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                N.Add(n); N.Add(n); N.Add(n); N.Add(n);
                T.Add(i); T.Add(i + 2); T.Add(i + 1);
                T.Add(i); T.Add(i + 3); T.Add(i + 2);
            }

            public Renderer Build(string name, Transform parent, Material mat, bool castShadows)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var mesh = new Mesh { name = name, indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(V);
                mesh.SetNormals(N);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = true;
                return r;
            }
        }

        /// <summary>Facade colours: brick, sandstone, slate, tan, mauve, cream. Muted so the hotel stays the star.</summary>
        static readonly uint[] Facades = { 0x9A6656, 0xC4AE93, 0x7A8697, 0xA6856B, 0x8C5F6B, 0xD8CDB6 };

        /// <summary>Builds the city under <paramref name="parent"/>. Returns the lit-window renderers (the sky swaps their
        /// material for the time of day, like the skyline's).</summary>
        public static Renderer[] Build(Transform parent)
        {
            var root = new GameObject("City").transform;
            root.SetParent(parent, false);
            var rng = new System.Random(1929);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            const float Street = -2.05f;   // the pavement's top (ArtSource/build_props.py base())
            var ground = new Batch();
            var asphalt = new Batch();
            var paint = new Batch();
            // the back street behind the hotel, and ground from there to the haze
            asphalt.Box(new Vector3(-700f, Street - 0.4f, 10f), new Vector3(700f, Street - 0.02f, 22f));
            for (int k = -70; k < 70; k++) paint.Box(new Vector3(k * 5f - 1.2f, Street - 0.02f, 15.85f), new Vector3(k * 5f + 1.2f, Street - 0.01f, 16.15f));
            ground.Box(new Vector3(-700f, Street - 0.4f, 22f), new Vector3(700f, Street, 1000f));

            var walls = new Batch[Facades.Length];
            for (int i = 0; i < walls.Length; i++) walls[i] = new Batch();
            var roofs = new Batch();
            var lit = new Batch();
            var dark = new Batch();
            // rows of blocks, lower near the hotel and taller towards the skyline, with alleys between them; cross
            // streets every so often so the rows don't read as one wall
            float[] rowZ = { 30f, 52f, 76f, 102f, 130f };
            for (int row = 0; row < rowZ.Length; row++)
            {
                float z0 = rowZ[row], depth = R(11f, 15f);
                float x = -260f + R(0f, 6f);
                while (x < 260f)
                {
                    if (rng.NextDouble() < 0.12) { x += R(5f, 8f); continue; }   // a cross street
                    float w = R(6.5f, 15f), h = R(4.5f + row * 2.5f, 9f + row * 4.5f);
                    float zf = z0 + R(-1.2f, 1.2f);
                    var wall = walls[rng.Next(walls.Length)];
                    wall.Box(new Vector3(x, Street, zf), new Vector3(x + w, Street + h, zf + depth));
                    // a parapet and, now and then, a stair house or a water tank on the roof
                    roofs.Box(new Vector3(x - 0.15f, Street + h, zf - 0.15f), new Vector3(x + w + 0.15f, Street + h + 0.45f, zf + 0.35f));
                    if (rng.NextDouble() < 0.35)
                    {
                        float rx = x + R(1f, Mathf.Max(1.2f, w - 3f));
                        roofs.Box(new Vector3(rx, Street + h, zf + 2f), new Vector3(rx + R(1.8f, 2.8f), Street + h + R(1.6f, 2.6f), zf + 4.5f));
                    }
                    // windows: a grid on the front face, about a third lit after dark
                    float fz = zf - 0.03f;
                    int floors = Mathf.FloorToInt((h - 1.2f) / 2.4f);
                    int cols = Mathf.FloorToInt((w - 1.0f) / 1.7f);
                    float x0 = x + (w - cols * 1.7f) * 0.5f + 0.4f;
                    for (int f = 0; f < floors; f++)
                        for (int c = 0; c < cols; c++)
                        {
                            float wx = x0 + c * 1.7f, wy = Street + 1.3f + f * 2.4f;
                            (rng.NextDouble() < 0.34 ? lit : dark).Front(wx, wy, wx + 0.9f, wy + 1.25f, fz);
                        }
                    x += w + R(1.4f, 3.2f);
                }
            }

            ground.Build("Ground", root, Mats.Lit(Palette.Hex(0x8B8379), 0.08f), false);
            asphalt.Build("BackStreet", root, Mats.Lit(Palette.Hex(0x4A4550), 0.15f), false);
            paint.Build("Lines", root, Mats.Lit(Palette.Hex(0xE8DCC0), 0.2f), false);
            for (int i = 0; i < walls.Length; i++) walls[i].Build("Blocks" + i, root, Mats.Lit(Palette.Hex(Facades[i]), 0.12f), true);
            roofs.Build("Roofs", root, Mats.Lit(Palette.Hex(0x4E4652), 0.1f), true);
            dark.Build("DarkWindows", root, Mats.Lit(Palette.Hex(0x2E3346), 0.6f), false);
            return new[] { lit.Build("LitWindows", root, Mats.Glow(Palette.Hex(0xFFD48A), 1.2f), false) };
        }
    }
}
