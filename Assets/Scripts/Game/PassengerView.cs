using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// A passenger's body and all of its little animations. The runner tells it where "home" is each frame
    /// (a queue spot on a floor, or a spot in the car); changing parents plays a hop.
    /// </summary>
    public sealed class PassengerView : MonoBehaviour
    {
        [System.NonSerialized] public Passenger P;
        public Transform Visual;     // squash/rotate this
        public Transform Head;
        Transform[] leaves;
        Transform cape, balloons, mirror, propeller;
        Transform legL, legR, armL, armR;
        float walk, stride;
        Vector3[] leafAxes;
        Quaternion[] leafBase;
        float anchorY = 2.3f;
        Renderer[] tintables;
        BoxCollider click;

        enum Mode { Home, Hop, Exit, StormOff, Poof, Gone, Bats }
        Mode mode = Mode.Home;
        Transform homeParent;
        Vector3 homeLocal;
        Vector3 hopFrom, hopTo;
        float t, dur;
        float seed;
        float shake, perk, angry, spawnPop = 1f;
        Spring land;
        Vector3 faceDir = Vector3.back;
        Vector3 exitTarget;

        public bool Leaving => mode == Mode.Exit || mode == Mode.StormOff || mode == Mode.Poof || mode == Mode.Gone || mode == Mode.Bats;
        public Vector3 BubbleAnchor => transform.position + Vector3.up * anchorY * Visual.localScale.y;

        public static PassengerView Create(Passenger p, Transform parent)
        {
            var go = new GameObject($"P{p.Id}_{p.Kind}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PassengerView>();
            v.P = p;
            v.seed = (p.Id * 0.6180339f) % 1f * 10f;
            v.BuildBody();
            v.spawnPop = 0f;
            return v;
        }

        // ---------------------------------------------------------------- bodies

        static readonly Color[] Skins = { Palette.Hex(0xF2C9A0), Palette.Hex(0xD9A066), Palette.Hex(0xA8714A), Palette.Hex(0x6E4630), Palette.Hex(0xF5D5BA) };
        static readonly Color[] Suits = { Palette.Hex(0x3D5A80), Palette.Hex(0x5C6B73), Palette.Hex(0x7A5C45), Palette.Hex(0x2F6F6A) };

        void BuildBody()
        {
            Visual = new GameObject("Visual").transform;
            Visual.SetParent(transform, false);
            var model = ModelLibrary.Character(P.Kind);
            if (model != null)
            {
                model.transform.SetParent(Visual, false);
                Head = model.transform.Find("Head") ?? model.transform;
                cape = model.transform.Find("Cape");
                balloons = model.transform.Find("Balloons");
                mirror = model.transform.Find("Mirror");
                propeller = model.transform.Find("Propeller");
                legL = model.transform.Find("LegL");
                legR = model.transform.Find("LegR");
                armL = model.transform.Find("ArmL");
                armR = model.transform.Find("ArmR");
                var leafRoot = model.transform.Find("Leaves");
                if (leafRoot)
                {
                    leaves = new Transform[leafRoot.childCount];
                    leafAxes = new Vector3[leaves.Length];
                    leafBase = new Quaternion[leaves.Length];
                    for (int i = 0; i < leaves.Length; i++)
                    {
                        leaves[i] = leafRoot.GetChild(i);
                        leafBase[i] = leaves[i].localRotation;
                        // droop axis: horizontal and perpendicular to the leaf's direction
                        var r = leaves[i].GetComponent<Renderer>();
                        var dir = r ? leafRoot.InverseTransformPoint(r.bounds.center) : Vector3.forward;
                        dir.y = 0f;
                        leafAxes[i] = Vector3.Cross(Vector3.up, dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward);
                    }
                }
                ApplyVariant(model);
            }
            else BuildGreybox();
            anchorY = AnchorHeight(P.Kind);

            tintables = Visual.GetComponentsInChildren<Renderer>();
            click = gameObject.AddComponent<BoxCollider>();
            float w = P.Kind == Kind.Mirror ? 1.15f : 0.75f;
            click.center = new Vector3(P.Kind == Kind.Mirror ? 0.25f : 0f, 0.85f, 0f);
            click.size = new Vector3(w, 1.9f, 0.8f);
        }

        static float AnchorHeight(Kind k)
        {
            switch (k)
            {
                case Kind.Houseplant: return 1.95f;
                case Kind.Kid: return 2.45f;
                case Kind.Tycoon: return 2.75f;
                case Kind.Vampire: return 2.4f;
                case Kind.Mirror: return 2.35f;
                default: return 2.3f;
            }
        }

        static readonly uint[] SuitVariants = { 0x3D5A80, 0x5C6B73, 0x7A5C45, 0x2F6F6A, 0x8E2433 };
        static readonly uint[] SkinVariants = { 0xF2C9A0, 0xD9A066, 0xA8714A, 0x6E4630, 0xF5D5BA };
        static readonly uint[] KidShirts = { 0xF07F3C, 0x6CCB5F, 0xF06EAA, 0x3FA9F5 };

        /// <summary>Recolour suits, shirts and skin per passenger so the crowd isn't a clone army.</summary>
        void ApplyVariant(GameObject model)
        {
            int v = P.Variant + P.Id * 7;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string n = mats[i].name;
                    uint? to = null;
                    if (P.Kind == Kind.Commuter && n.StartsWith("col_3D5A80")) to = SuitVariants[v % SuitVariants.Length];
                    else if (n.StartsWith("col_F2C9A0") && P.Kind != Kind.Swimmer) to = SkinVariants[(v / 3) % SkinVariants.Length];
                    else if (P.Kind == Kind.Kid && n.StartsWith("col_F07F3C")) to = KidShirts[v % KidShirts.Length];
                    if (to.HasValue) mats[i] = ModelLibrary.Resolve($"col_{to.Value:X6}_s30");
                }
                r.sharedMaterials = mats;
            }
        }

        void BuildGreybox()
        {
            var skin = Mats.Lit(Skins[P.Variant % Skins.Length], 0.3f);
            var ink = Mats.Lit(Palette.Ink, 0.4f);
            Color bodyCol = Palette.KindColor(P.Kind);
            if (P.Kind == Kind.Commuter) bodyCol = Suits[P.Variant % Suits.Length];
            var body = Mats.Lit(bodyCol, 0.3f);
            float scale = P.Kind == Kind.Kid ? 0.78f : 1f;
            var root = new GameObject("Rig").transform;
            root.SetParent(Visual, false);
            root.localScale = Vector3.one * scale;

            if (P.Kind == Kind.Houseplant)
            {
                var pot = Mats.Lit(Palette.Hex(0xC8643C), 0.25f);
                Prims.Make("Pot", Prims.Cylinder, pot, root, new Vector3(0, 0.32f, 0), new Vector3(0.62f, 0.32f, 0.62f));
                Prims.Make("Rim", Prims.Cylinder, pot, root, new Vector3(0, 0.66f, 0), new Vector3(0.72f, 0.06f, 0.72f));
                Prims.Make("Soil", Prims.Cylinder, Mats.Lit(Palette.Hex(0x4A3020)), root, new Vector3(0, 0.71f, 0), new Vector3(0.6f, 0.02f, 0.6f));
                Head = new GameObject("Head").transform;
                Head.SetParent(root, false);
                Head.localPosition = new Vector3(0, 1.25f, 0);
                Eyes(root, new Vector3(0, 0.42f, -0.31f), 0.14f);
                leaves = new Transform[6];
                var leaf = Mats.Lit(Palette.Hex(0x3FA34D), 0.45f);
                var leafDark = Mats.Lit(Palette.Hex(0x2C7A3B), 0.45f);
                for (int i = 0; i < leaves.Length; i++)
                {
                    var pivot = new GameObject("Leaf" + i).transform;
                    pivot.SetParent(root, false);
                    pivot.localPosition = new Vector3(0, 0.7f, 0);
                    pivot.localRotation = Quaternion.Euler(-28f - 10f * (i % 2), i * 60f, 0);
                    Prims.Make("Stem", Prims.Cylinder, leafDark, pivot, new Vector3(0, 0.3f, 0), new Vector3(0.05f, 0.3f, 0.05f));
                    Prims.Make("Blade", Prims.Sphere, i % 2 == 0 ? leaf : leafDark, pivot, new Vector3(0, 0.72f, 0.05f), new Vector3(0.36f, 0.46f, 0.1f));
                    leaves[i] = pivot;
                }
                return;
            }

            float bodyH = P.Kind == Kind.Tycoon ? 0.68f : 0.62f;
            float bodyW = P.Kind == Kind.Tycoon ? 0.82f : 0.64f;
            Prims.Make("Body", Prims.Capsule, body, root, new Vector3(0, 0.66f, 0), new Vector3(bodyW, bodyH, bodyW * 0.85f));
            var headGo = Prims.Make("Head", Prims.Sphere, P.Kind == Kind.Vampire ? Mats.Lit(Palette.Hex(0xE8E4EE), 0.3f) : skin, root, new Vector3(0, 1.52f, 0), Vector3.one * 0.6f);
            Head = headGo.transform;
            Eyes(Head, new Vector3(0, 0.05f, -0.42f), 0.16f / 0.6f, true);

            switch (P.Kind)
            {
                case Kind.Commuter:
                    Prims.Box("Briefcase", root, new Vector3(0.42f, 0.45f, 0), new Vector3(0.12f, 0.36f, 0.48f), Mats.Lit(Palette.Hex(0x5A3A22), 0.4f));
                    Prims.Box("Tie", root, new Vector3(0, 0.98f, -0.27f), new Vector3(0.1f, 0.3f, 0.04f), Mats.Lit(Palette.Bad, 0.4f));
                    Prims.Make("Hair", Prims.Sphere, ink, Head, new Vector3(0, 0.2f, 0.06f), new Vector3(1.04f, 0.7f, 1.04f));
                    break;
                case Kind.Mirror:
                    Prims.Make("Cap", Prims.Cylinder, Mats.Lit(Palette.Hex(0xE04E39), 0.3f), Head, new Vector3(0, 0.38f, 0), new Vector3(0.95f, 0.18f, 0.95f));
                    Prims.Box("Visor", Head, new Vector3(0, 0.32f, -0.45f), new Vector3(0.7f, 0.05f, 0.4f), Mats.Lit(Palette.Hex(0xE04E39), 0.3f));
                    mirror = new GameObject("Mirror").transform;
                    mirror.SetParent(root, false);
                    mirror.localPosition = new Vector3(0.62f, 1.0f, -0.05f);
                    Prims.Make("Frame", Prims.Cylinder, Mats.Lit(Palette.Brass, 0.8f, 0.9f), mirror, Vector3.zero, new Vector3(0.78f, 0.05f, 1.55f), Quaternion.Euler(90, 0, 0));
                    Prims.Make("Glass", Prims.Cylinder, Mats.Lit(Palette.Hex(0xD9ECF5), 0.97f, 0.9f), mirror, new Vector3(0, 0, -0.03f), new Vector3(0.62f, 0.05f, 1.36f), Quaternion.Euler(90, 0, 0));
                    break;
                case Kind.Vampire:
                    Prims.Make("Hair", Prims.Sphere, ink, Head, new Vector3(0, 0.2f, 0.08f), new Vector3(1.05f, 0.75f, 1.05f));
                    cape = new GameObject("Cape").transform;
                    cape.SetParent(root, false);
                    cape.localPosition = new Vector3(0, 1.25f, 0.22f);
                    Prims.Box("CapeOut", cape, new Vector3(0, -0.55f, 0.05f), new Vector3(0.95f, 1.15f, 0.06f), Mats.Lit(Palette.Hex(0x17121C), 0.5f));
                    Prims.Box("CapeIn", cape, new Vector3(0, -0.55f, 0), new Vector3(0.86f, 1.08f, 0.04f), Mats.Lit(Palette.Hex(0xB3122E), 0.5f));
                    Prims.Box("CollarL", cape, new Vector3(-0.32f, 0.12f, -0.05f), new Vector3(0.3f, 0.42f, 0.05f), Mats.Lit(Palette.Hex(0x17121C), 0.5f));
                    Prims.Box("CollarR", cape, new Vector3(0.32f, 0.12f, -0.05f), new Vector3(0.3f, 0.42f, 0.05f), Mats.Lit(Palette.Hex(0x17121C), 0.5f));
                    break;
                case Kind.Courier:
                    Prims.Make("Cap", Prims.Cylinder, Mats.Lit(Palette.Hex(0x6B4423), 0.3f), Head, new Vector3(0, 0.36f, 0), new Vector3(0.95f, 0.16f, 0.95f));
                    Prims.Box("Parcel", root, new Vector3(0, 0.82f, -0.42f), new Vector3(0.62f, 0.46f, 0.42f), Mats.Lit(Palette.Hex(0xC8955A), 0.2f));
                    Prims.Box("Label", root, new Vector3(0, 0.86f, -0.64f), new Vector3(0.36f, 0.16f, 0.02f), Mats.Lit(Palette.Bad, 0.3f));
                    break;
                case Kind.Swimmer:
                    Prims.Make("SwimCap", Prims.Sphere, Mats.Lit(Palette.Hex(0xF5F5F0), 0.6f), Head, new Vector3(0, 0.16f, 0.04f), new Vector3(1.06f, 0.82f, 1.06f));
                    Prims.Make("GoggleL", Prims.Sphere, Mats.Lit(Palette.Hex(0x2C8BD6), 0.9f), Head, new Vector3(-0.17f, 0.32f, -0.4f), Vector3.one * 0.2f);
                    Prims.Make("GoggleR", Prims.Sphere, Mats.Lit(Palette.Hex(0x2C8BD6), 0.9f), Head, new Vector3(0.17f, 0.32f, -0.4f), Vector3.one * 0.2f);
                    Prims.Box("Towel", root, new Vector3(-0.2f, 1.1f, 0), new Vector3(0.24f, 0.7f, 0.66f), Mats.Lit(Palette.Hex(0xF2F2EA), 0.1f));
                    Prims.Box("Stripe", root, new Vector3(0, 0.62f, -0.3f), new Vector3(0.62f, 0.1f, 0.04f), Mats.Lit(Palette.Bad, 0.3f));
                    break;
                case Kind.Kid:
                    Prims.Make("Beanie", Prims.Sphere, Mats.Lit(Palette.Hex(0x3B7DD8), 0.3f), Head, new Vector3(0, 0.22f, 0.02f), new Vector3(1.04f, 0.6f, 1.04f));
                    Prims.Box("Propeller", Head, new Vector3(0, 0.56f, 0), new Vector3(0.7f, 0.04f, 0.1f), Mats.Lit(Palette.Bad, 0.3f)).AddComponent<Spinner>().Speed = 600f;
                    balloons = new GameObject("Balloons").transform;
                    balloons.SetParent(root, false);
                    balloons.localPosition = new Vector3(0.35f, 0.9f, 0);
                    var cols = new[] { Palette.Bad, Palette.Hex(0xFFD23F), Palette.Hex(0x3FA9F5) };
                    for (int i = 0; i < 3; i++)
                    {
                        var b = Prims.Make("Balloon" + i, Prims.Sphere, Mats.Lit(cols[i], 0.75f), balloons, new Vector3(-0.25f + 0.25f * i, 1.4f + 0.12f * (i % 2), 0.05f * i), new Vector3(0.42f, 0.5f, 0.42f));
                        Prims.Make("String" + i, Prims.Cylinder, ink, balloons, new Vector3(-0.12f + 0.12f * i, 0.7f, 0), new Vector3(0.012f, 0.7f, 0.012f), Quaternion.Euler(0, 0, -12f + 12f * i));
                    }
                    break;
                case Kind.Tycoon:
                    Prims.Make("Hat", Prims.Cylinder, ink, Head, new Vector3(0, 0.62f, 0), new Vector3(0.68f, 0.38f, 0.68f));
                    Prims.Make("Brim", Prims.Cylinder, ink, Head, new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.03f, 1.0f));
                    Prims.Make("Band", Prims.Cylinder, Mats.Lit(Palette.Bad, 0.4f), Head, new Vector3(0, 0.36f, 0), new Vector3(0.7f, 0.06f, 0.7f));
                    Prims.Make("Monocle", Prims.Cylinder, Mats.Lit(Palette.Brass, 0.9f, 0.9f), Head, new Vector3(0.17f, 0.06f, -0.48f), new Vector3(0.24f, 0.02f, 0.24f), Quaternion.Euler(90, 0, 0));
                    Prims.Box("Moustache", Head, new Vector3(0, -0.12f, -0.47f), new Vector3(0.42f, 0.08f, 0.06f), ink);
                    Prims.Make("Chain", Prims.Cylinder, Mats.Lit(Palette.Brass, 0.9f, 0.9f), root, new Vector3(0.15f, 0.7f, -0.38f), new Vector3(0.03f, 0.16f, 0.03f), Quaternion.Euler(0, 0, 70));
                    break;
            }
        }

        static void Eyes(Transform parent, Vector3 center, float size, bool onHead = false)
        {
            var white = Mats.Lit(Color.white, 0.6f);
            var ink = Mats.Lit(Palette.Ink, 0.7f);
            float dx = onHead ? 0.17f : 0.1f;
            foreach (float s in new[] { -1f, 1f })
            {
                var e = Prims.Make("Eye", Prims.Sphere, white, parent, center + new Vector3(s * dx, 0, 0), Vector3.one * size, null, false);
                Prims.Make("Pupil", Prims.Sphere, ink, e.transform, new Vector3(0, -0.05f, -0.38f), Vector3.one * 0.55f, null, false);
            }
        }

        public static Transform BuildBellhop(Transform parent)
        {
            var root = new GameObject("Bellhop").transform;
            root.SetParent(parent, false);
            var red = Mats.Lit(Palette.Hex(0xC0262D), 0.35f);
            var gold = Mats.Lit(Palette.Brass, 0.8f, 0.9f);
            Prims.Make("Body", Prims.Capsule, red, root, new Vector3(0, 0.62f, 0), new Vector3(0.6f, 0.6f, 0.52f));
            var head = Prims.Make("Head", Prims.Sphere, Mats.Lit(Skins[0], 0.3f), root, new Vector3(0, 1.45f, 0), Vector3.one * 0.56f);
            Prims.Make("Hat", Prims.Cylinder, red, head.transform, new Vector3(0, 0.5f, 0), new Vector3(0.66f, 0.2f, 0.66f));
            Prims.Make("HatBand", Prims.Cylinder, gold, head.transform, new Vector3(0, 0.36f, 0), new Vector3(0.68f, 0.05f, 0.68f));
            for (int i = 0; i < 3; i++)
                Prims.Make("Button", Prims.Sphere, gold, root, new Vector3(0, 0.95f - i * 0.2f, -0.27f), Vector3.one * 0.07f, null, false);
            Eyes(head.transform, new Vector3(0, 0.05f, -0.42f), 0.16f / 0.56f, true);
            return root;
        }

        // ---------------------------------------------------------------- driving

        /// <summary>Where this passenger should be. A change of parent plays a hop.</summary>
        public void SetHome(Transform parent, Vector3 local, Vector3 face)
        {
            if (Leaving) return;
            faceDir = face;
            if (parent != homeParent)
            {
                bool first = homeParent == null;
                homeParent = parent;
                homeLocal = local;
                if (first)
                {
                    transform.SetParent(parent, false);
                    transform.localPosition = local;
                    return;
                }
                hopFrom = transform.position;
                transform.SetParent(parent, true);
                mode = Mode.Hop;
                t = 0f;
                dur = 0.36f;
                return;
            }
            homeLocal = local;
        }

        public void PlayExit(Transform floorContent, float dir)
        {
            mode = Mode.Exit;
            transform.SetParent(floorContent, true);
            hopFrom = transform.localPosition;
            exitTarget = new Vector3(hopFrom.x, 0f, Layout.FrontZ + 0.55f);
            t = 0f;
            dur = 2.2f;
            faceDir = new Vector3(-1f, 0, -0.35f);
            perk = 1f;
        }

        public void PlayStormOff()
        {
            mode = Mode.StormOff;
            hopFrom = transform.localPosition;
            t = 0f;
            dur = 1.4f;
            angry = 1f;
            faceDir = new Vector3(1f, 0, -0.2f);
        }

        public void PlayPoof(bool bats)
        {
            mode = bats ? Mode.Bats : Mode.Poof;
            t = 0f;
            dur = bats ? 0.5f : 0.35f;
        }

        public void PlayGone()
        {
            mode = Mode.Gone;
            t = 0f;
            dur = 1.6f;
        }

        public void Shake() => shake = 1f;
        public void Perk() => perk = 1f;
        public void Anger() => angry = 1f;
        public void Land() => land.Velocity -= 3f;

        void Update()
        {
            float dt = Time.deltaTime;
            float time = Time.time + seed;
            shake = Mathf.Max(0f, shake - dt * 2f);
            perk = Mathf.Max(0f, perk - dt * 1.5f);
            angry = Mathf.Max(0f, angry - dt * 0.8f);
            spawnPop = Mathf.Min(1f, spawnPop + dt * 3.5f);
            land.Step(0f, 380f, 16f, dt);

            float patience = P != null ? P.PatienceFrac : 1f;
            bool impatient = P != null && P.State != PState.Done && patience < 0.35f;
            float bobSpeed = impatient ? 14f : 3.2f;
            float bob = Mathf.Sin(time * bobSpeed) * (impatient ? 0.03f : 0.018f);
            float breathe = 1f + Mathf.Sin(time * 2.1f) * 0.02f;
            float sq = land.Value;
            float pop = Ease.OutBack(spawnPop, 2.5f);

            Vector3 pos = transform.localPosition;
            float walkTarget = 0f, cadence = 13f, tuck = 0f;
            switch (mode)
            {
                case Mode.Home:
                {
                    var target = homeLocal;
                    float dist = (pos - target).magnitude;
                    pos = Vector3.MoveTowards(pos, target, dt * 3.4f);
                    pos = Vector3.Lerp(pos, target, Ease.Damp(10f, dt));
                    transform.localPosition = pos;
                    if (dist > 0.05f) { bob += Mathf.Abs(Mathf.Sin(time * 16f)) * 0.06f; walkTarget = 1f; }
                    break;
                }
                case Mode.Hop:
                {
                    t += dt / dur;
                    var to = homeParent ? homeParent.TransformPoint(homeLocal) : hopFrom;
                    float k = Ease.InOutCubic(t);
                    var p = Vector3.Lerp(hopFrom, to, k) + Vector3.up * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.75f;
                    transform.position = p;
                    sq = -0.12f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    tuck = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    if (t >= 1f) { mode = Mode.Home; transform.localPosition = homeLocal; Land(); }
                    break;
                }
                case Mode.Exit:
                {
                    t += dt / dur;
                    // hop out onto the landing, then walk off into the left wing and fade away
                    if (t < 0.18f)
                    {
                        float k = t / 0.18f;
                        transform.localPosition = Vector3.Lerp(hopFrom, exitTarget, Ease.OutCubic(k)) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.55f;
                        tuck = Mathf.Sin(k * Mathf.PI);
                    }
                    else
                    {
                        float k = (t - 0.18f) / 0.82f;
                        transform.localPosition = exitTarget + new Vector3(-k * 5.2f, Mathf.Abs(Mathf.Sin(stride)) * 0.07f, k * 0.8f);
                        walkTarget = 1f;
                    }
                    if (t > 0.8f) pop *= 1f - Ease.InCubic((t - 0.8f) / 0.2f);
                    if (t >= 1f) Destroy(gameObject);
                    break;
                }
                case Mode.StormOff:
                {
                    t += dt / dur;
                    transform.localPosition = hopFrom + new Vector3(t * 6f, Mathf.Abs(Mathf.Sin(stride)) * 0.12f, 0);
                    walkTarget = 1f;
                    cadence = 20f;
                    if (t > 0.7f) pop *= 1f - Ease.InCubic((t - 0.7f) / 0.3f);
                    if (t >= 1f) Destroy(gameObject);
                    break;
                }
                case Mode.Poof:
                case Mode.Bats:
                {
                    t += dt / dur;
                    pop = 1f + 0.4f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) - Ease.InCubic(t);
                    if (t >= 1f) Destroy(gameObject);
                    break;
                }
                case Mode.Gone:
                {
                    t += dt / dur;
                    if (t >= 1f) Destroy(gameObject);
                    break;
                }
            }

            // face direction
            var look = faceDir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(faceDir) : Quaternion.identity;
            float headShake = Mathf.Sin(time * 38f) * 25f * shake;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, look, Ease.Damp(9f, dt));
            if (Head) Head.localRotation = Quaternion.Euler(0, headShake, 0);

            float stretch = 1f + sq + perk * 0.12f * Mathf.Sin(perk * Mathf.PI * 3f);
            Visual.localScale = new Vector3(pop * breathe / Mathf.Sqrt(Mathf.Max(0.2f, stretch)), pop * stretch, pop * breathe / Mathf.Sqrt(Mathf.Max(0.2f, stretch)));
            Visual.localPosition = new Vector3(Mathf.Sin(time * 31f) * 0.02f * angry, bob, 0f);

            AnimateLimbs(dt, time, walkTarget, cadence, tuck, impatient && mode == Mode.Home);

            // kind-specific secondary motion
            if (leaves != null)
            {
                float droop = P != null && P.Kind == Kind.Houseplant ? Mathf.Lerp(40f, -4f, patience) : 0f;
                if (P != null && P.Sunned) droop -= 8f;
                for (int i = 0; i < leaves.Length; i++)
                {
                    float sway = Mathf.Sin(time * 2.3f + i) * 4f + perk * 16f * Mathf.Sin(time * 20f + i);
                    if (leafAxes != null)
                        leaves[i].localRotation = Quaternion.AngleAxis(droop + sway, leafAxes[i]) * leafBase[i];
                    else
                        leaves[i].localRotation = Quaternion.Euler(-28f - 10f * (i % 2) + droop + sway, i * 60f, 0);
                }
            }
            if (propeller) propeller.localRotation *= Quaternion.Euler(0, (impatient ? 1400f : 520f) * dt, 0);
            if (mirror) mirror.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 1.4f) * 1.5f + (mode == Mode.Hop ? 8f : 0f));
            if (cape) cape.localRotation = Quaternion.Euler(Mathf.Sin(time * 2f) * 4f + (mode == Mode.Hop ? 25f : 0f) + angry * 20f, 0, 0);
            if (balloons) balloons.localRotation = Quaternion.Euler(Mathf.Sin(time * 1.7f) * 7f, 0, Mathf.Sin(time * 1.3f) * 9f);
        }

        /// <summary>
        /// Legs and arms are separate pivoted parts (hip / shoulder). Positive X tips a hanging limb backward,
        /// positive Z swings the left arm (+x side) outward.
        /// </summary>
        void AnimateLimbs(float dt, float time, float walkTarget, float cadence, float tuck, bool tapping)
        {
            if (!legL && !armL && !armR) return;
            walk = Mathf.MoveTowards(walk, walkTarget, dt * 6f);
            stride += dt * cadence * Mathf.Max(walk, 0.0001f);
            float swing = Mathf.Sin(stride) * 34f * walk;
            float tap = tapping ? Mathf.Max(0f, Mathf.Sin(time * 11f)) * 16f : 0f;
            if (legL) legL.localRotation = Quaternion.Euler(swing - 34f * tuck, 0, 0);
            if (legR) legR.localRotation = Quaternion.Euler(-swing - 30f * tuck - tap, 0, 0);

            float cheer = Ease.OutCubic(Mathf.Clamp01(perk * 1.6f));
            float mad = Mathf.Clamp01(angry * 1.5f);
            float idle = 4f + Mathf.Sin(time * 2.1f) * 2f + (tapping ? 14f : 0f);
            float outward = idle + 60f * tuck + 150f * cheer;
            float fist = -75f * mad + Mathf.Sin(time * 30f) * 18f * mad;
            if (armL) armL.localRotation = Quaternion.Euler(-swing * 0.8f + fist, 0, outward);
            if (armR) armR.localRotation = Quaternion.Euler(swing * 0.8f + fist, 0, -outward);
        }
    }
}
