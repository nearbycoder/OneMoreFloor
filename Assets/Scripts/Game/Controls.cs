using UnityEngine;
using UnityEngine.InputSystem;

namespace OneMoreFloor
{
    /// <summary>
    /// Device-agnostic input on top of the Input System: which device the player is using right now (so
    /// prompts and focus rings can follow), plus repeating directional "nav" input from the d-pad, the left
    /// stick, the arrow keys and WASD. Ticked once per frame before anything reads it.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Controls : MonoBehaviour
    {
        /// <summary>The last thing the player touched was a gamepad.</summary>
        public static bool Pad { get; private set; }
        /// <summary>Menus are being driven by d-pad/stick/arrow keys (show a focus ring); false after mouse use.</summary>
        public static bool KeyNav { get; private set; }
        public static int NavX { get; private set; }
        public static int NavY { get; private set; }
        /// <summary>Confirm in menus: A / Enter / keypad Enter.</summary>
        public static bool Submit { get; private set; }
        /// <summary>Back in menus: B (Escape is handled by the screens themselves).</summary>
        public static bool Cancel { get; private set; }
        public static bool PausePressed { get; private set; }
        /// <summary>F11 or Alt+Enter: switch FULLSCREEN (the Enter of Alt+Enter isn't a <see cref="Submit"/>).</summary>
        public static bool FullscreenPressed { get; private set; }
        /// <summary>The gamepad the player last pressed something on, and when (unscaled realtime). Unlike
        /// <see cref="Pad"/>, a mouse nudge doesn't clear it, so a disconnect can still be traced to the pad in use.</summary>
        public static InputDevice LastPadDevice { get; private set; }
        public static float LastPadUse { get; private set; } = -1f;
        /// <summary>
        /// The player is on a touchscreen (browser build): the page's on-screen controls are showing and a tap picks a
        /// guest or floor before a second tap acts (<see cref="ShiftRunner"/>). The page sets it (WebBridge) on a
        /// touch-first device or a real touch, and clears it when a mouse moves; a key or gamepad press clears it here too.
        /// </summary>
        public static bool Touch { get; private set; }

        /// <summary>The page switched touch play on or off.</summary>
        public static void SetTouch(bool on)
        {
            Touch = on;
            if (on) Pad = KeyNav = false;
        }

        /// <summary>Recordings: behave as if a gamepad is in use (prompts, cursor mode) without any real input.</summary>
        public static bool ForcePad;

        /// <summary>Recordings: end a forced-gamepad stretch and go back to pointer mode.</summary>
        public static void ReleaseForcedPad()
        {
            ForcePad = false;
            Pad = KeyNav = false;
        }
        /// <summary>Was there any directional/confirm input this frame (used to engage focus rings)?</summary>
        public static bool AnyNav => NavX != 0 || NavY != 0 || Submit || Cancel;

        static NavRepeat repeat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pad = KeyNav = Touch = false;
            LastPadDevice = null;
            LastPadUse = -1f;
            NavX = NavY = 0;
            Submit = Cancel = PausePressed = FullscreenPressed = false;
            repeat = default;
            UiScreen.All.Clear();
        }

        public static void Ensure(GameObject host)
        {
            if (!host.GetComponent<Controls>()) host.AddComponent<Controls>();
        }

        void Update()
        {
            var gp = Gamepad.current;
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            Vector2 stick = Vector2.zero;
            bool padUsed = false;
            if (gp != null)
            {
                stick = gp.leftStick.ReadValue();
                var d = gp.dpad.ReadValue();
                if (d.sqrMagnitude > 0.1f) stick = d;
                padUsed = stick.sqrMagnitude > 0.3f || gp.buttonSouth.wasPressedThisFrame || gp.buttonEast.wasPressedThisFrame ||
                          gp.buttonWest.wasPressedThisFrame || gp.buttonNorth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame ||
                          gp.leftShoulder.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame ||
                          gp.leftTrigger.ReadValue() > 0.3f || gp.rightTrigger.ReadValue() > 0.3f || gp.rightStick.ReadValue().sqrMagnitude > 0.3f;
            }
            bool keysUsed = false;
            if (kb != null)
            {
                Vector2 k = Vector2.zero;
                // WASD by position (ZQSD on an AZERTY keyboard), next to the Q / E / F that cursor play already uses
                if (kb.upArrowKey.isPressed || kb.wKey.isPressed) k.y += 1f;
                if (kb.downArrowKey.isPressed || kb.sKey.isPressed) k.y -= 1f;
                if (kb.rightArrowKey.isPressed || kb.dKey.isPressed) k.x += 1f;
                if (kb.leftArrowKey.isPressed || kb.aKey.isPressed) k.x -= 1f;
                if (k != Vector2.zero) { stick = k; keysUsed = true; }
            }
            if (padUsed) { LastPadDevice = gp; LastPadUse = Time.realtimeSinceStartup; }
            if (ForcePad) { Pad = KeyNav = true; NavX = NavY = 0; Submit = Cancel = PausePressed = FullscreenPressed = false; return; }
            if (padUsed && !Pad) { Pad = true; if (!Application.isEditor) Cursor.visible = false; }
            // a key or a gamepad takes over from the touchscreen (the page hides its controls on the same press)
            if (Touch && (padUsed || keysUsed || (kb != null && kb.anyKey.wasPressedThisFrame))) Touch = false;
            if (padUsed || keysUsed) KeyNav = true;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 9f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
            {
                if (Pad) Cursor.visible = true;
                Pad = false;
                KeyNav = false;
            }

            // directional input with key-repeat: fire on press, then again after a delay, then quickly
            var dir = new Vector2Int(Mathf.Abs(stick.x) > 0.55f ? (int)Mathf.Sign(stick.x) : 0, Mathf.Abs(stick.y) > 0.55f ? (int)Mathf.Sign(stick.y) : 0);
            if (dir.x != 0 && dir.y != 0) { if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y)) dir.y = 0; else dir.x = 0; }
            NavX = NavY = 0;
            if (repeat.Step(dir, Time.unscaledDeltaTime)) { NavX = dir.x; NavY = dir.y; }

            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
            bool enter = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
            // in a browser F11 is the browser's own fullscreen; Alt+Enter is the game's
            FullscreenPressed = kb != null && ((kb.f11Key.wasPressedThisFrame && !Web.IsWeb) || (alt && enter));
            Submit = (gp != null && gp.buttonSouth.wasPressedThisFrame) || (enter && !alt);
            Cancel = gp != null && gp.buttonEast.wasPressedThisFrame;
            PausePressed = gp != null && gp.startButton.wasPressedThisFrame;
        }
    }

    /// <summary>
    /// Key repeat for directional input: a step on press, another once it has been held for <see cref="Delay"/>, then
    /// one every <see cref="Interval"/>. Each frame counts for at most <see cref="MaxFrame"/>, so a stalled frame (a load
    /// hitch while a key is down) can't turn one press into two.
    /// </summary>
    public struct NavRepeat
    {
        public const float Delay = 0.38f, Interval = 0.12f, MaxFrame = 0.05f;
        Vector2Int held;
        float wait;

        /// <summary>Feeds this frame's direction and length; true when it should step.</summary>
        public bool Step(Vector2Int dir, float dt)
        {
            if (dir != held)
            {
                held = dir;
                wait = Delay;
                return dir != Vector2Int.zero;
            }
            if (dir == Vector2Int.zero) return false;
            wait -= Mathf.Min(dt, MaxFrame);
            if (wait > 0f) return false;
            wait = Interval;
            return true;
        }
    }
}
