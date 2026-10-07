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
        /// <summary>The gamepad the player last pressed something on, and when (unscaled realtime). Unlike
        /// <see cref="Pad"/>, a mouse nudge doesn't clear it, so a disconnect can still be traced to the pad in use.</summary>
        public static InputDevice LastPadDevice { get; private set; }
        public static float LastPadUse { get; private set; } = -1f;
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

        static Vector2Int held;
        static float repeatAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Pad = KeyNav = false;
            LastPadDevice = null;
            LastPadUse = -1f;
            NavX = NavY = 0;
            Submit = Cancel = PausePressed = false;
            held = Vector2Int.zero;
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
            if (ForcePad) { Pad = KeyNav = true; NavX = NavY = 0; Submit = Cancel = PausePressed = false; return; }
            if (padUsed && !Pad) { Pad = true; if (!Application.isEditor) Cursor.visible = false; }
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
            float now = Time.unscaledTime;
            if (dir != held) { held = dir; repeatAt = now + 0.38f; NavX = dir.x; NavY = dir.y; }
            else if (dir != Vector2Int.zero && now >= repeatAt) { repeatAt = now + 0.12f; NavX = dir.x; NavY = dir.y; }

            Submit = (gp != null && gp.buttonSouth.wasPressedThisFrame) || (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame));
            Cancel = gp != null && gp.buttonEast.wasPressedThisFrame;
            PausePressed = gp != null && gp.startButton.wasPressedThisFrame;
        }
    }
}
