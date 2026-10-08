using UnityEngine;
using UnityEngine.Rendering;

namespace OneMoreFloor
{
    /// <summary>Gradient sky backdrop, distant skyline and per-shift time-of-day lighting presets.</summary>
    public sealed class Sky : MonoBehaviour
    {
        Material skyMat;
        Transform skyline;
        Renderer[] windows;
        System.Collections.Generic.List<Transform> clouds;

        void Update()
        {
            if (clouds == null) return;
            foreach (var c in clouds)
            {
                var p = c.localPosition;
                p.x += Time.deltaTime * 0.8f;
                if (p.x > 150f) p.x -= 300f;
                c.localPosition = p;
            }
        }

        public struct Preset
        {
            public Color Top, Horizon, Bottom, Sun, AmbientSky, AmbientEquator, AmbientGround, Windows;
            public float SunIntensity, SunPitch, SunYaw, Exposure;
        }

        public static Sky Create(Transform parent)
        {
            var go = new GameObject("Sky");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Sky>();
            s.Build();
            return s;
        }

        void Build()
        {
            var template = Resources.Load<Material>("Materials/Sky");
            if (template != null) skyMat = new Material(template);

            // a distant skyline of Deco towers with lit windows
            skyline = new GameObject("Skyline").transform;
            skyline.SetParent(transform, false);
            var rng = new System.Random(7);
            // the low-rise city between the hotel and the skyline (its lit windows follow the time of day too)
            var cityWindows = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-omfNoCity") >= 0
                ? new Renderer[0] : City.Build(transform);   // -omfNoCity: automation's before/after comparisons
            if (ModelLibrary.Prefab("Tower0") != null)
            {
                var wins = new System.Collections.Generic.List<Renderer>(cityWindows);
                for (int i = 0; i < 34; i++)
                {
                    float x = -190f + i * 11.5f + (float)rng.NextDouble() * 5f;
                    var t = ModelLibrary.Instantiate("Tower" + rng.Next(5));
                    t.transform.SetParent(skyline, false);
                    float z = 150f + (float)rng.NextDouble() * 110f;
                    t.transform.localPosition = new Vector3(x, -3f, z);
                    t.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.6f);
                    var w = t.transform.Find("Windows");
                    if (w) wins.Add(w.GetComponent<Renderer>());
                }
                windows = wins.ToArray();
                clouds = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < 9; i++)
                {
                    var c = ModelLibrary.Instantiate("Cloud");
                    if (c == null) break;
                    c.transform.SetParent(transform, false);
                    c.transform.localPosition = new Vector3(-120f + i * 30f + (float)rng.NextDouble() * 12f, 22f + (float)rng.NextDouble() * 30f, 110f + (float)rng.NextDouble() * 40f);
                    c.transform.localScale = Vector3.one * (1.6f + (float)rng.NextDouble() * 1.6f);
                    foreach (var r in c.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    clouds.Add(c.transform);
                }
                return;
            }
            var bodyMat = Mats.Lit(Palette.Hex(0x2B2F4A), 0.1f);
            var winMats = new System.Collections.Generic.List<Renderer>(cityWindows);
            for (int i = 0; i < 46; i++)
            {
                float x = -150f + i * 6.6f + (float)rng.NextDouble() * 3f;
                if (Mathf.Abs(x) < 14f) continue;
                float h = 10f + (float)rng.NextDouble() * 34f;
                float w = 4.5f + (float)rng.NextDouble() * 4f;
                float z = 70f + (float)rng.NextDouble() * 60f;
                Prims.BoxMinMax("Tower" + i, skyline, new Vector3(x - w / 2, -3f, z), new Vector3(x + w / 2, h, z + 6f), bodyMat, false);
                int rows = Mathf.FloorToInt(h / 3.2f);
                for (int r = 1; r < rows; r++)
                {
                    if (rng.NextDouble() < 0.45) continue;
                    var win = Prims.BoxMinMax("Win", skyline, new Vector3(x - w / 2 + 0.8f, r * 3.2f, z - 0.05f), new Vector3(x + w / 2 - 0.8f, r * 3.2f + 0.9f, z), Mats.Glow(Palette.Hex(0xFFD48A), 1.2f), false);
                    winMats.Add(win.GetComponent<Renderer>());
                }
            }
            windows = winMats.ToArray();
        }

        /// <summary>Every lighting preset, in shift order (Monday's morning to the Graveyard's night).</summary>
        public static readonly string[] Ids = { "morning", "noon", "afternoon", "dusk", "evening", "beach", "golden", "overcast", "night" };

        public static Preset Get(string id)
        {
            var p = new Preset
            {
                Top = Palette.Hex(0x4A7BC8), Horizon = Palette.Hex(0xBFE3F2), Bottom = Palette.Hex(0xE8D9B8),
                Sun = Palette.Hex(0xFFF1D6), SunIntensity = 1.35f, SunPitch = 42f, SunYaw = -32f,
                AmbientSky = Palette.Hex(0xB9D4F0), AmbientEquator = Palette.Hex(0xE9DCC6), AmbientGround = Palette.Hex(0x7A6A5E),
                Windows = Palette.Hex(0x3A4060), Exposure = 0.1f,
            };
            switch (id)
            {
                case "morning":
                    p.Top = Palette.Hex(0x6C9BD9); p.Horizon = Palette.Hex(0xFFD9B0); p.Bottom = Palette.Hex(0xF7C9A6);
                    p.Sun = Palette.Hex(0xFFE2BC); p.SunPitch = 26f; p.SunYaw = -48f; p.SunIntensity = 1.3f;
                    p.AmbientSky = Palette.Hex(0xA9C6EA); p.AmbientEquator = Palette.Hex(0xF0D2B6);
                    break;
                case "noon":
                    p.SunPitch = 58f; p.SunIntensity = 1.45f;
                    break;
                case "afternoon":
                    p.Top = Palette.Hex(0x5A86C9); p.Horizon = Palette.Hex(0xF4E2B8); p.Sun = Palette.Hex(0xFFE8C4); p.SunPitch = 36f; p.SunYaw = -20f;
                    break;
                case "dusk":
                    p.Top = Palette.Hex(0x2E2F66); p.Horizon = Palette.Hex(0xF08A6C); p.Bottom = Palette.Hex(0xF4B183);
                    p.Sun = Palette.Hex(0xFFAE7A); p.SunPitch = 14f; p.SunYaw = -60f; p.SunIntensity = 1.1f;
                    p.AmbientSky = Palette.Hex(0x6E6FA8); p.AmbientEquator = Palette.Hex(0xE7A28A); p.AmbientGround = Palette.Hex(0x4A3A44);
                    p.Windows = Palette.Hex(0xFFC98A);
                    break;
                case "evening":
                    p.Top = Palette.Hex(0x1E2450); p.Horizon = Palette.Hex(0xB0607A); p.Bottom = Palette.Hex(0xD98B7A);
                    p.Sun = Palette.Hex(0xFF9E7A); p.SunPitch = 10f; p.SunYaw = -70f; p.SunIntensity = 0.85f;
                    p.AmbientSky = Palette.Hex(0x4C4F8A); p.AmbientEquator = Palette.Hex(0xB9788A); p.AmbientGround = Palette.Hex(0x3A2E3E);
                    p.Windows = Palette.Hex(0xFFC98A);
                    break;
                case "beach":
                    p.Top = Palette.Hex(0x2F8FE0); p.Horizon = Palette.Hex(0xA8EEF7); p.Bottom = Palette.Hex(0xFFF0C2);
                    p.SunPitch = 55f; p.SunIntensity = 1.55f; p.Sun = Palette.Hex(0xFFF8E6);
                    p.AmbientSky = Palette.Hex(0xA6DDF5);
                    break;
                case "golden":
                    p.Top = Palette.Hex(0x5277C2); p.Horizon = Palette.Hex(0xFFC27A); p.Bottom = Palette.Hex(0xFFB27A);
                    p.Sun = Palette.Hex(0xFFC98F); p.SunPitch = 18f; p.SunYaw = -55f; p.SunIntensity = 1.25f;
                    p.AmbientEquator = Palette.Hex(0xF5C79B);
                    break;
                case "overcast":
                    p.Top = Palette.Hex(0x7E8DA6); p.Horizon = Palette.Hex(0xC9CED6); p.Bottom = Palette.Hex(0xBDB8B0);
                    p.Sun = Palette.Hex(0xE6EAF0); p.SunIntensity = 0.95f; p.SunPitch = 50f;
                    p.AmbientSky = Palette.Hex(0xAEB8C8); p.AmbientEquator = Palette.Hex(0xC8C6C2);
                    break;
                case "night":
                    p.Top = Palette.Hex(0x0B1030); p.Horizon = Palette.Hex(0x2B2F63); p.Bottom = Palette.Hex(0x3D2E52);
                    p.Sun = Palette.Hex(0x9DB4FF); p.SunPitch = 40f; p.SunYaw = 30f; p.SunIntensity = 0.45f;
                    p.AmbientSky = Palette.Hex(0x2C3566); p.AmbientEquator = Palette.Hex(0x3E3A66); p.AmbientGround = Palette.Hex(0x1C1826);
                    p.Windows = Palette.Hex(0xFFD48A); p.Exposure = 0.25f;
                    break;
            }
            return p;
        }

        public void Apply(string id, Light sun, Camera cam)
        {
            var p = Get(id);
            if (skyMat != null)
            {
                skyMat.SetColor("_Top", p.Top);
                skyMat.SetColor("_Horizon", p.Horizon);
                skyMat.SetColor("_Bottom", p.Bottom);
                var sunDir = Quaternion.Euler(p.SunPitch, p.SunYaw, 0f) * Vector3.back;
                skyMat.SetVector("_SunDir", new Vector4(-sunDir.x, Mathf.Max(0.05f, -sunDir.y) * 0.5f + 0.08f, -sunDir.z, 0));
                skyMat.SetColor("_SunColor", p.Sun * (id == "night" ? 0.35f : 0.8f));
                RenderSettings.skybox = skyMat;
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            sun.color = p.Sun;
            sun.intensity = p.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(p.SunPitch, p.SunYaw, 0f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.AmbientSky;
            RenderSettings.ambientEquatorColor = p.AmbientEquator;
            RenderSettings.ambientGroundColor = p.AmbientGround;
            cam.backgroundColor = p.Top;
            // atmospheric perspective: the skyline fades into the horizon colour
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Color.Lerp(p.Horizon, p.Top, 0.25f);
            RenderSettings.fogStartDistance = 130f;
            RenderSettings.fogEndDistance = 520f;
            var winMat = Mats.Glow(p.Windows, id == "night" || id == "dusk" || id == "evening" ? 1.6f : 0.4f);
            foreach (var w in windows) if (w) w.sharedMaterial = winMat;
            // after dark the room lamps become the key light
            FloorView.LampScale = id == "night" ? 2.1f : id == "evening" ? 1.6f : id == "dusk" ? 1.35f : 1f;
        }
    }
}
