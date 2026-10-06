using System.Collections.Generic;
using OneMoreFloor.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace OneMoreFloor
{
    /// <summary>
    /// Owns the running ShiftSim: ticks it at a fixed rate, turns its events into animation and sound,
    /// and turns clicks and keys into commands.
    /// </summary>
    public sealed class ShiftRunner : MonoBehaviour
    {
        public const float Step = 1f / 60f;

        [System.NonSerialized] public ShiftSim Sim;
        public BuildingView Building;
        public CarView Car;
        public Hud Hud;
        public CameraRig Rig;
        public Camera Cam;
        public bool Paused;
        public bool InputEnabled = true;
        /// <summary>Title-screen attract mode: the bot plays silently with no HUD and no input.</summary>
        public bool Attract;
        public float TimeScale = 1f;
        public System.Action<ShiftSim> Ended;
        public System.Action<SimEvent> OnEvent;
        /// <summary>The local playtest log for the player's shifts (see <see cref="Telemetry"/>).</summary>
        public readonly Telemetry Log = new Telemetry();
        /// <summary>When set, the bot plays (autopilot, attract mode, captures).</summary>
        [System.NonSerialized] public Bot AutoBot;

        public FloorId? HoverFloor { get; private set; }
        public int HoverPid { get; private set; } = -1;
        /// <summary>Gamepad / arrow-key play: the selected floor slot (-1 = none) and selected guest (-1 = none).</summary>
        public int CursorSlot { get; private set; } = -1;
        public int CursorPid { get; private set; } = -1;
        /// <summary>The floor and guest cursors are in charge (the player is on a gamepad or the arrow keys).</summary>
        public bool CursorMode => Controls.KeyNav && !Attract;
        /// <summary>Fired for each command the AutoBot issues (action, pid or slot).</summary>
        public event System.Action<Bot.Action, int> BotActed;
        /// <summary>
        /// Recordings only: runs before the bot on every fixed step (and in FastForward), so scripted commands land
        /// on the same simulation tick in every pass. Return true to skip the bot for that step.
        /// </summary>
        [System.NonSerialized] public System.Func<ShiftSim, bool> PreStep;
        bool quiet;

        readonly Dictionary<int, PassengerView> views = new Dictionary<int, PassengerView>();
        float acc;
        float endTimer = -1f;
        bool endFired;
        int lastGroupStop = -1, groupCount;

        public PassengerView ViewOf(int pid) => views.TryGetValue(pid, out var v) && v ? v : null;

        public void Begin(ShiftDef def, ulong seed, bool relaxed = false)
        {
            foreach (var v in views.Values) if (v) Destroy(v.gameObject);
            views.Clear();
            Sim = new ShiftSim(def, seed, relaxed);
            if (!Attract) Log.Begin(Sim, seed);
            Building.Setup(Sim.B);
            Car.Setup(Building.PulleyY - 0.82f);
            Car.Pulley = Building.Pulley;
            Hud.Clear();
            Hud.SetShiftName(def.Day, def.Title);
            Rig.Frame(Building);
            acc = 0f;
            endTimer = -1f;
            endFired = false;
            Paused = false;
            CursorSlot = CursorPid = -1;
            Drain();
            Car.Sync(Sim, 0.016f);
            SyncViews(1f);
        }

        void Update()
        {
            if (Sim == null) return;
            float dt = Time.deltaTime;
            if (!Paused && !Sim.Ended)
            {
                acc += Mathf.Min(dt, 0.25f) * TimeScale;
                int guard = 0;
                while (acc >= Step && guard++ < 90)
                {
                    bool scripted = PreStep != null && PreStep(Sim);
                    if (AutoBot != null && !scripted)
                    {
                        var a = AutoBot.Tick(Sim, Step, out int arg);
                        if (a == Bot.Action.Send) Hud.Panel.Press(arg);
                        if (a != Bot.Action.None) BotActed?.Invoke(a, arg);
                    }
                    Sim.Tick(Step);
                    acc -= Step;
                    Drain();
                }
                if (acc > Step * 4f) acc = Step * 4f; // never spiral after a hitch
            }
            if (InputEnabled && !Paused && !Attract) HandleInput();
            if (!Paused && !Attract && AutoBot == null) Log.Tick(Rig.Zoom);
            SyncViews(dt);
            Car.Sync(Sim, Paused ? 0f : dt);
            UpdateFollow();
            Hud.Tick(dt);
            if (Mathf.Abs(Sim.Car.Vel) > 0.1f) lastCarDir = Mathf.Sign(Sim.Car.Vel);
            if (Audio != null && !Attract)
            {
                Audio.UpdateShift(Sim, dt, Paused);
                if (!Sim.Def.Endless && Sim.ClockRunning && !Sim.Ended && Sim.TimeLeft <= 10.5f)
                {
                    int sec = Mathf.CeilToInt(Sim.TimeLeft);
                    if (sec != lastTickSecond) { lastTickSecond = sec; Audio.Sfx("tick", 1f, sec <= 3 ? 1.25f : 1f); }
                }
            }

            if (Sim.Ended && Attract) { Begin(Sim.Def, (ulong)Random.Range(1, 99999)); AutoBot = Bot.Strong((ulong)Random.Range(1, 999)); return; }
            if (Sim.Ended && !endFired && endTimer < 0f) endTimer = 1.6f;
            if (endTimer > 0f)
            {
                endTimer -= dt;
                if (endTimer <= 0f) { endFired = true; Ended?.Invoke(Sim); }
            }
        }

        /// <summary>
        /// Advance the simulation manually (used by captures and tests while paused). With <paramref name="silent"/>
        /// the skipped stretch makes no sound, particles or popups (recordings jump ahead between shots).
        /// </summary>
        public void FastForward(float seconds, bool silent = false)
        {
            float t = 0f;
            quiet = silent;
            Hud.Quiet = silent;
            while (t < seconds && !Sim.Ended)
            {
                bool scripted = PreStep != null && PreStep(Sim);
                if (!scripted) AutoBot?.Tick(Sim, Step, out _);
                Sim.Tick(Step);
                Drain();
                t += Step;
            }
            quiet = false;
            Hud.Quiet = false;
        }

        /// <summary>Recordings: settle the car and its doors where the simulation has them (after a FastForward).</summary>
        public void SnapViews()
        {
            for (int i = 0; i < 40; i++) Car.Sync(Sim, 1f / 30f);
            SyncViews(1f);
        }

        // ---------------------------------------------------------------- input

        public bool RequestSend(int slot, bool fromPanel)
        {
            if (Sim == null || Sim.Ended || Paused) return false;
            if (!Sim.SendTo(slot)) return false;
            if (!Attract && AutoBot == null) Log.Action("send");
            Hud.Panel.Press(slot);
            Audio?.Sfx("btn_press", 1f, 1f + slot * 0.02f, 0.6f);
            return true;
        }

        // player commands (logged for the playtest log)
        void PlayerBoard(int pid) { if (Sim.Board(pid) == BoardResult.Ok) Log.Action("board"); }
        void PlayerBoardAll() { if (Sim.BoardAll() > 0) Log.Action("boardall"); }
        void PlayerDrop(int pid) { if (Sim.DropHere(pid)) Log.Action("drop"); }

        AudioDirector Audio => AudioDirector.Instance;
        int lastTickSecond = -1;
        static readonly int[] Scale = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16 };
        float DingPitch(int slot) => 0.84f * Mathf.Pow(2f, Scale[Mathf.Clamp(slot, 0, 9)] / 12f);
        float PanOf(Vector3 w) => AudioDirector.ScreenPan(w);
        float PanOfFloor(FloorId f) => PanOf(Building[f].transform.position);
        float PanOfPassenger(int pid) => PanOf(HeadOf(pid, Car.transform.position));
        float lastCarDir;

        void HandleInput()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    var key = kb[Key.Digit1 + i];
                    var pad = kb[Key.Numpad1 + i];
                    if ((key != null && key.wasPressedThisFrame) || (pad != null && pad.wasPressedThisFrame)) RequestSend(i, true);
                }
                if (kb.spaceKey.wasPressedThisFrame) PlayerBoardAll();
            }
            if (gp != null && gp.buttonNorth.wasPressedThisFrame) PlayerBoardAll();
            HandleZoom(kb, gp);

            if (CursorMode) { HandleCursor(kb, gp); return; }
            CursorSlot = CursorPid = -1;

            var mouse = Mouse.current;
            HoverFloor = null;
            int hoverPid = -1;
            if (mouse == null || Cam == null) { SetHover(hoverPid); return; }
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Hud.Panel.HoverSlot >= 0 && Hud.Panel.HoverSlot < Sim.B.Count) HoverFloor = Sim.B.At(Hud.Panel.HoverSlot);
            if (overUi) { SetHover(hoverPid); return; }

            var ray = Cam.ScreenPointToRay(mouse.position.ReadValue());
            var hits = Physics.RaycastAll(ray, 500f);
            PassengerView hitP = null;
            FloorView hitF = null;
            float bestP = float.MaxValue, bestF = float.MaxValue;
            foreach (var h in hits)
            {
                var pv = h.collider.GetComponentInParent<PassengerView>();
                if (pv != null && !pv.Leaving && h.distance < bestP) { bestP = h.distance; hitP = pv; continue; }
                var fv = h.collider.GetComponent<FloorView>();
                if (fv != null && fv.InBuilding && h.distance < bestF) { bestF = h.distance; hitF = fv; }
            }
            if (hitP != null) hoverPid = hitP.P.Id;
            else if (hitF != null) HoverFloor = hitF.Id;
            SetHover(hoverPid);

            // right-click a rider while docked: let them off here (deliberate, so it can't happen by accident)
            if (mouse.rightButton.wasPressedThisFrame && hitP != null && hitP.P.State == PState.Riding && Sim.Car.IsOpen)
            {
                PlayerDrop(hitP.P.Id);
                return;
            }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (hitP != null)
            {
                var p = hitP.P;
                if (p.State == PState.Waiting && Sim.Car.IsOpen && p.At == Sim.DockedFloor) PlayerBoard(p.Id);
                else if (p.State == PState.Waiting) RequestSend(Sim.B.SlotOf(p.At), false);
                else if (p.State == PState.Riding && Sim.B.Has(p.Dest)) RequestSend(Sim.B.SlotOf(p.Dest), false);
            }
            else if (hitF != null)
            {
                int slot = Sim.B.SlotOf(hitF.Id);
                if (slot >= 0) RequestSend(slot, false);
            }
        }

        /// <summary>Scroll wheel, Z (toggle) and -/= on the keyboard, triggers or the right stick on a pad.</summary>
        void HandleZoom(Keyboard kb, Gamepad gp)
        {
            float z = Rig.Zoom;
            var mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !overUi)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) z += Mathf.Sign(wheel) * Mathf.Clamp(Mathf.Abs(wheel) / 120f, 0.08f, 0.34f);
            }
            if (kb != null)
            {
                if (kb.zKey.wasPressedThisFrame) z = z < 0.5f ? 1f : 0f;
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame) z += 0.34f;
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame) z -= 0.34f;
            }
            if (gp != null)
            {
                float axis = gp.rightTrigger.ReadValue() - gp.leftTrigger.ReadValue() + gp.rightStick.ReadValue().y;
                if (Mathf.Abs(axis) > 0.15f) z += axis * Time.unscaledDeltaTime * 1.6f;
            }
            Rig.Zoom = z;
        }

        /// <summary>The close-up follows the car, but keeps the gamepad's floor cursor in view too.</summary>
        void UpdateFollow()
        {
            float y = Car.transform.position.y + Layout.CarHeight * 0.5f;
            if (CursorMode && CursorSlot >= 0 && CursorSlot < Sim.B.Count)
            {
                float cy = Building[Sim.B.At(CursorSlot)].transform.position.y + Layout.SlotHeight * 0.5f;
                float reach = CameraRig.CloseHeight * 0.5f - Layout.SlotHeight;
                y = Mathf.Clamp(y, cy - reach, cy + reach);
            }
            Rig.FollowY = y;
        }

        /// <summary>The guests the guest cursor can pick: waiting on the cursor's floor, then riders.</summary>
        readonly List<Passenger> cursorGuests = new List<Passenger>();

        void CollectCursorGuests()
        {
            cursorGuests.Clear();
            if (CursorSlot >= 0 && CursorSlot < Sim.B.Count) cursorGuests.AddRange(Sim.Waiting[(int)Sim.B.At(CursorSlot)]);
            cursorGuests.AddRange(Sim.Car.Riders);
        }

        /// <summary>
        /// Gamepad / arrow keys: up/down picks a floor, left/right (or LB/RB, Q/E) picks a guest there or in the car.
        /// A / Enter: with a guest picked, let them in (or go and get them); otherwise send the car (or let everyone in
        /// when it's already open there). X / F: let the picked rider off here. Y / Space: let everyone in. B: unpick.
        /// </summary>
        void HandleCursor(Keyboard kb, Gamepad gp)
        {
            int count = Sim.B.Count;
            if (CursorSlot < 0 || CursorSlot >= count) CursorSlot = Mathf.Clamp(Mathf.RoundToInt(Sim.Car.Pos), 0, count - 1);
            if (Controls.NavY != 0)
            {
                CursorSlot = Mathf.Clamp(CursorSlot + Controls.NavY, 0, count - 1);
                CursorPid = -1;
                Audio?.Sfx("ui_hover", 0.35f, 0.9f + CursorSlot * 0.04f, 0f, 0.02f, 0.03f);
            }
            CollectCursorGuests();
            int idx = cursorGuests.FindIndex(g => g.Id == CursorPid);
            if (idx < 0) CursorPid = -1;
            int step = Controls.NavX;
            if (gp != null) { if (gp.rightShoulder.wasPressedThisFrame) step = 1; if (gp.leftShoulder.wasPressedThisFrame) step = -1; }
            if (kb != null) { if (kb.eKey.wasPressedThisFrame) step = 1; if (kb.qKey.wasPressedThisFrame) step = -1; }
            if (step != 0 && cursorGuests.Count > 0)
            {
                idx = idx < 0 ? (step > 0 ? 0 : cursorGuests.Count - 1) : (idx + step + cursorGuests.Count) % cursorGuests.Count;
                CursorPid = cursorGuests[idx].Id;
                Audio?.Sfx("ui_hover", 0.4f, 1.15f, 0f, 0.02f, 0.03f);
            }
            if ((gp != null && gp.buttonEast.wasPressedThisFrame) || (kb != null && kb.backspaceKey.wasPressedThisFrame)) CursorPid = -1;

            var guest = CursorPid >= 0 ? Sim.Find(CursorPid) : null;
            HoverFloor = guest == null ? Sim.B.At(CursorSlot) : (FloorId?)null;
            SetHover(CursorPid);

            bool confirm = Controls.Submit;
            bool letOff = (gp != null && gp.buttonWest.wasPressedThisFrame) || (kb != null && kb.fKey.wasPressedThisFrame);
            if (letOff && guest != null && guest.State == PState.Riding && Sim.Car.IsOpen)
            {
                PlayerDrop(guest.Id);
                return;
            }
            if (!confirm) return;
            if (guest != null)
            {
                if (guest.State == PState.Waiting && Sim.Car.IsOpen && guest.At == Sim.DockedFloor) PlayerBoard(guest.Id);
                else if (guest.State == PState.Waiting) RequestSend(Sim.B.SlotOf(guest.At), false);
                else if (guest.State == PState.Riding && Sim.B.Has(guest.Dest)) RequestSend(Sim.B.SlotOf(guest.Dest), false);
                return;
            }
            var floor = Sim.B.At(CursorSlot);
            if (Sim.Car.IsOpen && floor == Sim.DockedFloor) PlayerBoardAll();
            else RequestSend(CursorSlot, false);
        }

        /// <summary>Scripted hover for recordings (pid -1 clears). Only meaningful with InputEnabled off.</summary>
        public void ShowHover(int pid) { HoverFloor = null; SetHover(pid); }

        /// <summary>Scripted floor hover for recordings: highlights the floor and previews the trip there.</summary>
        public void ShowHoverFloor(FloorId? floor) { HoverFloor = floor; SetHover(-1); }

        /// <summary>Scripted gamepad cursor for recordings (with InputEnabled off and Controls.ForcePad on).</summary>
        public void ScriptCursor(int slot, int pid)
        {
            CursorSlot = slot;
            CursorPid = pid;
            HoverFloor = pid < 0 && slot >= 0 && slot < Sim.B.Count ? Sim.B.At(slot) : (FloorId?)null;
            SetHover(pid);
        }

        /// <summary>Where a guest's body is in the world right now (null if they have no view).</summary>
        public Vector3? GuestPoint(int pid)
        {
            var v = ViewOf(pid);
            return v ? Vector3.Lerp(v.transform.position, v.BubbleAnchor, 0.45f) : (Vector3?)null;
        }

        void SetHover(int pid)
        {
            HoverPid = pid;
            FloorId? highlight = HoverFloor;
            if (pid >= 0)
            {
                var p = Sim.Find(pid);
                if (p != null) highlight = p.Dest;
            }
            foreach (var f in Building.Floors) f.SetHover(highlight.HasValue && f.Id == highlight.Value);
        }

        // ---------------------------------------------------------------- views

        void SyncViews(float dt)
        {
            for (int f = 0; f < Defs.FloorCount; f++)
            {
                var queue = Sim.Waiting[f];
                var fv = Building.Floors[f];
                for (int i = 0; i < queue.Count; i++)
                {
                    var v = ViewOf(queue[i].Id);
                    if (v) v.SetHome(fv.QueueRoot, Layout.QueueLocal(i), new Vector3(-0.55f, 0, -1f));
                }
            }
            int spot = 0;
            foreach (var r in Sim.Car.Riders)
            {
                var v = ViewOf(r.Id);
                if (v) v.SetHome(Car.Riders, Layout.RiderLocal(spot, r.Size), Vector3.back);
                spot += r.Size;
            }
        }

        PassengerView Spawn(Passenger p)
        {
            var fv = Building[p.At];
            var v = PassengerView.Create(p, fv.QueueRoot);
            int idx = Sim.Waiting[(int)p.At].IndexOf(p);
            v.transform.localPosition = Layout.QueueLocal(Mathf.Max(0, idx)) + new Vector3(1.6f, 0, 0.6f);
            v.SetHome(fv.QueueRoot, Layout.QueueLocal(Mathf.Max(0, idx)), new Vector3(-0.55f, 0, -1f));
            views[p.Id] = v;
            return v;
        }

        Vector3 HeadOf(int pid, Vector3 fallback)
        {
            var v = ViewOf(pid);
            return v ? v.BubbleAnchor : fallback;
        }

        Vector3 FloorPoint(FloorId f, float x = 0f) => Building[f].transform.position + new Vector3(x, 2f, Layout.FrontZ);

        void Drain()
        {
            foreach (var e in Sim.Events) Handle(e);
            Sim.Events.Clear();
        }

        void Later(float delay, System.Action a) => StartCoroutine(LaterRoutine(delay, a));

        System.Collections.IEnumerator LaterRoutine(float delay, System.Action a)
        {
            yield return new WaitForSeconds(delay);
            a();
        }

        Vector3 FloorFront(FloorId f) => Building[f].transform.position + new Vector3(0f, 0.05f, Layout.FrontZ + 0.3f);

        void PlayFx(SimEvent e)
        {
            switch (e.Type)
            {
                case Ev.Shuffled:
                {
                    var moved = new List<FloorId>();
                    foreach (var m in e.Moves) moved.Add(m.Floor);
                    Later(0.62f, () => { foreach (var f in moved) Fx.Play("dust", FloorFront(f), 14); });
                    break;
                }
                case Ev.FloorDeparted:
                {
                    var f = e.Floor; var g = e.Floor2;
                    Fx.Play("dust", FloorFront(f), 30);
                    Later(1.2f, () => { Fx.Play("dust", FloorFront(g), 30); if (g == FloorId.Ocean) Fx.Play("splash", FloorFront(g) + Vector3.up * 0.3f, 40); });
                    break;
                }
                case Ev.Jammed:
                    Fx.Play("sparks", Building[e.Floor].transform.position + new Vector3(-Layout.ShaftHalf, 0.6f, -1.3f), 30);
                    Fx.Play("sparks", Building[e.Floor].transform.position + new Vector3(Layout.ShaftHalf, 0.6f, -1.3f), 30);
                    break;
                case Ev.Poofed:
                {
                    var at = HeadOf(e.Pid, Car.transform.position) - Vector3.up * 0.8f;
                    Fx.Play("smoke", at, 16);
                    Fx.Play("bats", at, 12);
                    break;
                }
                case Ev.Sunned:
                    Fx.Play("sparkle", HeadOf(e.Pid, Car.transform.position) - Vector3.up * 0.6f, 18);
                    break;
                case Ev.Delivered:
                {
                    var at = HeadOf(e.Pid, Car.transform.position);
                    Fx.Play("coins", at - Vector3.up * 0.4f, Mathf.Clamp(e.Value / 120, 3, 10));
                    Hud.FlyCoins(at, Mathf.Clamp(e.Value / 90, 3, 12));
                    if (e.Aux >= 2) Fx.Play("confetti", FloorFront(e.Floor) + Vector3.up * 1.2f, 40 + e.Aux * 20);
                    break;
                }
                case Ev.StormedOff:
                    Fx.Play("smoke", HeadOf(e.Pid, FloorFront(e.Floor)) - Vector3.up * 0.4f, 6);
                    break;
                case Ev.Boarded:
                {
                    var p = Sim.Find(e.Pid);
                    if (p != null && p.Kind == Kind.Swimmer) Fx.Play("splash", HeadOf(e.Pid, Car.transform.position) - Vector3.up * 1.6f, 18);
                    break;
                }
                case Ev.RushHour:
                    Fx.Play("confetti", new Vector3(0f, Building.TopY + 1f, -2f), 160);
                    break;
                case Ev.Arrived:
                    if (e.Floor == FloorId.Ocean) Fx.Play("splash", FloorFront(e.Floor) + Vector3.up * 0.2f, 14);
                    break;
            }
        }

        void PlaySound(SimEvent e)
        {
            var a = Audio;
            if (a == null) return;
            switch (e.Type)
            {
                case Ev.Spawned:
                    var sp = Sim.Find(e.Pid);
                    if (sp != null && Time.timeSinceLevelLoad > 1f) a.Voice(sp.Kind, PanOfFloor(e.Floor), 0.25f);
                    break;
                case Ev.Boarded:
                {
                    var p = Sim.Find(e.Pid);
                    a.Sfx("board_hop", 0.7f, 1f + 0.05f * Sim.Car.Riders.Count, 0f);
                    if (p != null) a.Voice(p.Kind, 0f, 0.45f);
                    if (p != null && p.Kind == Kind.Kid) { a.SfxLater("button_mash", 0.25f, 0.6f); a.SfxLater("kid_giggle", 0.5f, 0.6f); }
                    if (p != null && p.Kind == Kind.Mirror) a.SfxLater("sparkle", 0.1f, 0.35f, 1.4f);
                    break;
                }
                case Ev.BoardRefused:
                {
                    var p = Sim.Find(e.Pid);
                    a.Sfx("refuse", 0.3f);
                    if (p != null) a.Voice(p.Kind, 0f, 0.5f);
                    break;
                }
                case Ev.Dropped: a.Sfx("board_hop", 0.6f, 0.8f); a.Bellhop(); break;
                case Ev.Departing:
                    a.Sfx("door_close", 0.55f);
                    a.SfxLater("car_start", 0.18f, 0.5f);
                    break;
                case Ev.Retarget: a.Sfx("btn_press", 0.7f, 1.1f); break;
                case Ev.QuickStop:
                    a.Sfx("ding_quick", 0.45f, DingPitch(e.Slot));
                    a.Sfx("door_open", 0.3f, 1.25f);
                    if (Random.value < 0.4f) a.Sfx("kid_giggle", 0.45f);
                    break;
                case Ev.Arrived:
                    a.Sfx("car_stop", 0.5f);
                    a.Sfx(lastCarDir >= 0 ? "ding_up" : "ding_down", 0.6f, DingPitch(e.Slot));
                    a.SfxLater("door_open", 0.06f, 0.5f);
                    break;
                case Ev.Shuffled:
                {
                    bool big = e.Card.Type == CardType.Flip || e.Card.Type == CardType.Roll || e.Moves.Length > 3;
                    a.Sfx("shuffle_lift", big ? 0.8f : 0.6f, big ? 0.85f : 1f);
                    a.SfxLater("shuffle_land", 0.55f, big ? 0.9f : 0.7f, big ? 0.85f : 1f);
                    if (big) a.Sfx("rumble", 0.6f);
                    break;
                }
                case Ev.Jammed: a.Sfx("jam", 0.7f); a.Bellhop(); break;
                case Ev.LeavingScheduled: a.Sfx("leaving_tick", 0.6f, 1f, PanOfFloor(e.Floor)); break;
                case Ev.LeavingTick: a.Sfx("leaving_tick", e.Value <= 1 ? 0.6f : 0.3f, e.Value <= 1 ? 1.2f : 1f, PanOfFloor(e.Floor)); break;
                case Ev.DepartureJammed: a.Sfx("jam", 0.4f, 1.2f); break;
                case Ev.FloorDeparted:
                    a.Sfx("floor_depart", 0.75f);
                    a.SfxLater("floor_arrive", 0.4f, 0.75f);
                    if (e.Floor2 == FloorId.Ocean) { a.SfxLater("splash", 0.9f, 0.8f); a.SfxLater("gull", 1.3f, 0.6f); }
                    break;
                case Ev.Delivered:
                {
                    var p = Sim.Find(e.Pid);
                    float pan = PanOfPassenger(e.Pid);
                    a.Sfx("coin", 0.7f, 1f + 0.12f * e.Aux, pan);
                    a.SfxLater("exit_cheer", 0.08f, 0.45f, 1f + 0.05f * e.Aux, pan);
                    a.Sfx("streak_" + Mathf.Clamp(Sim.Streak - 1, 0, 11), 0.5f, 1f, 0f, 0f, 0f);
                    if (p != null) a.SfxLater("voice_" + p.Kind.ToString().ToLowerInvariant() + "_" + Random.Range(0, 5), 0.15f, 0.45f, 1.08f, pan);
                    if (e.Aux >= 1 && e.Aux <= 3) a.SfxLater("fanfare_" + (e.Aux + 1), 0.1f, 0.6f);
                    var flags = (DeliveryFlags)int.Parse(e.Text ?? "0");
                    if ((flags & DeliveryFlags.Express) != 0 || e.Value >= 700) a.SfxLater("cash_register", 0.12f, 0.6f);
                    break;
                }
                case Ev.NeedsSun: a.Sfx("wilt", 0.6f); a.Voice(Kind.Houseplant, 0f, 0.5f); break;
                case Ev.Sunned: a.Sfx("sparkle", 0.6f); break;
                case Ev.Poofed: a.Sfx("poof", 0.85f); a.Bellhop(); break;
                case Ev.ExpressBroken: a.Sfx("harrumph", 0.7f); break;
                case Ev.Complaint: a.SfxLater("complaint", 0.05f, 0.75f); break;
                case Ev.StormedOff: a.Sfx("storm_off", 0.6f, 1f, PanOfFloor(e.Floor)); break;
                case Ev.SweptAway: a.Sfx("splash", 0.6f); break;
                case Ev.ChangedMind: { var p = Sim.Find(e.Pid); if (p != null) a.Voice(p.Kind, 0f, 0.4f); break; }
                case Ev.StreakBroken: if (e.Value >= 3) a.Sfx("record_scratch", 0.6f); break;
                case Ev.RushHour: a.Sting("sting_rush", 0.5f); a.Sfx("alarm_bell", 0.5f); break;
                case Ev.ClockStarted: a.Sfx("punch_clock", 0.5f, 1.2f); break;
                case Ev.Fired: a.Sfx("sad_trombone", 0.9f); break;
                case Ev.ShiftEnded: if (e.Aux == 0) a.Sfx("punch_clock", 0.8f); break;
            }
        }

        /// <summary>The trailer moment: the doors open onto the ocean and a soaked guest wants the Lobby.</summary>
        System.Collections.IEnumerator OceanMoment()
        {
            var ocean = Building[FloorId.Ocean];
            Rig.PushIn(ocean.transform.position + new Vector3(0f, 1.2f, 0f), 0.6f, 3.2f);
            TimeScale = 0.55f;
            Audio?.SfxLater("gull", 0.4f, 0.8f);
            Audio?.SfxLater("splash", 0.2f, 0.7f);
            yield return UiTime.Wait(1.1f);
            foreach (var p in Sim.All)
                if (p.Kind == Kind.Swimmer && p.State == PState.Waiting)
                {
                    Hud.PopupAt(HeadOf(p.Id, ocean.transform.position) + Vector3.up * 0.3f, "\"Lobby, please.\"", Palette.Cream, 40f, 2.6f);
                    Audio?.Voice(Kind.Swimmer, 0f, 0.7f);
                    break;
                }
            yield return UiTime.Wait(1.6f);
            TimeScale = 1f;
        }

        void Handle(SimEvent e)
        {
            OnEvent?.Invoke(e);
            if (!Attract) { if (!quiet) PlaySound(e); Log.SimEvent(e); }
            if (!quiet) PlayFx(e);
            switch (e.Type)
            {
                case Ev.Spawned:
                {
                    var p = Sim.Find(e.Pid);
                    if (p != null) Spawn(p);
                    break;
                }
                case Ev.BoardRefused:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.Shake();
                    Hud.Wobble(e.Pid);
                    bool full = e.Aux == (int)BoardResult.Full;
                    string msg = full ? "Full!" : Sim.Find(e.Pid)?.Kind == Kind.Vampire ? "Not with a mirror!" : "Not with a vampire!";
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), msg, Palette.Bad, 30f);
                    break;
                }
                case Ev.Departing:
                    Car.Bump(0.05f);
                    break;
                case Ev.Arrived:
                    Car.Bump(-0.09f);
                    Car.Cheer();
                    Building[e.Floor].Flash(0.8f);
                    lastGroupStop = Sim.Stops;
                    groupCount = 0;
                    break;
                case Ev.QuickStop:
                    Car.Bump(-0.05f);
                    Building[e.Floor].Flash(0.5f);
                    break;
                case Ev.Shuffled:
                {
                    for (int i = 0; i < e.Moves.Length; i++)
                    {
                        var m = e.Moves[i];
                        Building[m.Floor].ShuffleTo(m.To, 0.72f, 0.04f * i);
                    }
                    Rig.Shake(e.Card.Type == CardType.Flip ? 0.6f : 0.25f);
                    Car.Rattle(e.Card.Type == CardType.Flip ? 3f : 1.2f);
                    break;
                }
                case Ev.Jammed:
                    Hud.PopupAt(FloorPoint(e.Floor, -4f), "JAMMED!", Palette.Warn, 44f);
                    Rig.Shake(0.35f);
                    Car.Rattle(4f);
                    break;
                case Ev.LeavingScheduled:
                case Ev.LeavingTick:
                    Building[e.Floor].SetLeaving(e.Value);
                    break;
                case Ev.DepartureJammed:
                    Building[e.Floor].SetLeaving(0);
                    Hud.PopupAt(FloorPoint(e.Floor, 4f), "Holding it!", Palette.Warn, 34f);
                    break;
                case Ev.FloorDeparted:
                {
                    float dir = e.Slot % 2 == 0 ? 1f : -1f;
                    Building[e.Floor].Depart(dir);
                    Building[e.Floor2].Arrive(e.Slot, dir);
                    Building[e.Floor2].SetLeaving(Sim.B.Leaving[(int)e.Floor2]);
                    Rig.Shake(0.45f);
                    break;
                }
                case Ev.Delivered:
                {
                    var v = ViewOf(e.Pid);
                    var p = Sim.Find(e.Pid);
                    Vector3 at = HeadOf(e.Pid, Car.transform.position);
                    if (v) v.PlayExit(Building[e.Floor].Content, -1f);
                    Hud.PopupAt(at, "+$" + e.Value.ToString("N0"), Palette.Hex(0xFFD23F), 42f + Mathf.Min(20f, e.Value / 60f));
                    Hud.PunchScore();
                    var flags = (DeliveryFlags)int.Parse(e.Text ?? "0");
                    string extra = (flags & DeliveryFlags.Express) != 0 ? "EXPRESS!" :
                                   (flags & DeliveryFlags.JustInTime) != 0 ? "JUST IN TIME!" :
                                   (flags & DeliveryFlags.Rescued) != 0 ? "RESCUED!" :
                                   (flags & DeliveryFlags.Sunkissed) != 0 ? "SUNKISSED!" : null;
                    if (extra != null) Hud.PopupAt(at + Vector3.up * 1.1f, extra, Palette.Hex(0x9BE15D), 30f, 1.5f);
                    groupCount++;
                    if (groupCount == 2) Hud.PopupAt(FloorPoint(e.Floor, -3f), "DOUBLE DROP!", Palette.Hex(0xFF8A3D), 40f, 1.4f);
                    else if (groupCount == 3) Hud.PopupAt(FloorPoint(e.Floor, -3f), "TRIPLE DROP!", Palette.Hex(0xFF5DA2), 46f, 1.5f);
                    else if (groupCount == 4) Hud.PopupAt(FloorPoint(e.Floor, -3f), "FULL HOUSE!", Palette.Hex(0xC77DFF), 52f, 1.6f);
                    Car.Cheer();
                    break;
                }
                case Ev.NeedsSun:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.Shake();
                    Hud.Wobble(e.Pid);
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "I need sun first!", Palette.Warn, 28f);
                    break;
                }
                case Ev.Sunned:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.Perk();
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "Ahh, sunshine!", Palette.Hex(0xFFE066), 28f);
                    break;
                }
                case Ev.Poofed:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.PlayPoof(true);
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "POOF!", Palette.Bad, 50f);
                    Rig.Shake(0.4f);
                    break;
                }
                case Ev.ExpressBroken:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.Anger();
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "I said EXPRESS!", Palette.Bad, 28f);
                    break;
                }
                case Ev.StormedOff:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.PlayStormOff();
                    Hud.PopupAt(HeadOf(e.Pid, FloorPoint(e.Floor)), "I'll take the stairs!", Palette.Bad, 26f);
                    break;
                }
                case Ev.Fuming:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.Anger();
                    break;
                }
                case Ev.FumedOut:
                {
                    var v = ViewOf(e.Pid);
                    if (v) { v.PlayExit(Building[e.Floor].Content, 1f); v.Anger(); }
                    break;
                }
                case Ev.PackageLost:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.PlayPoof(false);
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "Package lost!", Palette.Bad, 30f);
                    break;
                }
                case Ev.SweptAway:
                case Ev.WentWithFloor:
                {
                    var v = ViewOf(e.Pid);
                    if (v) v.PlayGone();
                    break;
                }
                case Ev.ChangedMind:
                    Hud.PopupAt(HeadOf(e.Pid, Car.transform.position), "...never mind.", Palette.Cream, 24f);
                    break;
                case Ev.StreakBroken:
                    Rig.Shake(0.2f);
                    break;
                case Ev.RushHour:
                    Hud.Banner("RUSH HOUR!", Palette.Hex(0xFF8A3D));
                    break;
                case Ev.Beat:
                    if (e.Text == "saturday:ocean" && !Attract) StartCoroutine(OceanMoment());
                    break;
                case Ev.ClockStarted:
                    Hud.Banner("ON THE CLOCK!", Palette.Hex(0xFFD23F));
                    break;
                case Ev.Fired:
                    Hud.Banner("YOU'RE FIRED!", Palette.Bad);
                    break;
                case Ev.ShiftEnded:
                    if (e.Aux == 0) Hud.Banner("CLOCK OUT!", Palette.Hex(0xFFD23F));
                    break;
            }
        }
    }
}
