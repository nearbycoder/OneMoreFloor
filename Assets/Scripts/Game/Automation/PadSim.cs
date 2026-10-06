using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OneMoreFloor
{
    /// <summary>
    /// A virtual gamepad for automation: plays a script of button presses through the Input System, so the
    /// gamepad code paths (menus, cursor play, pause) are exercised exactly as a real pad would.
    ///   PadSim.Play("down down a rb a start", 0.3f);
    /// Tokens: a b x y lb rb lt rt start up down left right, "wait" (one gap), "hold:rt" (half a second).
    /// </summary>
    public sealed class PadSim : MonoBehaviour
    {
        static PadSim instance;
        Gamepad pad;
        public static bool Busy => instance != null && instance.running;
        bool running;

        public static Gamepad Device => Get().pad;

        static PadSim Get()
        {
            if (instance == null)
            {
                var go = new GameObject("PadSim");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<PadSim>();
                instance.pad = InputSystem.AddDevice<Gamepad>("OMF Virtual Pad");
            }
            return instance;
        }

        /// <summary>Pull the virtual pad out (a controller disconnect). The next Play plugs a new one in.</summary>
        public static void Unplug()
        {
            if (instance == null) return;
            DestroyImmediate(instance.gameObject);
            instance = null;
        }

        public static void Play(string script, float gap = 0.3f)
        {
            var s = Get();
            s.StartCoroutine(s.Run(script, gap));
        }

        void OnDestroy()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
        }

        IEnumerator Run(string script, float gap)
        {
            while (running) yield return null;
            running = true;
            foreach (var raw in script.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.ToLowerInvariant();
                if (token == "wait") { yield return Wait(gap); continue; }
                bool hold = token.StartsWith("hold:");
                if (hold) token = token.Substring(5);
                var state = new GamepadState();
                switch (token)
                {
                    case "a": state = state.WithButton(GamepadButton.South); break;
                    case "b": state = state.WithButton(GamepadButton.East); break;
                    case "x": state = state.WithButton(GamepadButton.West); break;
                    case "y": state = state.WithButton(GamepadButton.North); break;
                    case "lb": state = state.WithButton(GamepadButton.LeftShoulder); break;
                    case "rb": state = state.WithButton(GamepadButton.RightShoulder); break;
                    case "start": state = state.WithButton(GamepadButton.Start); break;
                    case "up": state = state.WithButton(GamepadButton.DpadUp); break;
                    case "down": state = state.WithButton(GamepadButton.DpadDown); break;
                    case "left": state = state.WithButton(GamepadButton.DpadLeft); break;
                    case "right": state = state.WithButton(GamepadButton.DpadRight); break;
                    case "rt": state.rightTrigger = 1f; break;
                    case "lt": state.leftTrigger = 1f; break;
                    default: Debug.LogWarning("[PadSim] unknown token " + token); continue;
                }
                InputSystem.QueueStateEvent(pad, state);
                yield return Wait(hold ? 0.5f : 0.08f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(gap);
            }
            running = false;
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }
    }
}
