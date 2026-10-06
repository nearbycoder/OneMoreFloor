using OneMoreFloor.Core;
using TMPro;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// One floor module. The transform sits at the floor's resting slot height; Content is offset during
    /// shuffles, departures and arrivals so waiting passengers (children of Content) ride along.
    /// </summary>
    public sealed class FloorView : MonoBehaviour
    {
        public static float LampScale = 1f;
        public FloorId Id;
        public int Slot = -1;
        public Transform Content;
        public Transform QueueRoot;
        public BoxCollider Click;
        public bool InBuilding;

        TMP_Text signText, numberText, leavingText;
        GameObject leavingSign;
        Renderer[] highlightRenderers;
        Light lamp;
        float lampBase;
        readonly System.Collections.Generic.List<Transform> drums = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<Transform> needles = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<Transform> waves = new System.Collections.Generic.List<Transform>();
        Vector3[] waveBase;

        // shuffle animation
        float animT = 1f, animDur = 0.7f, fromY, toY, popZ, delay;
        int mode; // 0 idle, 1 shuffle, 2 depart, 3 arrive
        float departDir = 1f;
        float hover, hoverTarget, flash;
        Spring bounce;

        public float RestY => Layout.SlotY(Slot);
        public bool Busy => animT < 1f;

        public static FloorView Create(FloorId id, Transform parent)
        {
            var go = new GameObject("Floor_" + id);
            go.transform.SetParent(parent, false);
            var fv = go.AddComponent<FloorView>();
            fv.Id = id;
            fv.Build();
            return fv;
        }

        void Build()
        {
            Content = new GameObject("Content").transform;
            Content.SetParent(transform, false);
            QueueRoot = new GameObject("Queue").transform;
            QueueRoot.SetParent(Content, false);

            var col = Palette.Floor(Id);
            var model = ModelLibrary.Floor(Id);
            if (model != null)
            {
                model.transform.SetParent(Content, false);
                foreach (Transform c in model.transform)
                {
                    if (c.name.StartsWith("Drum")) drums.Add(c);
                    else if (c.name.StartsWith("Needle")) needles.Add(c);
                    else if (c.name.StartsWith("Wave")) waves.Add(c);
                }
            }
            else BuildGreybox(col);

            // Sign above the shaft opening: floor name on a plaque in its colour, slot number beside it.
            var plaque = Prims.Box("Plaque", Content, new Vector3(0f, 2.27f, -1.6f), new Vector3(3.0f, 0.4f, 0.06f), Mats.Lit(col * 0.85f, 0.5f));
            plaque.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            signText = UiKit.WorldText(Content, Defs.Floor(Id).Name.ToUpperInvariant(), new Vector3(0.18f, 2.27f, -1.66f), 2.2f, Palette.Cream, UiKit.Signage);
            var numPlate = Prims.Make("NumPlate", Prims.Cylinder, Mats.Lit(Palette.Brass, 0.75f, 0.85f), Content,
                new Vector3(-1.3f, 2.27f, -1.66f), new Vector3(0.46f, 0.05f, 0.46f), Quaternion.Euler(90, 0, 0), false);
            numberText = UiKit.WorldText(Content, "1", new Vector3(-1.3f, 2.25f, -1.72f), 2.8f, Palette.Ink, UiKit.Signage);

            // "Leaving in N" sign that drops down when the floor is checking out
            leavingSign = new GameObject("LeavingSign");
            leavingSign.transform.SetParent(Content, false);
            leavingSign.transform.localPosition = new Vector3(4.9f, 2.15f, Layout.FrontZ - 0.05f);
            Prims.Box("Board", leavingSign.transform, Vector3.zero, new Vector3(2.4f, 0.62f, 0.06f), Mats.Lit(Palette.Hex(0xFFD23F), 0.4f));
            Prims.Box("Stripe", leavingSign.transform, new Vector3(0, -0.27f, -0.01f), new Vector3(2.4f, 0.08f, 0.06f), Mats.Lit(Palette.Ink));
            leavingText = UiKit.WorldText(leavingSign.transform, "LEAVING IN 5", new Vector3(0, 0.02f, -0.05f), 2.2f, Palette.Ink, UiKit.Signage);
            leavingSign.SetActive(false);

            lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(Content, false);
            lamp.transform.localPosition = new Vector3(3.6f, 2.1f, -0.3f);
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.color = Color.Lerp(Palette.Hex(0xFFD9A0), col, 0.25f);
            lampBase = Id == FloorId.Crypt ? 1.4f : Id == FloorId.Ocean || Id == FloorId.Greenhouse ? 0.6f : 2.2f;
            lamp.intensity = lampBase;
            lamp.shadows = LightShadows.None;

            Click = gameObject.AddComponent<BoxCollider>();
            Click.center = new Vector3(0f, Layout.SlotHeight * 0.5f - 0.15f, 0.4f);
            Click.size = new Vector3(Layout.HalfWidth * 2f, Layout.SlotHeight, Layout.BackZ - Layout.FrontZ);

            highlightRenderers = Content.GetComponentsInChildren<Renderer>();
        }

        void BuildGreybox(Color col)
        {
            var wall = Mats.Lit(Color.Lerp(col, Palette.Cream, 0.55f), 0.15f);
            var wallDark = Mats.Lit(Color.Lerp(col, Palette.Ink, 0.25f), 0.2f);
            var slab = Mats.Lit(Palette.Hex(0xE9DCC2), 0.2f);
            var brick = Mats.Lit(Palette.Oxblood, 0.1f);
            var trim = Mats.Lit(Palette.Brass, 0.7f, 0.8f);
            float h = Layout.SlotHeight - Layout.SlabThickness;
            float L = -Layout.HalfWidth, R = Layout.HalfWidth, S = Layout.ShaftHalf;

            // slabs either side of the shaft + landing strip in front of it
            Prims.BoxMinMax("SlabL", Content, new Vector3(L, -Layout.SlabThickness, Layout.FrontZ), new Vector3(-S, 0, Layout.BackZ), slab);
            Prims.BoxMinMax("SlabR", Content, new Vector3(S, -Layout.SlabThickness, Layout.FrontZ), new Vector3(R, 0, Layout.BackZ), slab);
            Prims.BoxMinMax("Landing", Content, new Vector3(-S, -Layout.SlabThickness, Layout.FrontZ), new Vector3(S, 0, -1.35f), slab);
            Prims.BoxMinMax("TrimL", Content, new Vector3(L, -Layout.SlabThickness, Layout.FrontZ - 0.06f), new Vector3(R, -Layout.SlabThickness + 0.1f, Layout.FrontZ), trim);
            // back walls, side brick walls
            Prims.BoxMinMax("BackL", Content, new Vector3(L, 0, Layout.BackZ - 0.12f), new Vector3(-S, h, Layout.BackZ), wall);
            Prims.BoxMinMax("BackR", Content, new Vector3(S, 0, Layout.BackZ - 0.12f), new Vector3(R, h, Layout.BackZ), wall);
            Prims.BoxMinMax("SideL", Content, new Vector3(L - 0.3f, -Layout.SlabThickness, Layout.FrontZ), new Vector3(L, h, Layout.BackZ), brick);
            Prims.BoxMinMax("SideR", Content, new Vector3(R, -Layout.SlabThickness, Layout.FrontZ), new Vector3(R + 0.3f, h, Layout.BackZ), brick);
            // walls flanking the shaft
            Prims.BoxMinMax("ShaftWallL", Content, new Vector3(-S - 0.12f, 0, -1.35f), new Vector3(-S, h, Layout.BackZ), wallDark);
            Prims.BoxMinMax("ShaftWallR", Content, new Vector3(S, 0, -1.35f), new Vector3(S + 0.12f, h, Layout.BackZ), wallDark);
            // a couple of identity props in the signature colour
            var prop = Mats.Lit(col, 0.3f);
            Prims.BoxMinMax("PropL", Content, new Vector3(-6.4f, 0, 1.2f), new Vector3(-4.2f, 1.1f, 2.6f), prop);
            Prims.BoxMinMax("PropL2", Content, new Vector3(-3.6f, 0, 1.8f), new Vector3(-2.6f, 2.0f, 2.7f), prop);
            Prims.BoxMinMax("PropR", Content, new Vector3(5.6f, 0, 1.4f), new Vector3(7.2f, 1.6f, 2.7f), prop);
        }

        public void SetSlot(int slot, bool snap)
        {
            Slot = slot;
            numberText.text = (slot + 1).ToString();
            if (snap)
            {
                transform.localPosition = new Vector3(0f, RestY, 0f);
                Content.localPosition = Vector3.zero;
                animT = 1f;
                mode = 0;
            }
        }

        /// <summary>Animate from the current slot to a new one. Floors going up pass in front, down pass behind.</summary>
        public void ShuffleTo(int slot, float duration, float startDelay)
        {
            fromY = transform.localPosition.y + Content.localPosition.y;
            Slot = slot;
            numberText.text = (slot + 1).ToString();
            toY = RestY;
            popZ = toY > fromY ? Layout.PopOutZ : -Layout.PopOutZ * 0.9f;
            if (Mathf.Abs(toY - fromY) < 0.01f) popZ = 0f;
            animDur = duration;
            delay = startDelay;
            animT = 0f;
            mode = 1;
            transform.localPosition = new Vector3(0f, toY, 0f);
            Content.localPosition = new Vector3(0f, fromY - toY, 0f);
        }

        public void Depart(float dir)
        {
            departDir = dir;
            animT = 0f;
            animDur = 1.1f;
            delay = 0f;
            mode = 2;
            leavingSign.SetActive(false);
        }

        public void Arrive(int slot, float dir)
        {
            InBuilding = true;
            gameObject.SetActive(true);
            SetSlot(slot, true);
            departDir = dir;
            animT = 0f;
            animDur = 0.9f;
            delay = 0.35f;
            mode = 3;
            Content.localPosition = new Vector3(-dir * 30f, 1.5f, 0f);
        }

        public void SetLeaving(int stops)
        {
            if (stops < 0) { leavingSign.SetActive(false); return; }
            leavingSign.SetActive(true);
            leavingText.text = Id == FloorId.Ocean
                ? (stops <= 1 ? "TIDE OUT NEXT STOP" : $"TIDE OUT IN {stops}")
                : (stops <= 1 ? "LEAVING NEXT STOP" : stops == 0 ? "LEAVING!" : $"LEAVING IN {stops}");
            flash = 1f;
        }

        void AnimateProps(float dt)
        {
            float t = Time.time;
            for (int i = 0; i < drums.Count; i++) drums[i].localRotation *= Quaternion.Euler(0, 0, (i % 2 == 0 ? 240f : -180f) * dt);
            for (int i = 0; i < needles.Count; i++)
                needles[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * (1.3f + i) + i) * 35f + Mathf.PerlinNoise(t * 3f, i) * 20f);
            if (waves.Count > 0)
            {
                if (waveBase == null) { waveBase = new Vector3[waves.Count]; for (int i = 0; i < waves.Count; i++) waveBase[i] = waves[i].localPosition; }
                for (int i = 0; i < waves.Count; i++)
                    waves[i].localPosition = waveBase[i] + new Vector3(Mathf.Sin(t * 0.6f + i) * 0.35f, Mathf.Sin(t * 1.7f + i * 0.9f) * 0.06f, Mathf.Sin(t * 0.9f + i * 1.3f) * 0.08f);
            }
        }

        public void SetHover(bool on) => hoverTarget = on ? 1f : 0f;
        public void Flash(float amount = 1f) => flash = Mathf.Max(flash, amount);
        /// <summary>Mid shuffle, departure or arrival.</summary>
        public bool Moving => mode != 0;

        public void Kick(float v) { if (!SaveData.Current.ReducedMotion) bounce.Velocity += v; }

        void Update()
        {
            float dt = Time.deltaTime;
            hover = Mathf.Lerp(hover, hoverTarget, Ease.Damp(14f, dt));
            flash = Mathf.Max(0f, flash - dt * 1.8f);
            if (lamp) { lamp.intensity = lampBase * LampScale * (1f + 0.35f * hover + 0.6f * flash); lamp.range = 9f + 3f * (LampScale - 1f); }
            bounce.Step(0f, 260f, 14f, dt);
            AnimateProps(dt);

            if (mode == 0)
            {
                Content.localPosition = new Vector3(0f, bounce.Value, 0f);
                return;
            }
            if (delay > 0f) { delay -= dt; return; }
            animT = Mathf.Min(1f, animT + dt / animDur);
            float t = animT;
            switch (mode)
            {
                case 1:
                {
                    // out (0-0.3), across (0.15-0.8), back in with a bounce (0.65-1)
                    float zOut = Ease.OutCubic(t / 0.3f);
                    bool still = SaveData.Current.ReducedMotion;
                    float zIn = still ? Ease.OutCubic(Mathf.Clamp01((t - 0.65f) / 0.35f)) : Ease.OutBack((t - 0.65f) / 0.35f, 2.2f);
                    float z = popZ * (zOut - zIn);
                    float y = Mathf.Lerp(fromY - toY, 0f, Ease.InOutCubic((t - 0.15f) / 0.65f));
                    Content.localPosition = new Vector3(0f, y + bounce.Value, z);
                    Content.localRotation = Quaternion.Euler(0f, 0f, still ? 0f : Mathf.Sin(t * Mathf.PI) * (popZ < 0 ? 1.2f : -1.2f));
                    if (animT >= 1f) { mode = 0; Content.localRotation = Quaternion.identity; Kick(-3.5f); }
                    break;
                }
                case 2:
                {
                    // a shudder, then it lifts off and drifts away into the sky
                    float shake = t < 0.25f ? Mathf.Sin(t * 120f) * 0.06f * (1f - t / 0.25f) : 0f;
                    float go = Ease.InCubic((t - 0.2f) / 0.8f);
                    Content.localPosition = new Vector3(departDir * 34f * go + shake, 9f * go, -2f * go);
                    Content.localRotation = Quaternion.Euler(0f, 0f, -departDir * 8f * go);
                    if (animT >= 1f)
                    {
                        mode = 0;
                        InBuilding = false;
                        gameObject.SetActive(false);
                        Content.localRotation = Quaternion.identity;
                    }
                    break;
                }
                case 3:
                {
                    float k = Ease.OutBack(t, 1.4f);
                    Content.localPosition = new Vector3(-departDir * 30f * (1f - k), 1.5f * (1f - Ease.OutCubic(t)), 0f);
                    Content.localRotation = Quaternion.Euler(0f, 0f, departDir * 6f * (1f - Ease.OutCubic(t)));
                    if (animT >= 1f) { mode = 0; Content.localRotation = Quaternion.identity; Kick(-4f); }
                    break;
                }
            }
        }
    }
}
