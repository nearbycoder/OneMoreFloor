// Checks the browser build on phones and tablets: loads it in headless WebKit with iPhone and iPad profiles and in
// Chromium with an Android phone profile, measures what a phone would have to hold (the wasm heap, the WebGL
// textures, buffers and render targets, and the browser processes' own memory), the download and the frame rate, and
// drives a short session by touch alone.
//
//   node Tools/check-mobile.mjs --serve Builds/Pages               the local build, served at /OneMoreFloor/
//   node Tools/check-mobile.mjs https://nearbycoder.github.io/OneMoreFloor/
//
// Options:
//   --device iphone|ipad|pixel|iphone-portrait|all   which profiles (default all: the three in landscape, then the
//                                    iPhone held upright)
//   --play                           also play by touch: the title's buttons, a shift through the on-screen controls,
//                                    pause and resume (the default is the title and the measurements)
//   --desktop                        instead: desktop Chromium and Firefox with a mouse, where the on-screen controls
//                                    must never appear (also after a stray touch event is followed by mouse use)
//   --out <dir>                      logs and screenshots (default Logs/mobile-check), one folder per profile
//   --timeout <s>                    how long the title may take (default 240)
//
// Headless WebKit doesn't enforce iOS's per-tab memory limit, so this measures memory itself: the largest wasm heap,
// a tally of every WebGL allocation the page makes (textures by format and size, buffers, renderbuffers, less what it
// deletes; the canvas's own buffers counted from its size), and the peak resident memory (VmHWM) of each browser
// process this script started. Taps, long presses and two-finger touches are real touch events from the browser's
// input pipeline (Playwright's touchscreen, WebKit's Input.dispatchTouchEvent through the inspector protocol, and
// Chromium's DevTools protocol), not synthetic DOM events.
//
// Needs playwright-core 1.63 (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, else ~/Sites/blog's copy). WebKit
// is Playwright's build at ~/.cache/webkit-libs/webkit-2359/pw_run.sh (WEBKIT_PATH overrides); Chromium is the newest
// in ~/.cache/ms-playwright (CHROMIUM_PATH overrides). Browsers' temporary files go under Logs/tmp, not /tmp.
import { createRequire } from "node:module";
import { createReadStream, existsSync, mkdirSync, readdirSync, readFileSync, statSync, writeFileSync } from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const args = process.argv.slice(2);
const opt = (name, fallback) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback; };
const serveDir = opt("--serve", null);
const play = args.includes("--play");
const desktop = args.includes("--desktop");
// Playwright's Linux WebKit plays Web Audio through GStreamer's autoaudiosink; without that plugin installed its web
// process aborts a few seconds after a page starts sound. --no-webaudio takes Web Audio away from WebKit pages (the game
// then runs silent), so the rest can still be checked there; Chromium keeps its sound either way.
const noAudioArg = args.includes("--no-webaudio");
const outRoot = path.resolve(opt("--out", path.join(root, "Logs", desktop ? "mobile-check-desktop" : "mobile-check")));
const titleTimeout = Number(opt("--timeout", 240)) * 1000;
const which = opt("--device", "all");
const positional = args.filter((a, i) => !a.startsWith("--") && !(i > 0 && ["--serve", "--out", "--timeout", "--device"].includes(args[i - 1])));
let url = positional[0];
if (!url && !serveDir) {
  console.error("usage: node Tools/check-mobile.mjs <url> | --serve <dir>  [--device iphone|ipad|pixel|iphone-portrait|all] [--play] [--desktop] [--out dir]");
  process.exit(2);
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
process.env.TMPDIR = path.join(root, "Logs", "tmp");
mkdirSync(process.env.TMPDIR, { recursive: true });

const require = createRequire(import.meta.url);
function findPlaywright() {
  for (const p of [process.env.PLAYWRIGHT_CORE, path.join(os.homedir(), "Sites", "blog", "node_modules", "playwright-core"), "playwright-core"]) {
    if (!p) continue;
    try { return require(p); } catch (e) { }
  }
  console.error("check-mobile: playwright-core not found; set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core");
  process.exit(2);
}
const pw = findPlaywright();
const webkitPath = process.env.WEBKIT_PATH || path.join(os.homedir(), ".cache", "webkit-libs", "webkit-2359", "pw_run.sh");
function cachedChromium() {
  if (process.env.CHROMIUM_PATH) return process.env.CHROMIUM_PATH;
  try { if (existsSync(pw.chromium.executablePath())) return undefined; } catch (e) { }
  const cache = path.join(os.homedir(), ".cache", "ms-playwright");
  const shells = (existsSync(cache) ? readdirSync(cache) : []).filter((n) => /^chromium_headless_shell-\d+$/.test(n))
    .sort((a, b) => Number(b.split("-")[1]) - Number(a.split("-")[1]));
  for (const s of shells) {
    const exe = path.join(cache, s, "chrome-headless-shell-linux64", "chrome-headless-shell");
    if (existsSync(exe)) return exe;
  }
  return undefined;
}

// the profiles: Playwright's device descriptors, held the way the game is played (landscape)
const D = pw.devices;
const profiles = {
  iphone: { engine: "webkit", device: D["iPhone 15 landscape"], name: "iPhone 15 (landscape)" },
  ipad: { engine: "webkit", device: D["iPad Pro 11 landscape"], name: "iPad Pro 11 (landscape)" },
  pixel: { engine: "chromium", device: D["Pixel 7 landscape"], name: "Pixel 7 (landscape)" },
  "iphone-portrait": { engine: "webkit", device: D["iPhone 15"], name: "iPhone 15 (portrait)", portrait: true },
  "desktop-chromium": { engine: "chromium", device: { viewport: { width: 1600, height: 900 } }, name: "desktop Chromium", desktop: true },
  "desktop-firefox": { engine: "firefox", device: { viewport: { width: 1600, height: 900 } }, name: "desktop Firefox", desktop: true },
};
const list = desktop ? ["desktop-chromium", "desktop-firefox"]
  : which === "all" ? ["iphone", "ipad", "pixel", "iphone-portrait"] : which.split(",");

// ----------------------------------------------------------------------------------------------- a static server

// Serves <dir> at /OneMoreFloor/ with no Content-Encoding headers, as GitHub Pages does for .br files.
function serve(dir) {
  const base = "/OneMoreFloor/";
  const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".json": "application/json",
                  ".wasm": "application/wasm", ".png": "image/png", ".css": "text/css" };
  const server = http.createServer((req, res) => {
    const u = decodeURIComponent(new URL(req.url, "http://x").pathname);
    if (u === "/OneMoreFloor") { res.writeHead(301, { Location: base }); return res.end(); }
    if (!u.startsWith(base) || u.includes("..")) { res.writeHead(404); return res.end("not found"); }
    let file = path.join(dir, u.slice(base.length));
    try { if (statSync(file).isDirectory()) file = path.join(file, "index.html"); } catch (e) { }
    let st;
    try { st = statSync(file); } catch (e) { res.writeHead(404); return res.end("not found"); }
    res.writeHead(200, { "Content-Type": types[path.extname(file)] || "application/octet-stream", "Content-Length": st.size,
                         "Cache-Control": "max-age=600" });
    createReadStream(file).pipe(res);
  });
  return new Promise((resolve) => server.listen(0, "127.0.0.1", () => resolve(server)));
}

// ------------------------------------------------------------------------------- the browser processes' memory

// Every process under this one (the browsers this script launched), sampled while it runs: the largest resident size
// each reached (VmHWM, the kernel's own high-water mark) and the largest sum at any one sample.
function descendants() {
  const kids = new Map();
  for (const d of readdirSync("/proc")) {
    if (!/^\d+$/.test(d)) continue;
    try {
      const st = readFileSync(`/proc/${d}/stat`, "utf8");
      const ppid = Number(st.slice(st.lastIndexOf(")") + 2).split(" ")[1]);
      if (!kids.has(ppid)) kids.set(ppid, []);
      kids.get(ppid).push(Number(d));
    } catch (e) { }
  }
  const out = [], stack = [process.pid];
  while (stack.length) for (const k of kids.get(stack.pop()) || []) { out.push(k); stack.push(k); }
  return out;
}
function procMem(pid) {
  try {
    const s = readFileSync(`/proc/${pid}/status`, "utf8");
    const kb = (k) => Number((s.match(new RegExp(`^${k}:\\s+(\\d+)`, "m")) || [0, 0])[1]);
    let name = (s.match(/^Name:\s+(.*)$/m) || [0, "?"])[1];
    try {
      const cmd = readFileSync(`/proc/${pid}/cmdline`, "utf8").split("\0");
      const type = cmd.join(" ").match(/--type=(\S+)/);
      if (type) name += ` (${type[1]})`;
    } catch (e) { }
    return { name, rss: kb("VmRSS") * 1024, hwm: kb("VmHWM") * 1024 };
  } catch (e) { return null; }
}
class MemWatch {
  constructor() { this.procs = new Map(); this.peakSum = 0; this.timer = setInterval(() => this.sample(), 250); }
  sample() {
    let sum = 0;
    for (const pid of descendants()) {
      const m = procMem(pid);
      if (!m || m.rss === 0) continue;
      sum += m.rss;
      const p = this.procs.get(pid) || { name: m.name, hwm: 0 };
      p.hwm = Math.max(p.hwm, m.hwm, m.rss);
      if (m.name.includes("(")) p.name = m.name;
      p.rss = m.rss;
      this.procs.set(pid, p);
    }
    this.peakSum = Math.max(this.peakSum, sum);
    return sum;
  }
  stop() { clearInterval(this.timer); this.sample(); }
  report() {
    const byName = [...this.procs.entries()].filter(([, p]) => p.hwm > 20 * 1048576).sort((a, b) => b[1].hwm - a[1].hwm)
      .map(([pid, p]) => ({ process: p.name, pid, peakMB: mb(p.hwm) }));
    return { peakSumMB: mb(this.peakSum), processes: byName };
  }
}
const mb = (b) => Number((b / 1048576).toFixed(1));

// ------------------------------------------------------------------------------------------------------ in the page

// Tallies WebGL memory (textures, buffers, renderbuffers, less what's deleted), counts frames, and reports through the
// console twice a second (page.evaluate would count as a user gesture and could unlock sound early).
const pageProbe = () => {
  const bpp = { 0x8058: 4, 0x8C43: 4, 0x881A: 8, 0x8814: 16, 0x8C3A: 4, 0x88F0: 4, 0x81A6: 4, 0x8CAC: 4, 0x8CAD: 8,
                0x8229: 1, 0x822B: 2, 0x822D: 2, 0x822F: 4, 0x822E: 4, 0x8230: 8, 0x8059: 4, 0x1908: 4, 0x1907: 3,
                0x8051: 3, 0x8D62: 2, 0x81A5: 2, 0x8D48: 1, 0x1906: 1, 0x1909: 1, 0x190A: 2, 0x8C41: 3, 0x881B: 6 };
  const tex = new Map(), buf = new Map(), rb = new Map(), texInfo = new Map(), rbInfo = new Map();
  let peak = 0, heapPeak = 0, frames = 0, gl2 = null;
  const total = () => { let t = 0; for (const m of tex.values()) for (const v of m.values()) t += v; for (const v of buf.values()) t += v; for (const v of rb.values()) t += v; return t; };
  const sizes = (map) => { let t = 0; for (const v of map.values()) t += v instanceof Map ? [...v.values()].reduce((a, b) => a + b, 0) : v; return t; };
  const P = typeof WebGL2RenderingContext !== "undefined" ? WebGL2RenderingContext.prototype : null;
  if (P) {
    const bound = new WeakMap();   // context -> {unit, units: [{target: tex}], buffers: {target: buf}, rb}
    const st = (gl) => { let s = bound.get(gl); if (!s) { s = { unit: 0, units: [], buffers: {}, rb: null }; bound.set(gl, s); gl2 = gl; } return s; };
    const texOf = (gl, target) => {
      const s = st(gl), u = s.units[s.unit] || {};
      const t = target >= 0x8515 && target <= 0x851A ? 0x8513 : target;   // a cube face belongs to the cube map
      return u[t];
    };
    const setLevel = (gl, target, level, bytes) => {
      const t = texOf(gl, target);
      if (!t) return;
      if (!tex.has(t)) tex.set(t, new Map());
      tex.get(t).set(target + ":" + level, bytes);
      peak = Math.max(peak, total());
    };
    const wrap = (name, fn) => { const orig = P[name]; P[name] = function (...a) { try { fn(this, a); } catch (e) { } return orig.apply(this, a); }; };
    wrap("activeTexture", (gl, a) => { st(gl).unit = a[0] - 0x84C0; });
    wrap("bindTexture", (gl, a) => { const s = st(gl); (s.units[s.unit] = s.units[s.unit] || {})[a[0]] = a[1]; });
    wrap("deleteTexture", (gl, a) => { tex.delete(a[0]); });
    wrap("texStorage2D", (gl, a) => {
      const [target, levels, fmt, w, h] = a, faces = target === 0x8513 ? 6 : 1;
      let bytes = 0;
      for (let l = 0; l < levels; l++) bytes += Math.max(1, w >> l) * Math.max(1, h >> l) * (bpp[fmt] || 4) * faces;
      setLevel(gl, target, "storage", bytes);
      const t = texOf(gl, target);
      if (t) texInfo.set(t, `${w}x${h}${faces > 1 ? "x6" : ""} 0x${fmt.toString(16)} ${levels} mips`);
    });
    wrap("texStorage3D", (gl, a) => {
      const [target, levels, fmt, w, h, d] = a;
      let bytes = 0;
      for (let l = 0; l < levels; l++) bytes += Math.max(1, w >> l) * Math.max(1, h >> l) * (target === 0x8C1A ? d : Math.max(1, d >> l)) * (bpp[fmt] || 4);
      setLevel(gl, target, "storage", bytes);
    });
    wrap("texImage2D", (gl, a) => {
      let w, h, fmt = a[2];
      if (a.length >= 8) { w = a[3]; h = a[4]; } else { const src = a[5]; w = src.videoWidth || src.naturalWidth || src.width; h = src.videoHeight || src.naturalHeight || src.height; }
      setLevel(gl, a[0], a[1], w * h * (bpp[fmt] || 4));
      const t = texOf(gl, a[0]);
      if (t && a[1] === 0) texInfo.set(t, `${w}x${h} 0x${fmt.toString(16)} (texImage2D)`);
    });
    wrap("texImage3D", (gl, a) => { setLevel(gl, a[0], a[1], a[3] * a[4] * a[5] * (bpp[a[2]] || 4)); });
    wrap("compressedTexImage2D", (gl, a) => {
      const data = a[6];
      const bytes = typeof data === "number" ? data : data && data.byteLength !== undefined ? (a.length > 8 && a[8] ? a[8] : data.byteLength - (a[7] || 0)) : 0;
      setLevel(gl, a[0], a[1], bytes);
    });
    wrap("compressedTexImage3D", (gl, a) => { const d = a[7]; setLevel(gl, a[0], a[1], typeof d === "number" ? d : d ? d.byteLength : 0); });
    wrap("bindBuffer", (gl, a) => { st(gl).buffers[a[0]] = a[1]; });
    wrap("deleteBuffer", (gl, a) => { buf.delete(a[0]); });
    wrap("bufferData", (gl, a) => {
      const b = st(gl).buffers[a[0]];
      if (!b) return;
      const d = a[1];
      buf.set(b, typeof d === "number" ? d : d ? (a.length > 4 && a[4] ? a[4] * (d.BYTES_PER_ELEMENT || 1) : d.byteLength) : 0);
      peak = Math.max(peak, total());
    });
    wrap("bindRenderbuffer", (gl, a) => { st(gl).rb = a[1]; });
    wrap("deleteRenderbuffer", (gl, a) => { rb.delete(a[0]); });
    wrap("renderbufferStorage", (gl, a) => { const r = st(gl).rb; if (r) { rb.set(r, a[2] * a[3] * (bpp[a[1]] || 4)); peak = Math.max(peak, total()); } });
    wrap("renderbufferStorageMultisample", (gl, a) => { const r = st(gl).rb; if (r) { rb.set(r, Math.max(1, a[1]) * a[3] * a[4] * (bpp[a[2]] || 4)); rbInfo.set(r, `${a[3]}x${a[4]} 0x${a[2].toString(16)} x${a[1]}`); peak = Math.max(peak, total()); } });
  }
  // the largest textures and renderbuffers now, for the summary
  window.__glDetail = () => {
    const sz = (m) => [...m.values()].reduce((a, b) => a + b, 0);
    const t = [...tex.entries()].map(([k, m]) => [sz(m), texInfo.get(k) || "?"]).sort((a, b) => b[0] - a[0]).slice(0, 14)
      .map(([b, d]) => `${(b / 1048576).toFixed(1)} MB ${d}`);
    const r = [...rb.entries()].map(([k, b]) => [b, rbInfo.get(k) || "single-sample"]).sort((a, b) => b[0] - a[0]).slice(0, 8)
      .map(([b, d]) => `${(b / 1048576).toFixed(1)} MB ${d}`);
    return { textures: t, renderbuffers: r };
  };
  const heap = () => {
    const M = window.unityInstance && window.unityInstance.Module;
    if (!M) return 0;
    const a = M.HEAPU8 || M.HEAPU32 || M.HEAPF64;
    const b = a && a.buffer;
    return b ? b.byteLength : 0;
  };
  const loop = () => { frames++; requestAnimationFrame(loop); };
  requestAnimationFrame(loop);
  let lastFrames = 0, lastT = performance.now();
  setInterval(() => { try { report(); } catch (e) { console.log("[probe-error] " + e); } }, 500);
  const report = () => {
    const h = heap();
    heapPeak = Math.max(heapPeak, h);
    const now = performance.now(), fps = (frames - lastFrames) * 1000 / (now - lastT);
    lastFrames = frames; lastT = now;
    const c = document.querySelector("#unity-canvas");
    const canvasBytes = c ? c.width * c.height * 4 * 3 : 0;   // front + back colour and a depth/stencil buffer
    let ctx = null;
    try { const C = window.AudioContext || window.webkitAudioContext; ctx = window.__ctxStates ? window.__ctxStates() : null; } catch (e) { }
    const touch = window.oneMoreFloorTouch ? window.oneMoreFloorTouch.state() : null;
    console.log("[probe] " + JSON.stringify({
      status: window.oneMoreFloor || null, error: window.oneMoreFloorError || null, heap: h, heapPeak, fps: Number(fps.toFixed(1)),
      gl: { textures: sizes(tex), textureCount: tex.size, buffers: sizes(buf), renderbuffers: sizes(rb), now: total(), peak, canvas: canvasBytes,
            canvasSize: c ? [c.width, c.height] : null },
      jsHeap: performance.memory ? performance.memory.usedJSHeapSize : null, audio: ctx, touch,
      viewport: [innerWidth, innerHeight], dpr: devicePixelRatio,
    }));
  };
  // every AudioContext's state, so the check can tell when the first touch unlocked sound
  const Ctx = window.AudioContext || window.webkitAudioContext;
  if (Ctx) {
    const made = [];
    const Wrapped = function (...a) {
      const c = new Ctx(...a);
      made.push(c);
      console.log(`[audio] context made at ${performance.now().toFixed(0)} ms, ${c.state}`);
      c.addEventListener("statechange", () => console.log(`[audio] context ${c.state} at ${performance.now().toFixed(0)} ms`));
      return c;
    };
    Wrapped.prototype = Ctx.prototype;
    window.AudioContext = Wrapped;
    if (window.webkitAudioContext) window.webkitAudioContext = Wrapped;
    window.__ctxStates = () => made.map((c) => c.state);
  }
};

// ---------------------------------------------------------------------------------------------------- touch input

// Real touch events: taps through Playwright's touchscreen; held and two-finger touches through each engine's own
// protocol (Chromium's Input.dispatchTouchEvent over CDP; WebKit's Input.dispatchTouchEvent over its inspector
// protocol, reached through Playwright's in-process session).
async function makeTouch(engine, page, context, fingerMs) {
  let send = null;
  if (engine === "chromium") {
    const cdp = await context.newCDPSession(page);
    send = (type, points) => cdp.send("Input.dispatchTouchEvent", { type, touchPoints: points.map((p, i) => ({ x: p[0], y: p[1], id: p[2] ?? i, radiusX: 8, radiusY: 8, force: 1 })) });
  } else if (engine === "webkit") {
    const session = webkitSession(page);
    if (session) send = (type, points) => session.send("Input.dispatchTouchEvent", { type, touchPoints: points.map((p, i) => ({ x: Math.round(p[0]), y: Math.round(p[1]), id: p[2] ?? i })) });
  }
  const T = { start: "touchStart", move: "touchMove", end: "touchEnd" };
  // Lifting fingers: Chromium's protocol lists the points still down after the event, WebKit's the points lifted.
  const lift = (lifted, remaining) => send(T.end, engine === "webkit" ? lifted : remaining);
  return {
    raw: !!send,
    // A finger down for a moment, as a real tap is. Headless WebKit draws on the CPU at a few frames a second and stamps
    // touch events when its busy main thread gets to them, so a timed tap there can measure as a long press: it gets
    // Playwright's own tap instead (down and up in one go).
    async tap(x, y, ms = fingerMs) {
      if (!send || !ms) return page.touchscreen.tap(x, y);
      await send(T.start, [[x, y, 1]]);
      await sleep(ms);
      await lift([[x, y, 1]], []);
    },
    // a tap on the game's own UI (menus, the panel), which the engine handles: a finger held long enough to span its
    // frames even in headless WebKit (it ignores how long, only the page tells a tap from a long press)
    tapUi(x, y) { return this.tap(x, y, Math.max(fingerMs, engine === "webkit" ? 160 : 90)); },
    /** a finger held down for ms, then lifted */
    async hold(x, y, ms) {
      if (!send) throw new Error("no touch protocol for " + engine);
      await send(T.start, [[x, y, 1]]);
      await sleep(ms);
      await lift([[x, y, 1]], []);
    },
    /** two fingers about c, spreading from d0 to d1 pixels apart */
    async pinch(c, d0, d1, steps = 8) {
      if (!send) throw new Error("no touch protocol for " + engine);
      const pts = (d) => [[c[0] - d / 2, c[1], 1], [c[0] + d / 2, c[1], 2]];
      await send(T.start, pts(d0));
      for (let i = 1; i <= steps; i++) { await sleep(30 * (engine === "webkit" ? 4 : 1)); await send(T.move, pts(d0 + (d1 - d0) * i / steps)); }
      await sleep(60);
      await lift(pts(d1), []);
    },
    /** one finger held on a, while another taps b */
    async chord(a, b, ms = 500) {
      if (!send) throw new Error("no touch protocol for " + engine);
      const A = [a[0], a[1], 1], B = [b[0], b[1], 2];
      await send(T.start, [A]);
      await sleep(120);
      await send(T.start, [A, B]);
      await sleep(120);
      await lift([B], [A]);
      await sleep(ms);
      await lift([A], []);
    },
  };
}

// Playwright's WebKit page session (the in-process server object behind the client page), for protocol messages
// Playwright has no client API for.
function webkitSession(page) {
  try {
    const impl = page._connection.toImpl(page);          // the server-side Page (in-process Playwright only)
    const session = impl && impl.delegate && impl.delegate._pageProxySession || impl && impl._delegate && impl._delegate._pageProxySession;
    return session ? { send: (m, p) => session.send(m, p) } : null;
  } catch (e) { return null; }
}

// ---------------------------------------------------------------------------------------------------------- a profile

async function check(key, url) {
  const prof = profiles[key];
  const engine = prof.engine;
  const out = path.join(outRoot, key);
  mkdirSync(out, { recursive: true });
  const results = [];
  const ok = (pass, what) => { results.push({ pass, what }); console.log(`[${key}] ${pass ? "PASS" : "FAIL"} ${what}`); return pass; };
  const info = (what) => { console.log(`[${key}] INFO ${what}`); results.push({ info: what }); };
  const log = [];
  const tStart = Date.now();
  log.push = ((push) => (line) => push.call(log, `${((Date.now() - tStart) / 1000).toFixed(1).padStart(6)} ${line}`))(log.push);
  const summary = { profile: prof.name, engine, url, play };
  const watch = new MemWatch();
  const noAudio = noAudioArg && engine === "webkit";
  if (noAudio) summary.webAudio = "removed (--no-webaudio)";
  let browser;
  try {
    const launch = { headless: true };
    if (engine === "webkit") launch.executablePath = webkitPath;
    if (engine === "chromium") {
      launch.executablePath = cachedChromium();
      launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist", "--autoplay-policy=document-user-activation-required"];
      launch.ignoreDefaultArgs = ["--autoplay-policy=no-user-gesture-required"];
    }
    if (engine === "firefox") { launch.channel = "moz-firefox"; launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox"; }
    browser = await pw[engine].launch(launch);
    summary.version = browser.version();
    const ctxOpts = { ...prof.device };
    delete ctxOpts.defaultBrowserType;
    if (engine === "firefox") delete ctxOpts.isMobile;
    const context = await browser.newContext(ctxOpts);
    await context.addInitScript(pageProbe);
    if (noAudio) await context.addInitScript(() => { delete window.AudioContext; delete window.webkitAudioContext; window.AudioContext = undefined; window.webkitAudioContext = undefined; });
    const page = await context.newPage();
    const errors = [];
    let latest = null, probes = 0;
    page.on("console", (m) => {
      const text = m.text();
      if (text.startsWith("[probe] ")) {
        try { latest = JSON.parse(text.slice(8)); latest.__at = Date.now(); } catch (e) { }
        if (++probes % 10 === 1) log.push(`[probe ${(probes / 2).toFixed(0)} s] ` + text.slice(8, 600));
        return;
      }
      log.push(`[${m.type()}] ${text}`);
      if (m.type() === "error" && !(noAudio && /Loading FSB failed|audio/i.test(text))) errors.push("console: " + text);
    });
    page.on("pageerror", (e) => { log.push("[pageerror] " + e); errors.push("page: " + e); });
    page.on("crash", () => { log.push("[crash] the page crashed"); errors.push("the page crashed"); });
    let bytes = 0;
    page.on("requestfailed", (r) => { log.push(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`); if (/^https?:/.test(r.url())) errors.push("request failed: " + r.url()); });
    page.on("response", async (r) => {
      if (r.status() >= 400) errors.push(`HTTP ${r.status()}: ${r.url()}`);
      const len = Number(r.headers()["content-length"] || 0);
      if (len) bytes += len; else { try { bytes += (await r.request().sizes()).responseBodySize; } catch (e) { } }
    });
    const shot = (name) => page.screenshot({ path: path.join(out, name + ".png") }).catch(() => {});
    const status = () => latest && latest.status;
    const waitFor = async (pred, ms) => { for (const end = Date.now() + ms; Date.now() < end; await sleep(200)) { if (latest && pred(latest)) return latest; if (latest && latest.error) return null; } return null; };
    const waitScreen = (screen, ms) => waitFor((l) => l.status && l.status.screen === screen, ms);
    // headless WebKit renders on the CPU at a few frames a second: longer taps and longer waits there
    const slow = engine === "webkit" ? 3 : 1;
    const touch = await makeTouch(engine, page, context, engine === "webkit" ? 0 : 90);
    const vw = prof.device.viewport.width, vh = prof.device.viewport.height;

    // ---- to the title ---------------------------------------------------------------------------------------
    const t0 = Date.now();
    // -omfTouchTest: the game logs what each tap did (into console.log here) and reports where guests and floors are
    await page.goto(url + (url.includes("?") ? "&" : "?") + "arg=-omfTouchTest", { waitUntil: "load", timeout: 120000 });
    if (prof.portrait) {
      // held upright: the page asks the player to turn the phone; the game itself still loads behind the card
      await sleep(4000);
      await shot("01_portrait_loading");
      const rotate = await page.evaluate(() => { const r = document.querySelector("#rotate"); return r ? getComputedStyle(r).display !== "none" : null; }).catch(() => null);
      const atTitle = await waitScreen("title", titleTimeout);
      await sleep(2000);
      await shot("02_portrait_title");
      const rotateAtTitle = await page.evaluate(() => { const r = document.querySelector("#rotate"); return r ? getComputedStyle(r).display !== "none" : null; }).catch(() => null);
      summary.loadSeconds = Number(((Date.now() - t0) / 1000).toFixed(1));
      ok(!!atTitle, `reached the title held upright (${summary.loadSeconds} s)`);
      ok(rotate === true && rotateAtTitle === true, `asks the player to turn the phone sideways (card shown while loading: ${rotate}, at the title: ${rotateAtTitle})`);
      await page.setViewportSize({ width: prof.device.viewport.height, height: prof.device.viewport.width });
      await sleep(1500);
      const rotateGone = await page.evaluate(() => { const r = document.querySelector("#rotate"); return r ? getComputedStyle(r).display === "none" : null; }).catch(() => null);
      await shot("03_turned_sideways");
      ok(rotateGone === true, "the card goes once the phone is turned sideways");
      await context.close();
      return finish();
    }
    const atTitle = await waitScreen("title", titleTimeout);
    summary.loadSeconds = Number(((Date.now() - t0) / 1000).toFixed(1));
    summary.downloadMB = mb(bytes);
    const audioBefore = latest?.audio;   // before anything that counts as a user gesture (page.evaluate does)
    await sleep(5000);   // the title's first seconds, and a frame-rate sample
    const atRest = latest;
    const audioAtRest = latest?.audio;
    await shot("01_title");   // (in Chromium a screenshot seems to count as a user gesture too: after the sound check)
    console.log(`[${key}] ${atTitle ? "title" : "no title"} after ${summary.loadSeconds} s, ${summary.downloadMB} MB downloaded; canvas ${atRest?.gl.canvasSize} at dpr ${atRest?.dpr}, ${atRest?.fps} fps at the title`);
    ok(!!atTitle && !(latest && latest.error), `reached the title screen${latest && latest.error ? " (page says: " + latest.error + ")" : ""}`);
    summary.title = atRest && { heapMB: mb(atRest.heap), glMB: mb(atRest.gl.now), texturesMB: mb(atRest.gl.textures), buffersMB: mb(atRest.gl.buffers),
                                renderbuffersMB: mb(atRest.gl.renderbuffers), canvasMB: mb(atRest.gl.canvas), canvas: atRest.gl.canvasSize, fps: atRest.fps,
                                jsHeapMB: atRest.jsHeap ? mb(atRest.jsHeap) : null };
    const controlsShown = async () => page.evaluate(() => { const c = document.querySelector("#touch"); return !!c && getComputedStyle(c).display !== "none" && document.body.classList.contains("touching"); }).catch(() => null);

    const engineInfo = async () => {
      summary.renderer = await page.evaluate(() => {
        const gl = document.createElement("canvas").getContext("webgl2");
        if (!gl) return "no WebGL2";
        const ext = gl.getExtension("WEBGL_debug_renderer_info");
        return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
      }).catch(() => "unknown");
      summary.media = await page.evaluate(() => ({ coarse: matchMedia("(pointer: coarse)").matches, fine: matchMedia("(any-pointer: fine)").matches,
                                                   hover: matchMedia("(hover: hover)").matches, webgpu: !!navigator.gpu, maxTouchPoints: navigator.maxTouchPoints })).catch(() => null);
      console.log(`[${key}] ${engine} ${summary.version}, renderer ${summary.renderer}, media ${JSON.stringify(summary.media)}`);
    };
    if (desktop) {
      await engineInfo();
      // ---- desktop: never the controls, whatever the mouse does; a stray touch shows them, the mouse hides them ----
      await page.mouse.move(400, 300); await page.mouse.move(600, 400);
      await sleep(600);
      ok((await controlsShown()) === false, "no on-screen controls on the desktop at the title");
      if (play) {
        await page.mouse.click(620, 60);
        await sleep(500);
        await page.keyboard.press("Enter");
        await sleep(800);
        ok((await controlsShown()) === false, "still none after a click and a key press");
      }
      await shot("02_desktop");
      await context.close();
      return finish();
    }

    // ---- by touch only ---------------------------------------------------------------------------------------
    // sound: still waiting before the first touch, running after it
    await touch.tap(vw * 0.36, vh * 0.06);   // an empty corner of the title
    await sleep(1500);
    const audioAfter = latest?.audio;
    await engineInfo();
    const shown1 = await controlsShown();
    info(`audio contexts before the first touch ${JSON.stringify(audioBefore)}, after it ${JSON.stringify(audioAfter)}`);
    if (noAudio) info("Web Audio removed (--no-webaudio): the first touch's sound is checked in Chromium");
    else ok(!!audioAfter && audioAfter.length > 0 && audioAfter.every((s) => s === "running") && ![...(audioBefore || []), ...(audioAtRest || [])].some((s) => s === "running"),
            `sound waits for the first touch and runs after it (contexts at the title ${JSON.stringify(audioBefore)}, 5 s later ${JSON.stringify(audioAtRest)}, after the touch ${JSON.stringify(audioAfter)})`);
    await shot("02_title_touched");
    const touching1 = await page.evaluate(() => document.body.classList.contains("touching")).catch(() => null);
    ok(touching1 === true && shown1 === false, `touch play is on (the controls wait for a shift: shown at the title ${shown1})`);
    const menu = latest?.touch;
    info(`the page's touch layer: ${JSON.stringify(menu)}`);

    if (play) await playByTouch({ page, touch, key, latest: () => latest, waitScreen, waitFor, shot, ok, info, vw, vh, status, summary, slow });

    // ---- memory after play ---------------------------------------------------------------------------------
    await sleep(1000);
    summary.peak = latest && { heapPeakMB: mb(latest.heapPeak), glPeakMB: mb(latest.gl.peak), glNowMB: mb(latest.gl.now), texturesMB: mb(latest.gl.textures),
                               textureCount: latest.gl.textureCount, renderbuffersMB: mb(latest.gl.renderbuffers), canvasMB: mb(latest.gl.canvas) };
    summary.glDetail = await page.evaluate(() => window.__glDetail && window.__glDetail()).catch(() => null);
    ok(errors.length === 0, `no console errors, page errors, crashes or failed requests${errors.length ? ": " + errors.slice(0, 5).join(" | ") : ""}`);
    await context.close();
  } catch (e) {
    ok(false, "the check ran: " + (e && e.message ? e.message.split("\n")[0] : e));
  }
  return finish();

  async function finish() {
    if (browser) await browser.close().catch(() => {});
    watch.stop();
    summary.processes = watch.report();
    summary.results = results;
    summary.passed = results.some((r) => r.pass !== undefined) && results.every((r) => r.pass !== false);
    console.log(`[${key}] memory: ${JSON.stringify({ title: summary.title, peak: summary.peak })}`);
    console.log(`[${key}] browser processes: peak sum ${summary.processes.peakSumMB} MB; ${summary.processes.processes.map((p) => `${p.process} ${p.peakMB} MB`).join(", ")}`);
    writeFileSync(path.join(out, "console.log"), (log.length > 20000 ? [...log.slice(0, 10000), "[...]", ...log.slice(-10000)] : log).join("\n") + "\n");
    writeFileSync(path.join(out, "summary.json"), JSON.stringify(summary, null, 2) + "\n");
    console.log(`[${key}] ${summary.passed ? "PASSED" : "FAILED"}; console log, summary and screenshots in ${out}`);
    return summary.passed;
  }
}

// A short session by touch alone: the title's menus, a shift through taps on guests and floors, the panel and the
// on-screen buttons, a long press, a pinch, two fingers at once, pause and resume, and quitting back to the title.
async function playByTouch({ page, touch, key, latest, waitScreen, waitFor, shot, ok, info, summary, slow }) {
  const waitScreen0 = waitScreen, waitFor0 = waitFor;
  waitScreen = (name, ms) => waitScreen0(name, ms * slow);
  waitFor = (pred, ms) => waitFor0(pred, ms * slow);
  // a fresh report from the game (it reports twice a second of its own time)
  const fresh = async () => { const t = Date.now(); await waitFor0((l) => l.__at > t + 50, 4000 * slow); };
  const st = () => latest() && latest().status;
  // the canvas on the page, and the UI's scale: the canvas is laid out for 1920x1080 (TextFloor.Reference, match 0.6)
  const geo = await page.evaluate(() => { const c = document.querySelector("#unity-canvas"), r = c.getBoundingClientRect();
                                           return { left: r.left, top: r.top, width: r.width, height: r.height, w: c.width, h: c.height }; });
  const scale = Math.pow(2, 0.4 * Math.log2(geo.w / 1920) + 0.6 * Math.log2(geo.h / 1080));   // reference units -> canvas pixels
  const k = scale * geo.width / geo.w;                                                          // reference units -> CSS pixels
  info(`canvas ${geo.w}x${geo.h} pixels in ${geo.width.toFixed(0)}x${geo.height.toFixed(0)} CSS px; the UI at ${k.toFixed(3)} CSS px per reference unit`);
  // a point in the UI: an anchor (0..1 of the canvas, y up) and an offset in reference units (y up)
  const ui = (ax, ay, x, y) => Object.assign([geo.left + ax * geo.width + x * k, geo.top + (1 - ay) * geo.height - y * k], { ui: true });
  // a point the game reported, in canvas pixels from the bottom left
  const px = (x, y) => [geo.left + x * geo.width / geo.w, geo.top + geo.height - y * geo.height / geo.h];
  const brief = () => { const x = st(); return x ? JSON.stringify({ screen: x.screen, car: x.car, open: x.open, riders: x.riders, here: x.here, picked: x.picked,
                                                                    pickedFloor: x.pickedFloor, guests: x.guests }) : "null"; };
  const tap = async (p, what, settle = 900) => {
    if (p.ui) await touch.tapUi(p[0], p[1]); else await touch.tap(p[0], p[1]);
    await sleep(settle * slow);
    if (process.env.DEBUG_TOUCH) console.log(`[${key}] tap ${what} at ${p[0].toFixed(0)},${p[1].toFixed(0)} -> ${brief()}`);
    return what;
  };
  const screenIs = async (name, what, ms = 6000) => ok(!!(await waitScreen(name, ms)), `${what} (screen ${st()?.screen})`);
  const visible = (sel) => page.evaluate((sel) => { const e = document.querySelector(sel); return !!e && getComputedStyle(e).display !== "none" && e.getBoundingClientRect().width > 0; }, sel);
  const rect = (sel) => page.evaluate((sel) => { const r = document.querySelector(sel).getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2, w: r.width, h: r.height }; }, sel);
  // a guest's latest place, after they've had a moment to settle (walking into the car, or along the queue)
  const settled = async (id) => { await sleep(600 * slow); await fresh(); return ((st() && st().guests) || []).find((g) => g[0] === id); };
  const press = async (sel, settle = 700) => { const r = await rect(sel); await touch.tap(r.x, r.y); await sleep(settle * slow); await fresh(); };

  // ---- the menus by tap ---------------------------------------------------------------------------------------------
  await tap(ui(0, 0.5, 360, -345), "SETTINGS");
  await screenIs("settings", "a tap on SETTINGS opens Settings");
  await shot("03_settings");
  await tap(ui(0.5, 0.5, 0, -394), "DONE");
  await screenIs("title", "a tap on DONE goes back to the title");
  await tap(ui(0, 0.5, 360, -260), "DUTY ROSTER");
  await screenIs("roster", "a tap on DUTY ROSTER opens the roster");
  await tap(ui(0.5, 0.5, 160, -474), "GUEST GUIDE");
  await screenIs("guide", "a tap on GUEST GUIDE opens the guide");
  await shot("04_guide");
  await tap(ui(0.5, 0.5, 0, -366), "BACK");
  await screenIs("roster", "a tap on the guide's BACK returns to the roster");
  await tap(ui(0.5, 0.5, -160, -474), "BACK");
  await screenIs("title", "a tap on the roster's BACK returns to the title");
  ok(!(await visible("#touch")), "no on-screen controls on the menus");
  await tap(ui(0, 0.5, 360, -120), "START SHIFT");
  await screenIs("intro", "a tap on START SHIFT opens the shift's card");
  await shot("05_intro");
  await tap(ui(0.5, 0.5, 120, -400), "CLOCK IN");
  await screenIs("shift", "a tap on CLOCK IN starts the shift", 10000);
  await sleep(3500);   // the doors and the count
  await shot("06_shift");
  const shownInShift = await visible("#touch") && await visible("#t-pause") && await visible("#t-board");
  ok(shownInShift, "the on-screen controls show during the shift");
  const sizes = {};
  for (const id of ["#t-pause", "#t-zoom", "#t-board", "#t-letoff"]) sizes[id] = await rect(id);
  const small = Object.entries(sizes).filter(([, r]) => r.w < 44 || r.h < 44);
  ok(small.length === 0, `every on-screen button is at least 44 pt (${Object.entries(sizes).map(([id, r]) => `${id} ${r.w.toFixed(0)}x${r.h.toFixed(0)}`).join(", ")})`);

  // ---- a guest: the first tap picks (their card shows), the second lets them in or fetches them -------------------
  const guest = await waitFor((l) => l.status && l.status.guests && l.status.guests.some((g) => g[1] === 0), 30000);
  ok(!!guest, "a guest arrives");
  if (guest) {
    let g = guest.status.guests.find((g) => g[1] === 0);
    g = await settled(g[0]) || g;
    const before = st();
    await tap(px(g[2], g[3]), "guest", 700);
    const picked = await waitFor((l) => l.status && l.status.picked === g[0], 2500);
    await shot("07_guest_picked");
    ok(!!picked, `a first tap on a waiting guest picks them (guest ${g[0]}; picked ${st()?.picked}) and does nothing else (riders ${st()?.riders}, was ${before.riders})`);
    g = await settled(g[0]) || g;
    await tap(px(g[2], g[3]), "guest again", 700);
    const acted = await waitFor((l) => l.status && (l.status.riders > before.riders || Math.abs(l.status.car - before.car) > 0.05 || l.status.picked !== g[0]), 3000);
    ok(!!acted && st().picked !== g[0], `a second tap acts on them (riders ${before.riders} -> ${st()?.riders}, car ${before.car} -> ${st()?.car})`);
  }
  // Go and get a waiting guest (two taps on them send the car), and stop there with the doors open: true when someone
  // waits at the open car.
  const fetch = async () => {
    for (let tries = 0; tries < 3; tries++) {
      const w = await waitFor((l) => l.status && (l.status.here > 0 || (l.status.guests || []).some((g) => g[1] === 0)), 40000);
      if (!w) return false;
      if (w.status.open && w.status.here > 0) return true;
      const g0 = w.status.guests.find((g) => g[1] === 0);
      const g = await settled(g0[0]) || g0;
      await tap(px(g[2], g[3]), "guest", 350);
      const again = await settled(g[0]) || g;
      await tap(px(again[2], again[3]), "guest again", 600);
      if (await waitFor((l) => l.status && l.status.open && l.status.here > 0, 15000)) return true;
    }
    return false;
  };
  // a rider aboard with the doors open (fetching someone and letting them in if need be)
  const riderAtOpenCar = async () => {
    let r = await waitFor((l) => l.status && l.status.open && l.status.riders > 0, 1500);
    if (r) return r.status;
    if (!(await fetch())) return null;
    await press("#t-board", 900);
    r = await waitFor((l) => l.status && l.status.open && l.status.riders > 0, 3000);
    return r ? r.status : null;
  };
  // ALL IN: whoever was just let off waits at the open car again
  const allIn = async (what) => {
    const w = await waitFor((l) => l.status && l.status.open && l.status.here > 0, 3000) || (await fetch() ? latest() : null);
    if (!w) { ok(false, `someone waiting at the open car for ALL IN ${what}`); return; }
    const r0 = st().riders;
    await sleep(300);
    const ready = await page.evaluate(() => document.querySelector("#t-board").classList.contains("ready"));
    await press("#t-board", 900);
    await waitFor((l) => l.status && l.status.riders > r0, 3000);
    ok(st().riders > r0 && ready, `ALL IN ${what} lets in who's waiting at the open car (riders ${r0} -> ${st().riders}; the button was lit: ${ready})`);
  };
  // ---- LET OFF: a first tap picks a rider, the button lets them off -----------------------------------------------
  {
    const s0 = await riderAtOpenCar();
    if (!s0) ok(false, "a rider at the open car for LET OFF");
    else {
      const rider = await settled(s0.guests.find((g) => g[1] === 1)[0]) || s0.guests.find((g) => g[1] === 1);
      await tap(px(rider[2], rider[3]), "rider", 600);
      const picked = await waitFor((l) => l.status && l.status.picked === rider[0], 2000);
      const lit = await page.evaluate(() => document.querySelector("#t-letoff").classList.contains("ready"));
      await shot("08_rider_picked");
      const r0 = st().riders;
      await press("#t-letoff", 900);
      await waitFor((l) => l.status && l.status.riders < r0, 3000);
      ok(!!picked && lit && st().riders < r0, `a first tap picks a rider (${!!picked}), LET OFF lights up (${lit}) and lets them off here (riders ${r0} -> ${st().riders})`);
    }
  }
  await allIn("after LET OFF");
  // ---- the right-click: a long press on a rider at the open car lets them off -------------------------------------
  if (touch.raw) {
    const s1 = await riderAtOpenCar();
    if (s1) {
      const rider = await settled(s1.guests.find((g) => g[1] === 1)[0]) || s1.guests.find((g) => g[1] === 1);
      const p = px(rider[2], rider[3]);
      await touch.hold(p[0], p[1], 650);
      await waitFor((l) => l.status && l.status.riders < s1.riders, 3000);
      ok(st().riders < s1.riders, `a long press on a rider at the open car lets them off (riders ${s1.riders} -> ${st().riders})`);
      await allIn("after the long press");
    } else ok(false, "a rider at the open car for the long press");
  } else info("no touch protocol here for a long press");
  await shot("09_all_in");
  // ---- a floor: the first tap previews the trip, the second sends the car -----------------------------------------
  {
    const s0 = st();
    const here = Math.round(s0.car);
    await fresh();
    const target = st().floors.find((f) => f[0] !== here && f[0] === (here + 2) % s0.floors.length) || st().floors.find((f) => f[0] !== here);
    await tap(px(target[2], target[3]), "floor", 700);
    const pickedF = await waitFor((l) => l.status && l.status.pickedFloor === target[1], 2500);
    await shot("10_floor_picked");
    const carBefore = st().car;
    ok(!!pickedF, `a first tap on a floor picks it and previews the trip (floor ${target[1]} in slot ${target[0]}; the car stays at ${carBefore})`);
    await fresh();
    const t2 = (st().floors || []).find((f) => f[1] === target[1]) || target;
    await tap(px(t2[2], t2[3]), "floor again", 400);
    const moved = await waitFor((l) => l.status && Math.abs(l.status.car - carBefore) > 0.2, 5000);
    ok(!!moved, `a second tap on the floor sends the car (car ${carBefore} -> ${st()?.car})`);
    await waitFor((l) => l.status && l.status.open, 12000);
  }
  // ---- a panel button sends the car straight away ------------------------------------------------------------------
  {
    const s0 = st();
    const here = Math.round(s0.car);
    const slot = here === 0 ? 1 : 0;
    const cell = ui(1, 0.5, -230 + (-108 + (slot % 3) * 108), -20 + (-385 + Math.floor(slot / 3) * 114) + 18);
    await tap(cell, "panel", 300);
    const moved = await waitFor((l) => l.status && Math.abs(l.status.car - s0.car) > 0.2, 5000);
    ok(!!moved, `a tap on panel button ${slot + 1} sends the car (car ${s0.car} -> ${st()?.car})`);
    await waitFor((l) => l.status && l.status.open, 12000);
  }
  // ---- zoom: the button, then two fingers pinching -----------------------------------------------------------------
  {
    const z0 = st().zoom;
    await press("#t-zoom", 900);
    await waitFor((l) => l.status && Math.abs(l.status.zoom - z0) > 0.5, 3000);
    const z1 = st().zoom;
    ok(Math.abs(z1 - z0) > 0.5, `the ZOOM button switches between the tower and the close-up (zoom ${z0} -> ${z1})`);
    await shot("11_zoomed");
    if (touch.raw) {
      const cx = geo.left + geo.width * 0.5, cy = geo.top + geo.height * 0.5;
      await touch.pinch([cx, cy], z1 > 0.5 ? 140 : 40, z1 > 0.5 ? 40 : 140);
      await waitFor((l) => l.status && Math.abs(l.status.zoom - z1) > 0.25, 3000);
      ok(Math.abs(st().zoom - z1) > 0.25, `two fingers pinching zoom the camera (zoom ${z1} -> ${st().zoom})`);
      // two at once: a finger held on the game while another taps ZOOM
      const z2 = st().zoom;
      const zr = await rect("#t-zoom");
      await touch.chord([geo.left + geo.width * 0.45, geo.top + geo.height * 0.4], [zr.x, zr.y], 400);
      await waitFor((l) => l.status && Math.abs(l.status.zoom - z2) > 0.3, 3000);
      ok(Math.abs(st().zoom - z2) > 0.3, `a tap on ZOOM works while another finger is down on the game (zoom ${z2} -> ${st().zoom})`);
    } else info("no touch protocol for pinch and two-finger checks here");
  }
  // ---- pause by the button, resume by tap, and quit back to the title ----------------------------------------------
  await press("#t-pause", 900);
  await screenIs("pause", "the PAUSE button pauses the shift");
  await sleep(600);
  await shot("12_paused");
  ok(!(await visible("#touch")), "the on-screen controls step aside on the pause card");
  await tap(ui(0.5, 0.5, -272, 126), "RESUME", 900);
  await screenIs("shift", "a tap on RESUME goes back to the shift");
  await sleep(1500);
  await press("#t-pause", 900);
  await waitScreen("pause", 3000);
  await tap(ui(0.5, 0.5, -272, -218), "QUIT TO ROSTER", 700);
  await tap(ui(0.5, 0.5, -272, -218), "REALLY QUIT?", 2500);
  await screenIs("roster", "QUIT TO ROSTER and its confirmation, by tap, leave the shift", 8000);
  await tap(ui(0.5, 0.5, -160, -474), "BACK");
  await screenIs("title", "and BACK reaches the title again");

  // ---- the controls go when a mouse or a key is used, and come back with a touch ---------------------------------
  const touchingNow = () => page.evaluate(() => document.body.classList.contains("touching"));
  await page.mouse.move(200, 120);
  await page.mouse.move(260, 160, { steps: 4 });
  await sleep(400);
  const afterMouse = await touchingNow();
  await touch.tap(geo.left + geo.width * 0.36, geo.top + geo.height * 0.06);
  await sleep(400);
  const afterTouch = await touchingNow();
  await page.keyboard.press("Shift");
  await sleep(400);
  const afterKey = await touchingNow();
  await touch.tap(geo.left + geo.width * 0.36, geo.top + geo.height * 0.06);
  await sleep(400);
  ok(afterMouse === false && afterTouch === true && afterKey === false && (await touchingNow()) === true,
     `touch mode: off after the mouse moves (${!afterMouse}), on after a touch (${afterTouch}), off after a key (${!afterKey}), on after a touch (${await touchingNow()})`);
}

let server;
if (serveDir) {
  const dir = path.resolve(serveDir);
  if (!existsSync(path.join(dir, "index.html"))) { console.error(`check-mobile: no index.html in ${dir}`); process.exit(2); }
  server = await serve(dir);
  url = `http://127.0.0.1:${server.address().port}/OneMoreFloor/`;
  console.log(`check-mobile: serving ${dir} at ${url}`);
}
let allPassed = true;
for (const k of list) {
  if (!profiles[k]) { console.error(`check-mobile: no profile ${k}`); allPassed = false; continue; }
  allPassed = (await check(k, url)) && allPassed;
}
if (server) server.close();
console.log(allPassed ? "check-mobile: PASSED" : "check-mobile: FAILED");
process.exit(allPassed ? 0 : 1);
