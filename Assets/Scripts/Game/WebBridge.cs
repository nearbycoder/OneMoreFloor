using System.Globalization;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// The browser page's touch layer (Assets/WebGLTemplates/OneMoreFloor/index.html) talks to the game through this
    /// object, by name: unityInstance.SendMessage("WebBridge", method, argument). The page recognises the gestures (a tap,
    /// a long press, a pinch) and owns the on-screen buttons; the game decides what they do, and tells the page what its
    /// buttons can do right now through <see cref="Web.TouchState"/>. Nothing calls it outside a browser.
    /// </summary>
    public sealed class WebBridge : MonoBehaviour
    {
        GameRoot game;
        string sent;

        public static void Create(GameRoot game)
        {
            var go = new GameObject("WebBridge");
            go.AddComponent<WebBridge>().game = game;
        }

        ShiftRunner Runner => game ? game.Runner : null;

        // ---------------------------------------------------------------- called by the page

        /// <summary>1 when the player is on the touchscreen (a touch-first device, or a real touch), 0 when a mouse moved.</summary>
        public void Touch(int on) => Controls.SetTouch(on != 0);

        /// <summary>A tap on the game at "x y", in canvas pixels from the bottom left.</summary>
        public void Tap(string xy) => At(xy, false);

        /// <summary>A long press on the game at "x y" (held still for at least 0.45 s, then lifted; the page decides).</summary>
        public void Hold(string xy) => At(xy, true);

        void At(string xy, bool hold)
        {
            var parts = xy.Split(' ');
            if (parts.Length != 2 || Runner == null) return;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                Runner.TouchAt(new Vector2(x, y), hold);
        }

        /// <summary>Two fingers on the game moved apart (factor above 1) or together since the last call.</summary>
        public void Pinch(string factor)
        {
            if (Runner != null && float.TryParse(factor, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) Runner.TouchPinch(f);
        }

        /// <summary>An on-screen button: pause, board (ALL IN), letoff (LET OFF) or zoom.</summary>
        public void Button(string name)
        {
            if (Runner == null) return;
            switch (name)
            {
                case "pause": game.Pause(); break;
                case "board": Runner.TouchBoardAll(); break;
                case "letoff": Runner.TouchLetOff(); break;
                case "zoom": Runner.TouchZoomToggle(); break;
            }
        }

        /// <summary>The page is taller than wide on a touch device (it asks the player to turn it): a running shift pauses.</summary>
        public void Portrait(int on)
        {
            if (on != 0 && game) game.AutoPauseFor("Turn your device sideways to keep playing");
        }

        // ---------------------------------------------------------------- told to the page

        void LateUpdate()
        {
            if (!Web.IsWeb || Runner == null) return;
            var r = Runner;
            bool shift = game.InShift && r.TakesInput;
            string picked = "";
            if (shift && r.TouchPid >= 0)
            {
                var p = r.Sim.Find(r.TouchPid);
                picked = p != null && p.State == Core.PState.Riding ? "rider" : "guest";
            }
            else if (shift && r.TouchFloor.HasValue) picked = "floor";
            string json = "{\"shift\":" + B(shift) + ",\"board\":" + B(shift && r.CanBoard) + ",\"letOff\":" + B(shift && r.CanLetOff)
                        + ",\"picked\":\"" + picked + "\",\"zoomed\":" + B(r.Rig != null && r.Rig.Zoom >= 0.5f) + ",\"touch\":" + B(Controls.Touch) + "}";
            if (json == sent) return;
            sent = json;
            Web.TouchState(json);
        }

        static string B(bool b) => b ? "true" : "false";
    }
}
