using System.Collections.Generic;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>The tower: floor modules, the brass shaft, the roof with its neon sign, and the street.</summary>
    public sealed class BuildingView : MonoBehaviour
    {
        public readonly FloorView[] Floors = new FloorView[Defs.FloorCount];
        public int SlotCount { get; private set; }
        public Transform Roof { get; private set; }
        Transform shaft, ground;
        readonly List<Renderer> neon = new List<Renderer>();
        TMP_Text neonText;
        float neonFlicker = 1f;

        public static BuildingView Create(Transform parent)
        {
            var go = new GameObject("Building");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BuildingView>();
            for (int i = 0; i < Defs.FloorCount; i++)
            {
                b.Floors[i] = FloorView.Create((FloorId)i, go.transform);
                b.Floors[i].gameObject.SetActive(false);
            }
            return b;
        }

        public FloorView this[FloorId id] => Floors[(int)id];

        public float TopY => Layout.SlotY(SlotCount) - Layout.SlabThickness;

        public void Setup(Building model)
        {
            SlotCount = model.Count;
            foreach (var f in Floors) { f.gameObject.SetActive(false); f.InBuilding = false; }
            for (int s = 0; s < model.Count; s++)
            {
                var fv = this[model.At(s)];
                fv.gameObject.SetActive(true);
                fv.InBuilding = true;
                fv.SetSlot(s, true);
                fv.SetLeaving(model.Leaving[(int)model.At(s)]);
            }
            BuildShaft();
            BuildRoof();
            BuildGround();
        }

        void BuildShaft()
        {
            if (shaft) Destroy(shaft.gameObject);
            shaft = new GameObject("Shaft").transform;
            shaft.SetParent(transform, false);
            var model = ModelLibrary.Instantiate("Shaft");
            float top = TopY + 3.2f;
            var brass = Mats.Lit(Palette.Brass, 0.7f, 0.85f);
            var dark = Mats.Lit(Palette.Hex(0x221B26), 0.2f);
            float S = Layout.ShaftHalf - 0.08f;
            // back of the shaft
            Prims.BoxMinMax("ShaftBack", shaft, new Vector3(-S, -0.4f, 1.55f), new Vector3(S, top, 1.7f), dark);
            // four rails
            foreach (float x in new[] { -S, S })
            {
                Prims.BoxMinMax("RailF", shaft, new Vector3(x - 0.06f, -0.4f, -1.42f), new Vector3(x + 0.06f, top, -1.3f), brass);
                Prims.BoxMinMax("RailB", shaft, new Vector3(x - 0.06f, -0.4f, 1.4f), new Vector3(x + 0.06f, top, 1.52f), brass);
            }
            // pulley housing at the top
            var wheel = Prims.Make("Pulley", Prims.Cylinder, brass, shaft, new Vector3(0, top - 0.6f, 0.4f), new Vector3(1.2f, 0.12f, 1.2f), Quaternion.Euler(90, 0, 0));
            wheel.AddComponent<Spinner>().Axis = Vector3.up;
            Prims.BoxMinMax("Header", shaft, new Vector3(-S - 0.2f, top - 0.2f, -1.5f), new Vector3(S + 0.2f, top + 0.15f, 1.7f), brass);
            if (model) Destroy(model);
        }

        void BuildRoof()
        {
            if (Roof) Destroy(Roof.gameObject);
            Roof = new GameObject("Roof").transform;
            Roof.SetParent(transform, false);
            Roof.localPosition = new Vector3(0, TopY, 0);
            var brick = Mats.Lit(Palette.Oxblood, 0.1f);
            var cream = Mats.Lit(Palette.Cream, 0.25f);
            var brass = Mats.Lit(Palette.Brass, 0.7f, 0.85f);
            float L = Layout.HalfWidth + 0.3f;
            Prims.BoxMinMax("RoofSlab", Roof, new Vector3(-L, 0, Layout.FrontZ - 0.1f), new Vector3(L, 0.45f, Layout.BackZ + 0.1f), brick);
            Prims.BoxMinMax("Cornice", Roof, new Vector3(-L - 0.2f, 0.45f, Layout.FrontZ - 0.25f), new Vector3(L + 0.2f, 0.7f, Layout.BackZ + 0.2f), cream);
            Prims.BoxMinMax("Parapet", Roof, new Vector3(-L, 0.7f, Layout.FrontZ - 0.1f), new Vector3(L, 1.2f, Layout.FrontZ + 0.15f), brick);
            // water tower
            var tank = Prims.Make("Tank", Prims.Cylinder, Mats.Lit(Palette.Hex(0x8A5A3B), 0.2f), Roof, new Vector3(5.2f, 3.6f, 1.6f), new Vector3(2.2f, 1.2f, 2.2f));
            Prims.Make("TankCap", Prims.Sphere, Mats.Lit(Palette.Hex(0x5E3B28), 0.2f), Roof, new Vector3(5.2f, 4.8f, 1.6f), new Vector3(2.3f, 0.9f, 2.3f));
            foreach (var o in new[] { new Vector2(-0.8f, -0.8f), new Vector2(0.8f, -0.8f), new Vector2(-0.8f, 0.8f), new Vector2(0.8f, 0.8f) })
                Prims.BoxMinMax("Leg", Roof, new Vector3(5.2f + o.x - 0.07f, 0.7f, 1.6f + o.y - 0.07f), new Vector3(5.2f + o.x + 0.07f, 2.5f, 1.6f + o.y + 0.07f), Mats.Lit(Palette.Ink));
            // neon sign on a brass frame
            var frame = Prims.BoxMinMax("SignFrame", Roof, new Vector3(-4.6f, 1.2f, 0.3f), new Vector3(3.2f, 3.0f, 0.42f), Mats.Lit(Palette.Hex(0x2A1E2E), 0.3f));
            Prims.BoxMinMax("SignPostL", Roof, new Vector3(-4.2f, 0.7f, 0.32f), new Vector3(-4.0f, 1.2f, 0.4f), brass);
            Prims.BoxMinMax("SignPostR", Roof, new Vector3(2.8f, 0.7f, 0.32f), new Vector3(3.0f, 1.2f, 0.4f), brass);
            neonText = UiKit.WorldText(Roof, "THE SHUFFLETON", new Vector3(-0.7f, 2.12f, 0.22f), 5.6f, Palette.Hex(0xFF7AA8), UiKit.Display);
            neonText.fontMaterial.EnableKeyword("GLOW_ON");
            neonText.fontSharedMaterial.SetColor("_FaceColor", Palette.Hex(0xFF7AA8) * 2.2f);
            var line = Prims.BoxMinMax("NeonLine", Roof, new Vector3(-4.3f, 1.42f, 0.24f), new Vector3(2.9f, 1.5f, 0.28f), Mats.Glow(Palette.Hex(0xFFC857), 3f), false);
            neon.Add(line.GetComponent<Renderer>());
        }

        void BuildGround()
        {
            if (ground) Destroy(ground.gameObject);
            ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            var stone = Mats.Lit(Palette.Hex(0xB9A88E), 0.15f);
            var street = Mats.Lit(Palette.Hex(0x3B3540), 0.25f);
            var brick = Mats.Lit(Palette.Hex(0x5A222B), 0.1f);
            Prims.BoxMinMax("Foundation", ground, new Vector3(-Layout.HalfWidth - 0.6f, -2.2f, Layout.FrontZ - 0.2f), new Vector3(Layout.HalfWidth + 0.6f, -Layout.SlabThickness, Layout.BackZ + 0.3f), brick);
            Prims.BoxMinMax("Sidewalk", ground, new Vector3(-60f, -2.6f, -9f), new Vector3(60f, -2.2f, 8f), stone);
            Prims.BoxMinMax("Street", ground, new Vector3(-60f, -2.75f, -30f), new Vector3(60f, -2.6f, -9f), street);
            Prims.BoxMinMax("Curb", ground, new Vector3(-60f, -2.75f, -9.25f), new Vector3(60f, -2.45f, -8.9f), Mats.Lit(Palette.Hex(0xD9CBB0), 0.2f));
        }

        public void SetNeon(float on) => neonFlicker = on;

        void Update()
        {
            float t = Time.time;
            float flick = Mathf.PerlinNoise(t * 7f, 0.3f) > 0.08f ? 1f : 0.4f;
            if (neonText) neonText.alpha = Mathf.Lerp(0.25f, 1f, neonFlicker * flick);
        }
    }

    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float Speed;
        public void Update() { if (Speed != 0f) transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self); }
    }
}
