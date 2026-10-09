using System.Runtime.InteropServices;

namespace OneMoreFloor
{
    /// <summary>The few things the browser build does differently (Assets/Plugins/WebGL/OneMoreFloorWeb.jslib).</summary>
    public static class Web
    {
        /// <summary>
        /// The page found a touch-first device (a phone or tablet: a coarse pointer and no fine one) and passed
        /// -omfTouch: the lighter graphics default and the on-screen controls' wording from the start.
        /// </summary>
        public static readonly bool TouchFirst = HasArg("-omfTouch");

        /// <summary>
        /// The page passed -omfLite: the last visit in this tab ended without the page closing (on a phone, most likely
        /// the browser ran out of memory and reloaded the tab), so this one starts at LOW graphics.
        /// </summary>
        public static readonly bool Lite = HasArg("-omfLite");

        static bool HasArg(string arg) => System.Array.IndexOf(System.Environment.GetCommandLineArgs(), arg) >= 0;

#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void OneMoreFloor_SyncFileSystem();
        [DllImport("__Internal")] static extern void OneMoreFloor_RequestFullscreen(bool on);
        [DllImport("__Internal")] static extern int OneMoreFloor_IsFullscreen();
        [DllImport("__Internal")] static extern int OneMoreFloor_CanFullscreen();
        [DllImport("__Internal")] static extern void OneMoreFloor_Status(string json);
        [DllImport("__Internal")] static extern void OneMoreFloor_TouchState(string json);

        /// <summary>Flushes persistentDataPath to IndexedDB so a save survives a reload.</summary>
        public static void SyncFileSystem() => OneMoreFloor_SyncFileSystem();

        /// <summary>Asks for fullscreen now, while the browser still counts the click or key that asked as recent.</summary>
        public static void RequestFullscreen(bool on) => OneMoreFloor_RequestFullscreen(on);

        /// <summary>The page is fullscreen right now (Esc or the browser can end it without the game knowing).</summary>
        public static bool IsFullscreen => OneMoreFloor_IsFullscreen() != 0;

        /// <summary>The browser can make the page fullscreen (iPhones can't).</summary>
        public static bool CanFullscreen => OneMoreFloor_CanFullscreen() != 0;

        /// <summary>Tells the page what's on screen, as JSON (the loading screen and Tools/check-pages.mjs read it).</summary>
        public static void Status(string json) => OneMoreFloor_Status(json);

        /// <summary>Tells the page's on-screen controls what they can do now, as JSON (see <see cref="WebBridge"/>).</summary>
        public static void TouchState(string json) => OneMoreFloor_TouchState(json);
#else
        public static readonly bool IsWeb = false;

        public static void SyncFileSystem() { }

        public static void RequestFullscreen(bool on) { }

        public static bool IsFullscreen => false;

        public static bool CanFullscreen => false;

        public static void Status(string json) { }

        public static void TouchState(string json) { }
#endif
    }
}
