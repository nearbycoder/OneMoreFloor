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

        public Transform Pulley { get; private set; }
        public float PulleyY => TopY + 0.4f + 3.2f;

        void BuildShaft()
        {
            if (shaft) Destroy(shaft.gameObject);
            shaft = new GameObject("Shaft").transform;
            shaft.SetParent(transform, false);
            for (int s = 0; s < SlotCount; s++)
            {
                var seg = ModelLibrary.Instantiate("ShaftSegment");
                if (seg == null) break;
                seg.transform.SetParent(shaft, false);
                seg.transform.localPosition = new Vector3(0, Layout.SlotY(s), 0);
            }
            var shaftLight = new GameObject("ShaftLight").AddComponent<Light>();
            shaftLight.transform.SetParent(shaft, false);
            shaftLight.transform.localPosition = new Vector3(0, TopY * 0.5f, -0.4f);
            shaftLight.type = LightType.Point;
            shaftLight.range = TopY + 4f;
            shaftLight.intensity = 1.2f;
            shaftLight.color = Palette.Hex(0xFFC98A);
            shaftLight.shadows = LightShadows.None;
        }

        void BuildRoof()
        {
            if (Roof) Destroy(Roof.gameObject);
            Roof = new GameObject("Roof").transform;
            Roof.SetParent(transform, false);
            Roof.localPosition = new Vector3(0, TopY, 0);
            var roof = ModelLibrary.Instantiate("Roof");
            if (roof) roof.transform.SetParent(Roof, false);
            var top = ModelLibrary.Instantiate("ShaftTop");
            if (top)
            {
                top.transform.SetParent(Roof, false);
                top.transform.localPosition = new Vector3(0, 0.4f, 0);
                Pulley = top.transform.Find("Pulley");
            }
            neonText = UiKit.WorldText(Roof, "THE SHUFFLETON", new Vector3(0f, 1.52f, -0.62f), 7.2f, Palette.Hex(0xFF7AA8), UiKit.Display);
            neonText.rectTransform.sizeDelta = new Vector2(9f, 1.6f);
            neonText.fontSharedMaterial = new Material(neonText.fontSharedMaterial);
            neonText.fontSharedMaterial.SetColor("_FaceColor", Palette.Hex(0xFF7AA8) * 2.6f);
            neonText.outlineWidth = 0.12f;
            neonText.outlineColor = new Color32(255, 200, 220, 255);
            var glow = new GameObject("NeonGlow").AddComponent<Light>();
            glow.transform.SetParent(Roof, false);
            glow.transform.localPosition = new Vector3(0, 1.4f, -2.2f);
            glow.type = LightType.Point;
            glow.range = 7f;
            glow.intensity = 2.2f;
            glow.color = Palette.Hex(0xFF6FA8);
            glow.shadows = LightShadows.None;
            neonLight = glow;
        }

        void BuildGround()
        {
            if (ground) Destroy(ground.gameObject);
            ground = new GameObject("Ground").transform;
            ground.SetParent(transform, false);
            var b = ModelLibrary.Instantiate("Base");
            if (b) b.transform.SetParent(ground, false);
            var plaque = UiKit.WorldText(ground, "THE SHUFFLETON · EST. 1931", new Vector3(0, -1.05f, Layout.FrontZ - 0.47f), 2.4f, Palette.Brass, UiKit.Display);
            plaque.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
        }

        Light neonLight;

        public void SetNeon(float on) => neonFlicker = on;

        void Update()
        {
            float t = Time.time;
            float flick = Mathf.PerlinNoise(t * 7f, 0.3f) > 0.08f ? 1f : 0.4f;
            float on = Mathf.Lerp(0.25f, 1f, neonFlicker * flick);
            if (neonText) neonText.alpha = on;
            if (neonLight) neonLight.intensity = 2.2f * on;
        }
    }

    public sealed class Spinner : MonoBehaviour
    {
        public Vector3 Axis = Vector3.up;
        public float Speed;
        public void Update() { if (Speed != 0f) transform.Rotate(Axis, Speed * Time.deltaTime, Space.Self); }
    }
}
