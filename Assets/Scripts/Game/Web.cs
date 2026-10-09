using System.Runtime.InteropServices;

namespace OneMoreFloor
{
    /// <summary>The few things the browser build does differently (Assets/Plugins/WebGL/OneMoreFloorWeb.jslib).</summary>
    public static class Web
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        public static readonly bool IsWeb = true;

        [DllImport("__Internal")] static extern void OneMoreFloor_SyncFileSystem();
        [DllImport("__Internal")] static extern void OneMoreFloor_RequestFullscreen(bool on);
        [DllImport("__Internal")] static extern int OneMoreFloor_IsFullscreen();
        [DllImport("__Internal")] static extern int OneMoreFloor_CanFullscreen();
        [DllImport("__Internal")] static extern void OneMoreFloor_Status(string json);

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
#else
        public static readonly bool IsWeb = false;

        public static void SyncFileSystem() { }

        public static void RequestFullscreen(bool on) { }

        public static bool IsFullscreen => false;

        public static bool CanFullscreen => false;

        public static void Status(string json) { }
#endif
    }
}
