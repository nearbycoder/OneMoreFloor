using System.Collections.Generic;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>Particle presets built in code: dust, sparks, bats, sparkles, splashes, confetti, coins.</summary>
    public sealed class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }
        /// <summary>GRAPHICS FIDELITY: bursts emit this many times the particles they ask for (LOW 0.5, HIGH 1, ULTRA 1.6).</summary>
        public static float Density = 1f;
        readonly Dictionary<string, ParticleSystem> systems = new Dictionary<string, ParticleSystem>();
        Material alphaMat, addMat, starMat, squareMat;

        public static Fx Create(Transform parent)
        {
            var go = new GameObject("Fx");
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<Fx>();
            Instance = fx;
            fx.Build();
            return fx;
        }

        Material ParticleMat(Texture tex, bool additive)
        {
            var template = Resources.Load<Material>("Materials/Particles");
            var m = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.renderQueue = 3100;
            return m;
        }

        static Texture2D SquareTex()
        {
            var t = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var px = new Color32[64];
            for (int i = 0; i < 64; i++) px[i] = new Color32(255, 255, 255, 255);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        void Build()
        {
            alphaMat = ParticleMat(UiKit.SoftCircle.texture, false);
            addMat = ParticleMat(UiKit.SoftCircle.texture, true);
            starMat = ParticleMat(Stars.Sprite.texture, true);
            squareMat = ParticleMat(SquareTex(), false);

            Make("dust", alphaMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                m.startColor = new Color(0.93f, 0.86f, 0.74f, 0.55f);
                m.gravityModifier = -0.05f;
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(14f, 0.1f, 0.6f);
                var vel = s.limitVelocityOverLifetime;
                vel.enabled = true;
                vel.dampen = 0.2f;
                vel.limit = 1f;
                Fade(s);
                Grow(s, 1f, 1.8f);
            });
            Make("sparks", addMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.5f, 0.15f));
                m.gravityModifier = 1.5f;
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Hemisphere;
                sh.radius = 0.2f;
                sh.rotation = new Vector3(-90, 0, 0);
                var r = s.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.5f;
                Fade(s);
            });
            Make("smoke", alphaMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
                m.startColor = new Color(0.35f, 0.3f, 0.4f, 0.7f);
                m.gravityModifier = -0.15f;
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.4f;
                Fade(s);
                Grow(s, 0.6f, 2f);
            });
            var batMesh = MeshFrom("FxMeshes", "Bat");
            Make("bats", batMesh != null ? Mats.Lit(Palette.Hex(0x1C1820), 0.3f) : alphaMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
                m.startColor = Color.white;
                m.gravityModifier = -0.4f;
                m.startRotation3D = false;
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 50f;
                sh.radius = 0.3f;
                sh.rotation = new Vector3(-90, 0, 0);
                var noise = s.noise;
                noise.enabled = true;
                noise.strength = 2.5f;
                noise.frequency = 1.5f;
                var sz = s.sizeOverLifetime;
                sz.enabled = true;
                var curve = new AnimationCurve();
                for (int i = 0; i <= 12; i++) curve.AddKey(i / 12f, i % 2 == 0 ? 1f : 0.55f);
                curve.AddKey(1f, 0f);
                sz.size = new ParticleSystem.MinMaxCurve(1f, curve);
                var r = s.GetComponent<ParticleSystemRenderer>();
                if (batMesh != null)
                {
                    r.renderMode = ParticleSystemRenderMode.Mesh;
                    r.mesh = batMesh;
                    r.alignment = ParticleSystemRenderSpace.View;
                }
            });
            Make("sparkle", starMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.5f), new Color(1f, 0.75f, 0.25f));
                m.gravityModifier = -0.1f;
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Sphere;
                sh.radius = 0.6f;
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                Fade(s);
            });
            Make("splash", alphaMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
                m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.92f, 1f, 0.9f), new Color(0.4f, 0.75f, 1f, 0.9f));
                m.gravityModifier = 2f;
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 35f;
                sh.radius = 0.5f;
                sh.rotation = new Vector3(-90, 0, 0);
                Fade(s);
            });
            Make("confetti", squareMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
                m.startSize3D = true;
                m.startSizeX = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                m.startSizeY = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
                m.startSizeZ = 1f;
                var g = new Gradient();
                m.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex(0xFF5DA2), Palette.Hex(0xFFC857)) { mode = ParticleSystemGradientMode.TwoColors };
                m.gravityModifier = 0.6f;
                m.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 30f;
                sh.radius = 0.3f;
                sh.rotation = new Vector3(-90, 0, 0);
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-8f, 8f);
                var lim = s.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit = 3f;
                lim.dampen = 0.15f;
                var col = s.colorOverLifetime;
                col.enabled = true;
                var cg = new Gradient();
                cg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                           new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
                col.color = cg;
            });
            var coinMesh = MeshFrom("FxMeshes", "Coin");
            Make("coins", coinMesh != null ? Mats.Lit(Palette.Hex(0xFFC83D), 0.85f, 0.9f) : starMat, s =>
            {
                var m = s.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
                m.startColor = Color.white;
                m.gravityModifier = 2.4f;
                m.startRotation3D = true;
                m.startRotationX = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                m.startRotationY = new ParticleSystem.MinMaxCurve(0f, 6.28f);
                var sh = s.shape;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 28f;
                sh.radius = 0.15f;
                sh.rotation = new Vector3(-90, 0, 0);
                var rot = s.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(6f, 12f);
                rot.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                var sz = s.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0.7f, 1f, 1f, 0f));
                var r = s.GetComponent<ParticleSystemRenderer>();
                if (coinMesh != null)
                {
                    r.renderMode = ParticleSystemRenderMode.Mesh;
                    r.mesh = coinMesh;
                }
            });
        }

        static Mesh MeshFrom(string model, string child)
        {
            var prefab = ModelLibrary.Prefab(model);
            if (prefab == null) return null;
            var t = prefab.transform.Find(child);
            var mf = t ? t.GetComponent<MeshFilter>() : null;
            return mf ? mf.sharedMesh : null;
        }

        void Make(string name, Material mat, System.Action<ParticleSystem> configure)
        {
            var go = new GameObject("FX_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10f;
            configure(ps);
            systems[name] = ps;
        }

        static void Fade(ParticleSystem s)
        {
            var col = s.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
        }

        static void Grow(ParticleSystem s, float from, float to)
        {
            var sz = s.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from / to, 1f, 1f));
        }

        public void Burst(string name, Vector3 at, int count, Color? tint = null)
        {
            if (!systems.TryGetValue(name, out var ps)) return;
            ps.transform.position = at;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            if (tint.HasValue) ep.startColor = tint.Value;
            ps.Emit(ep, Scaled(count));
        }

        /// <summary>A burst's particle count at the current <see cref="Density"/> (never fewer than one).</summary>
        public static int Scaled(int count) => count <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(count * Density));

        public static void Play(string name, Vector3 at, int count, Color? tint = null) => Instance?.Burst(name, at, count, tint);
    }
}
