using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace OneMoreFloor
{
    /// <summary>
    /// A virtual keyboard for automation, like <see cref="PadSim"/>: plays a script of key presses through the Input
    /// System, so the arrow-key paths (menus, cursor play, the pause card) run exactly as a real keyboard's would.
    ///   KeySim.Play("down down enter esc", 0.3f);
    /// Tokens are Input System key names (enter, escape, space, backspace, q, e, f, z...), plus up/down/left/right
    /// for the arrow keys, "esc", and "wait" (one gap).
    /// </summary>
    public sealed class KeySim : MonoBehaviour
    {
        static KeySim instance;
        Keyboard keyboard;
        bool running;
        public static bool Busy => instance != null && instance.running;

        static KeySim Get()
        {
            if (instance == null)
            {
                var go = new GameObject("KeySim");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<KeySim>();
                instance.keyboard = InputSystem.AddDevice<Keyboard>("OMF Virtual Keyboard");
            }
            return instance;
        }

        public static void Play(string script, float gap = 0.3f)
        {
            var s = Get();
            s.StartCoroutine(s.Run(script, gap));
        }

        void OnDestroy()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        }

        IEnumerator Run(string script, float gap)
        {
            while (running) yield return null;
            running = true;
            foreach (var raw in script.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.ToLowerInvariant();
                if (token == "wait") { yield return Wait(gap); continue; }
                if (token == "esc") token = "escape";
                if (token == "up" || token == "down" || token == "left" || token == "right") token += "arrow";
                if (!System.Enum.TryParse(token, true, out Key key) || key == Key.None)
                {
                    Debug.LogWarning("[KeySim] unknown key " + token);
                    continue;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return Wait(0.08f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
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
