// Browser glue for the WebGL build (Assets/Scripts/Game/Web.cs).
mergeInto(LibraryManager.library, {
  // persistentDataPath lives in an in-memory file system backed by IndexedDB: push a finished save to IndexedDB now, so
  // it survives closing or reloading the tab. One sync at a time; a save during a sync gets one more sync after it.
  OneMoreFloor_SyncFileSystem: function () {
    if (Module.omfSyncing) { Module.omfSyncAgain = true; return; }
    Module.omfSyncing = true;
    var done = function (err) {
      if (err) console.warn('[Save] IndexedDB sync failed: ' + err);
      if (Module.omfSyncAgain) { Module.omfSyncAgain = false; FS.syncfs(false, done); }
      else Module.omfSyncing = false;
    };
    FS.syncfs(false, done);
  },

  // Fullscreen on or off right away. Browsers allow it for a few seconds after a click or key press (transient user
  // activation), so a switch that acts a frame after the press still qualifies; Screen.fullScreen would wait for the
  // next input event instead.
  OneMoreFloor_RequestFullscreen: function (on) {
    if (Module.SetFullscreen) Module.SetFullscreen(on ? 1 : 0);
  },

  // 1 while the page (the game) is fullscreen: Esc or the browser's own controls can end it at any time.
  OneMoreFloor_IsFullscreen: function () {
    return document.fullscreenElement || document.webkitFullscreenElement ? 1 : 0;
  },

  // Whether this browser can make the page fullscreen (iPhones can't).
  OneMoreFloor_CanFullscreen: function () {
    return document.fullscreenEnabled || document.webkitFullscreenEnabled ? 1 : 0;
  },

  // Twice a second, what's on screen and the shift's state, as window.oneMoreFloor: the page's loading screen waits for
  // the title, and Tools/check-pages.mjs reads it ({screen, score, delivered, time, fidelity, fullscreen, largeText}).
  OneMoreFloor_Status: function (json) {
    var s;
    try { s = JSON.parse(UTF8ToString(json)); } catch (e) { console.warn('[Status] unreadable report: ' + e); return; }
    var first = !window.oneMoreFloor;
    window.oneMoreFloor = s;
    if (first && window.oneMoreFloorReady) window.oneMoreFloorReady(s.screen);
  },

  // Whenever it changes, what the on-screen controls can do now (WebBridge.cs): the page shows them during a shift
  // and greys out the ones with nothing to act on ({shift, board, letOff, picked, zoomed}).
  OneMoreFloor_TouchState: function (json) {
    var s;
    try { s = JSON.parse(UTF8ToString(json)); } catch (e) { console.warn('[Touch] unreadable state: ' + e); return; }
    if (window.oneMoreFloorTouch) window.oneMoreFloorTouch.update(s);
  },
});
