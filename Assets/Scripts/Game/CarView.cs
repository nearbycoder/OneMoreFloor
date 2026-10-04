using OneMoreFloor.Core;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>The elevator car: follows the sim's position with squash-and-stretch, doors, cables, counterweight.</summary>
    public sealed class CarView : MonoBehaviour
    {
        public Transform Body;      // squashes
        public Transform Riders;    // passengers parent (inside Body)
        public Transform Bellhop;
        Transform doorL, doorR, cableRoot;
        Transform cable;
        Light interior;
        Spinner pulley;
        float doorOpen;        // 0 closed .. 1 open
        float shaftTop;
        Spring squash, tilt;
        float lastVel;
        Transform bellhopHead, bellhopArmL, bellhopArmR;
        public Transform Pulley;
        float bellhopCheer, bellhopWorry;

        public static CarView Create(Transform parent)
        {
            var go = new GameObject("Car");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CarView>();
            c.Build();
            return c;
        }

        void Build()
        {
            Body = new GameObject("Body").transform;
            Body.SetParent(transform, false);
            float W = Layout.CarWidth, H = Layout.CarHeight, D = Layout.CarDepth;
            var model = ModelLibrary.Instantiate("Car");
            if (model != null)
            {
                model.transform.SetParent(Body, false);
                doorL = model.transform.Find("DoorL");
                doorR = model.transform.Find("DoorR");
            }
            else
            {
                var brass = Mats.Lit(Palette.Brass, 0.72f, 0.9f);
                var wood = Mats.Lit(Palette.Hex(0x7A4A2E), 0.35f);
                var carpet = Mats.Lit(Palette.Hex(0x8E2B3A), 0.1f);
                Prims.BoxMinMax("Floor", Body, new Vector3(-W / 2, -0.12f, -D / 2), new Vector3(W / 2, 0f, D / 2), carpet);
                Prims.BoxMinMax("Back", Body, new Vector3(-W / 2, 0, D / 2 - 0.1f), new Vector3(W / 2, H, D / 2), wood);
                Prims.BoxMinMax("SideL", Body, new Vector3(-W / 2, 0, -D / 2), new Vector3(-W / 2 + 0.1f, H, D / 2), wood);
                Prims.BoxMinMax("SideR", Body, new Vector3(W / 2 - 0.1f, 0, -D / 2), new Vector3(W / 2, H, D / 2), wood);
                Prims.BoxMinMax("Roof", Body, new Vector3(-W / 2 - 0.08f, H, -D / 2 - 0.08f), new Vector3(W / 2 + 0.08f, H + 0.18f, D / 2 + 0.08f), brass);
                Prims.BoxMinMax("Sill", Body, new Vector3(-W / 2 - 0.08f, -0.2f, -D / 2 - 0.08f), new Vector3(W / 2 + 0.08f, 0.02f, -D / 2 + 0.05f), brass);
                Prims.BoxMinMax("Rail", Body, new Vector3(-W / 2 + 0.12f, 0.95f, D / 2 - 0.2f), new Vector3(W / 2 - 0.12f, 1.02f, D / 2 - 0.12f), brass);
                doorL = MakeDoor("DoorL", -1);
                doorR = MakeDoor("DoorR", 1);
            }
            Riders = new GameObject("Riders").transform;
            Riders.SetParent(Body, false);

            interior = new GameObject("Interior").AddComponent<Light>();
            interior.transform.SetParent(Body, false);
            interior.transform.localPosition = new Vector3(0, H - 0.3f, 0.2f);
            interior.type = LightType.Point;
            interior.range = 4.5f;
            interior.intensity = 2.6f;
            interior.color = Palette.Hex(0xFFE2B0);
            interior.shadows = LightShadows.None;

            var bell = ModelLibrary.Instantiate("Char_Bellhop");
            if (bell != null)
            {
                bell.transform.SetParent(Body, false);
                Bellhop = bell.transform;
                bellhopHead = Bellhop.Find("Head");
                bellhopArmL = Bellhop.Find("ArmL");
                bellhopArmR = Bellhop.Find("ArmR");
            }
            else Bellhop = PassengerView.BuildBellhop(Body);
            Bellhop.localPosition = new Vector3(-1.35f, 0.02f, 0.75f);
            Bellhop.localRotation = Quaternion.Euler(0, 165, 0);
        }

        /// <summary>An Art Deco accordion gate anchored at the car's side; it folds up toward that side to open.</summary>
        Transform MakeDoor(string name, int side)
        {
            float W = Layout.CarWidth, H = Layout.CarHeight, D = Layout.CarDepth;
            var root = new GameObject(name).transform;
            root.SetParent(Body, false);
            root.localPosition = new Vector3(side * (W / 2 - 0.12f), 0, -D / 2 - 0.04f);
            var brass = Mats.Lit(Palette.Brass, 0.72f, 0.9f);
            float w = W / 2f - 0.1f;
            int bars = 6;
            for (int i = 0; i <= bars; i++)
            {
                float x = -side * w * i / bars;
                Prims.Box("Bar" + i, root, new Vector3(x, H / 2, 0), new Vector3(0.05f, H - 0.08f, 0.05f), brass, false);
                if (i == bars) break;
                float xm = -side * w * (i + 0.5f) / bars;
                float seg = w / bars;
                float len = Mathf.Sqrt(seg * seg + 0.36f * 0.36f) * 1.02f;
                float ang = Mathf.Atan2(0.36f, seg) * Mathf.Rad2Deg;
                for (int k = 0; k < 6; k++)
                {
                    float y = 0.3f + k * 0.42f;
                    Prims.Make("X", Prims.Cube, brass, root, new Vector3(xm, y, 0), new Vector3(len, 0.035f, 0.035f), Quaternion.Euler(0, 0, ((k % 2 == 0) ? ang : -ang)), false);
                }
            }
            Prims.Box("RailT", root, new Vector3(-side * w / 2, H - 0.06f, 0), new Vector3(w, 0.07f, 0.06f), brass, false);
            Prims.Box("RailB", root, new Vector3(-side * w / 2, 0.06f, 0), new Vector3(w, 0.07f, 0.06f), brass, false);
            return root;
        }

        public void Setup(float shaftTopY)
        {
            shaftTop = shaftTopY;
            if (cableRoot) Destroy(cableRoot.gameObject);
            cableRoot = new GameObject("Cables").transform;
            cableRoot.SetParent(transform.parent, false);
            var steel = Mats.Lit(Palette.Hex(0x2C2C30), 0.6f, 0.9f);
            cable = Prims.Make("Cable", Prims.Cylinder, steel, cableRoot, Vector3.zero, Vector3.one, null, false).transform;

        }

        public void Cheer() => bellhopCheer = 1f;

        /// <summary>Drive the visuals from the sim. Called every frame by the runner.</summary>
        public void Sync(ShiftSim sim, float dt)
        {
            var car = sim.Car;
            float y = Layout.SlotY(car.Pos);
            transform.localPosition = new Vector3(0f, y, Layout.CarZ);

            // squash on acceleration, stretch at speed, spring bounce on stopping
            float accel = dt > 1e-4f ? (car.Vel - lastVel) / Mathf.Max(dt, 1f / 120f) : 0f;
            lastVel = car.Vel;
            squash.Step(Mathf.Clamp(-accel * 0.0016f, -0.08f, 0.08f) + Mathf.Abs(car.Vel) * 0.006f, 320f, 13f, dt);
            if (Mathf.Abs(accel) > 200f) squash.Velocity += -Mathf.Sign(accel) * 0.02f;
            float s = squash.Value;
            Body.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            tilt.Step(0f, 200f, 10f, dt);
            Body.localRotation = Quaternion.Euler(0f, 0f, tilt.Value);

            // doors
            float target;
            switch (car.State)
            {
                case CarState.Docked: case CarState.Opening: target = 1f; break;
                case CarState.QuickStop: target = car.Phase <= 1 ? 1f : 0f; break;
                default: target = 0f; break;
            }
            float speed = target > doorOpen ? 1f / Tuning.DoorOpen : 1f / Tuning.DoorClose;
            if (car.State == CarState.QuickStop) speed *= 1.6f;
            doorOpen = Mathf.MoveTowards(doorOpen, target, speed * dt);
            float k = target > 0.5f ? Ease.OutBack(doorOpen, 1.2f) : Ease.InOutCubic(doorOpen);
            float fold = Mathf.Lerp(1f, 0.12f, k);
            if (doorL) doorL.localScale = new Vector3(fold, 1f, 1f);
            if (doorR) doorR.localScale = new Vector3(fold, 1f, 1f);

            // cables & counterweight
            if (cable)
            {
                float top = shaftTop;
                float bottom = y + Layout.CarHeight + 0.2f;
                cable.localPosition = new Vector3(0f, (top + bottom) * 0.5f, Layout.CarZ);
                cable.localScale = new Vector3(0.06f, Mathf.Max(0.01f, (top - bottom) * 0.5f), 0.06f);

            }
            if (interior) interior.intensity = 2.6f + 0.5f * Mathf.Sin(Time.time * 1.7f) * 0.1f;
            if (Pulley) Pulley.localRotation *= Quaternion.Euler(-car.Vel * Layout.SlotHeight / 0.82f * Mathf.Rad2Deg * dt, 0, 0);

            // bellhop: salute when the doors open, sweat when it's going badly
            bellhopCheer = Mathf.Max(0f, bellhopCheer - dt * 1.5f);
            bellhopWorry = Mathf.Lerp(bellhopWorry, sim.Trouble, Ease.Damp(2f, dt));
            if (Bellhop)
            {
                float hop = Mathf.Abs(Mathf.Sin(bellhopCheer * Mathf.PI * 3f)) * 0.25f * bellhopCheer;
                float jitter = bellhopWorry > 0.6f ? Mathf.Sin(Time.time * 40f) * 0.02f * bellhopWorry : 0f;
                Bellhop.localPosition = new Vector3(-1.35f + jitter, 0.02f + hop, 0.75f);
                if (bellhopHead) bellhopHead.localRotation = Quaternion.Euler(-12f * bellhopCheer, Mathf.Sin(Time.time * 0.7f) * 12f, 0);
                // a crisp salute with the right hand, the left arm nervously fidgeting when things go badly
                float salute = Ease.OutCubic(Mathf.Clamp01(bellhopCheer * 2.2f));
                if (bellhopArmR) bellhopArmR.localRotation = Quaternion.Euler(-150f * salute, 0, -6f - 28f * salute);
                if (bellhopArmL) bellhopArmL.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 9f) * 14f * Mathf.Clamp01(bellhopWorry - 0.5f) * 2f, 0, 6f);
            }
        }

        public void Bump(float amount) => squash.Velocity -= amount;
        public void Rattle(float amount) => tilt.Velocity += amount;
    }
}
