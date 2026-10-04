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

        public ShiftSim Sim;
        public BuildingView Building;
        public CarView Car;
        public Hud Hud;
        public CameraRig Rig;
        public Camera Cam;
        public bool Paused;
        public bool InputEnabled = true;
        public float TimeScale = 1f;
        public System.Action<ShiftSim> Ended;
        public System.Action<SimEvent> OnEvent;
        /// <summary>When set, the bot plays (autopilot, attract mode, captures).</summary>
        public Bot AutoBot;

        public FloorId? HoverFloor { get; private set; }
        public int HoverPid { get; private set; } = -1;

        readonly Dictionary<int, PassengerView> views = new Dictionary<int, PassengerView>();
        float acc;
        float endTimer = -1f;
        int lastGroupStop = -1, groupCount;

        public PassengerView ViewOf(int pid) => views.TryGetValue(pid, out var v) && v ? v : null;

        public void Begin(ShiftDef def, ulong seed)
        {
            foreach (var v in views.Values) if (v) Destroy(v.gameObject);
            views.Clear();
            Sim = new ShiftSim(def, seed);
            Building.Setup(Sim.B);
            Car.Setup(Building.PulleyY - 0.82f);
            Car.Pulley = Building.Pulley;
            Hud.Clear();
            Hud.SetShiftName(def.Day, def.Title);
            Rig.Frame(Building);
            acc = 0f;
            endTimer = -1f;
            Paused = false;
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
                acc += dt * TimeScale;
                int guard = 0;
                while (acc >= Step && guard++ < 20)
                {
                    if (AutoBot != null)
                    {
                        var a = AutoBot.Tick(Sim, Step, out int arg);
                        if (a == Bot.Action.Send) Hud.Panel.Press(arg);
                    }
                    Sim.Tick(Step);
                    acc -= Step;
                    Drain();
                }
            }
            if (InputEnabled && !Paused) HandleInput();
            SyncViews(dt);
            Car.Sync(Sim, Paused ? 0f : dt);
            Hud.Tick(dt);

            if (Sim.Ended && endTimer < 0f) endTimer = 1.6f;
            if (endTimer > 0f)
            {
                endTimer -= dt;
                if (endTimer <= 0f) Ended?.Invoke(Sim);
            }
        }

        /// <summary>Advance the simulation manually (used by captures and tests while paused).</summary>
        public void FastForward(float seconds)
        {
            float t = 0f;
            while (t < seconds && !Sim.Ended)
            {
                Sim.Tick(Step);
                Drain();
                t += Step;
            }
        }

        // ---------------------------------------------------------------- input

        public bool RequestSend(int slot, bool fromPanel)
        {
            if (Sim == null || Sim.Ended || Paused) return false;
            if (!Sim.SendTo(slot)) return false;
            Hud.Panel.Press(slot);
            return true;
        }

        void HandleInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                for (int i = 0; i < 9; i++)
                {
                    var key = kb[Key.Digit1 + i];
                    var pad = kb[Key.Numpad1 + i];
                    if ((key != null && key.wasPressedThisFrame) || (pad != null && pad.wasPressedThisFrame)) RequestSend(i, true);
                }
                if (kb.spaceKey.wasPressedThisFrame) Sim.BoardAll();
            }

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

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (hitP != null)
            {
                var p = hitP.P;
                if (p.State == PState.Waiting && Sim.Car.IsOpen && p.At == Sim.DockedFloor) Sim.Board(p.Id);
                else if (p.State == PState.Riding && Sim.Car.IsOpen) Sim.DropHere(p.Id);
                else if (p.State == PState.Waiting) RequestSend(Sim.B.SlotOf(p.At), false);
                else if (p.State == PState.Riding) RequestSend(Sim.B.SlotOf(p.Dest), false);
            }
            else if (hitF != null)
            {
                int slot = Sim.B.SlotOf(hitF.Id);
                if (slot >= 0) RequestSend(slot, false);
            }
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

        void Handle(SimEvent e)
        {
            OnEvent?.Invoke(e);
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
