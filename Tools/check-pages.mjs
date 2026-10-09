// Checks the browser build as GitHub Pages serves it: loads it in headless Chromium and/or Firefox and exits 0 only when
// every browser reaches the title screen with no console errors, page errors or failed requests.
//
//   node Tools/check-pages.mjs https://nearbycoder.github.io/OneMoreFloor/      the live site
//   node Tools/check-pages.mjs --serve Builds/Pages                             the local build, served at /OneMoreFloor/
//
// Options:
//   --browser chromium|firefox|all   which browsers (default all)
//   --play                           also: sound starts after the first click (and, in Chromium, not before it), a
//                                    setting survives a reload, and a short scripted shift scores (the full local
//                                    test; the title check is the default)
//   --out <dir>                      logs and screenshots (default Logs/pages-check), one folder per browser
//   --timeout <s>                    how long the title may take to come up (default 240)
//
// The game reports what's on screen to the page twice a second as window.oneMoreFloor ({screen, score, delivered, time,
// fidelity, fullscreen, largeText}; Assets/Plugins/WebGL/OneMoreFloorWeb.jslib). Every browser gets a fresh profile, so
// nothing here touches a real save.
//
// Needs playwright-core (PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core, or any copy this script finds). Chromium
// is a Playwright build from ~/.cache/ms-playwright (CHROMIUM_PATH overrides); Firefox is the system Firefox
// (FIREFOX_PATH, default /usr/bin/firefox) over WebDriver BiDi, with its temporary profile under Logs/tmp.
import { createRequire } from "node:module";
import { createReadStream, existsSync, mkdirSync, readdirSync, statSync, writeFileSync } from "node:fs";
import http from "node:http";
import os from "node:os";
import path from "node:path";

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname), "..");
const args = process.argv.slice(2);
const opt = (name, fallback) => { const i = args.indexOf(name); return i >= 0 && i + 1 < args.length ? args[i + 1] : fallback; };
const serveDir = opt("--serve", null);
const play = args.includes("--play");
const outRoot = path.resolve(opt("--out", path.join(root, "Logs", "pages-check")));
const titleTimeout = Number(opt("--timeout", 240)) * 1000;
const which = opt("--browser", "all");
const browsers = which === "all" ? ["chromium", "firefox"] : [which];
const positional = args.filter((a, i) => !a.startsWith("--") && !(i > 0 && ["--serve", "--out", "--timeout", "--browser"].includes(args[i - 1])));
let url = positional[0];
if (!url && !serveDir) {
  console.error("usage: node Tools/check-pages.mjs <url> | --serve <dir>  [--browser chromium|firefox|all] [--play] [--out dir]");
  process.exit(2);
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------------------------------------------------------------------------------------------------- playwright-core

function findPlaywright() {
  const require = createRequire(import.meta.url);
  const tries = [process.env.PLAYWRIGHT_CORE, "playwright-core"].filter(Boolean);
  for (const t of tries) { try { return require(t); } catch (e) { } }
  // any project on this machine that has it (newest first)
  const found = [];
  for (const base of [path.join(os.homedir(), "Sites"), path.dirname(root)]) {
    let names = [];
    try { names = readdirSync(base); } catch (e) { continue; }
    for (const n of names) {
      const p = path.join(base, n, "node_modules", "playwright-core");
      try { found.push({ p, v: require(path.join(p, "package.json")).version }); } catch (e) { }
    }
  }
  const num = (v) => v.split(".").map(Number).reduce((a, x) => a * 1000 + (x || 0), 0);
  found.sort((a, b) => num(b.v) - num(a.v));
  for (const f of found) { try { return require(f.p); } catch (e) { } }
  console.error("check-pages: playwright-core not found; set PLAYWRIGHT_CORE=/path/to/node_modules/playwright-core");
  process.exit(2);
}
const pw = findPlaywright();

// the newest Chromium in Playwright's cache, when this playwright-core's own revision isn't there
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

// --------------------------------------------------------------------------------------- a static server like Pages

// Serves <dir> at /OneMoreFloor/ only, case-sensitively, with no Content-Encoding headers (GitHub Pages sets none for
// .br files), so the build has to decompress itself, as it will online.
function serve(dir) {
  const base = "/OneMoreFloor/";
  const types = { ".html": "text/html; charset=utf-8", ".js": "application/javascript", ".json": "application/json",
                  ".wasm": "application/wasm", ".png": "image/png", ".jpg": "image/jpeg", ".svg": "image/svg+xml",
                  ".css": "text/css", ".ico": "image/x-icon" };
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

// ------------------------------------------------------------------------------------------------------ in the page

// An analyser on every AudioContext that reaches the speakers: tells us whether the game is producing sound.
const audioProbe = () => {
  const analysers = [];
  // every AudioContext's state changes, in the console log (when the game's sound was allowed to start)
  const Ctx = window.AudioContext;
  if (Ctx) {
    window.AudioContext = function (...a) {
      const ctx = new Ctx(...a);
      const t0 = performance.now();
      console.log(`[audio] context made at ${t0.toFixed(0)} ms, ${ctx.state}`);
      ctx.addEventListener("statechange", () => console.log(`[audio] context ${ctx.state} at ${performance.now().toFixed(0)} ms`));
      return ctx;
    };
    window.AudioContext.prototype = Ctx.prototype;
  }
  window.addEventListener("pointerdown", () => console.log(`[audio] first pointerdown at ${performance.now().toFixed(0)} ms`), { once: true });
  const connect = AudioNode.prototype.connect;
  AudioNode.prototype.connect = function (dest, ...rest) {
    const r = connect.call(this, dest, ...rest);
    try {
      if (typeof AudioDestinationNode !== "undefined" && dest instanceof AudioDestinationNode) {
        const ctx = dest.context;
        if (!ctx.__probe) {
          ctx.__probe = ctx.createAnalyser();
          ctx.__probe.fftSize = 2048;
          const mute = ctx.createGain();
          mute.gain.value = 0;
          connect.call(ctx.__probe, mute);
          connect.call(mute, ctx.destination);
          analysers.push(ctx.__probe);
        }
        connect.call(this, ctx.__probe);
      }
    } catch (e) { /* never break the game's audio */ }
    return r;
  };
  window.__audio = () => {
    let peak = 0;
    const buf = new Float32Array(2048);
    for (const a of analysers) {
      a.getFloatTimeDomainData(buf);
      for (let i = 0; i < buf.length; i++) peak = Math.max(peak, Math.abs(buf[i]));
    }
    return { peak, contexts: analysers.length, states: analysers.map((a) => a.context.state) };
  };
  // Does this browser hold sound back until the first input at all? A context of our own, made and resumed before any
  // input: where sound may start, resume() settles (Firefox takes a moment, longer on a busy machine); where it must
  // wait for input, it doesn't within 3 s.
  let blocks = null;
  setTimeout(() => {
    const ctx = new AudioContext();
    const done = (b) => { if (blocks === null) { blocks = b; ctx.close(); } };
    ctx.resume().then(() => done(ctx.state !== "running"), () => done(true));
    setTimeout(() => done(ctx.state !== "running"), 3000);
  }, 0);
  // The page reports to the check through the console four times a second: page.evaluate counts as a user gesture,
  // which would let the game's sound start before the first click and spoil that check.
  setInterval(() => {
    console.log("[check] " + JSON.stringify({ status: window.oneMoreFloor || null, error: window.oneMoreFloorError || null,
                                               audio: window.__audio(), blocks }));
  }, 250);
};

// ---------------------------------------------------------------------------------------------------------- a browser

async function check(engine, url) {
  const out = path.join(outRoot, engine);
  mkdirSync(out, { recursive: true });
  const results = [];
  const ok = (pass, what) => { results.push({ pass, what }); console.log(`[${engine}] ${pass ? "PASS" : "FAIL"} ${what}`); return pass; };
  const log = [];
  const note = (line) => { log.push(line); };

  const launch = { headless: true };
  if (engine === "chromium") {
    launch.executablePath = cachedChromium();
    // the real GPU through ANGLE/Vulkan rather than SwiftShader; sound waits for a click, as in a normal browser
    launch.args = ["--use-angle=vulkan", "--enable-features=Vulkan", "--ignore-gpu-blocklist",
                   "--autoplay-policy=document-user-activation-required"];
    launch.ignoreDefaultArgs = ["--autoplay-policy=no-user-gesture-required"];   // Playwright's own default lets pages autoplay
  } else if (engine === "firefox") {
    process.env.TMPDIR = path.join(root, "Logs", "tmp");
    mkdirSync(process.env.TMPDIR, { recursive: true });
    launch.channel = "moz-firefox";
    launch.executablePath = process.env.FIREFOX_PATH || "/usr/bin/firefox";
    // Firefox's default prefs: it lets Web Audio start without input (media.autoplay.block-webaudio is off), and when
    // that's turned on, headless Firefox under automation applies it to some contexts and not others
  }

  let browser;
  const summary = { engine, url, play };
  try {
    browser = await pw[engine === "firefox" ? "firefox" : "chromium"].launch(launch);
    summary.version = browser.version();
    const context = await browser.newContext({ viewport: { width: 1600, height: 900 } });
    await context.addInitScript(audioProbe);
    const page = await context.newPage();
    const errors = [];
    let latest = { status: null, error: null, audio: { peak: 0, states: [] }, blocks: null };
    page.on("console", (m) => {
      const text = m.text();
      if (text.startsWith("[check] ")) { try { latest = JSON.parse(text.slice(8)); } catch (e) { } return; }
      note(`[${m.type()}] ${text}`);
      if (m.type() === "error") errors.push("console: " + m.text());
    });
    page.on("pageerror", (e) => { note("[pageerror] " + e); errors.push("page: " + e); });
    let bytes = 0, requests = 0;
    page.on("requestfailed", (r) => {
      note(`[requestfailed] ${r.url()} ${r.failure()?.errorText}`);
      // the engine's loader revokes its own blob: URLs once it's done with them; only real downloads count
      if (/^https?:/.test(r.url())) errors.push("request failed: " + r.url());
    });
    page.on("response", async (r) => {
      requests++;
      if (r.status() >= 400) { note(`[http ${r.status()}] ${r.url()}`); errors.push(`HTTP ${r.status()}: ${r.url()}`); }
      const len = Number(r.headers()["content-length"] || 0);
      if (len) bytes += len;
      else { try { bytes += (await r.request().sizes()).responseBodySize; } catch (e) { } }
    });
    const shot = (name) => page.screenshot({ path: path.join(out, name + ".png") }).catch(() => {});
    const status = async () => latest.status;
    const waitScreen = async (screen, ms) => {
      for (const end = Date.now() + ms; Date.now() < end; await sleep(250)) {
        const s = await status();
        if (s && s.screen === screen) return s;
        if (latest.error) return null;
      }
      return null;
    };

    // ---- load to the title -------------------------------------------------------------------------------
    const t0 = Date.now();
    await page.goto(url, { waitUntil: "load", timeout: 120000 });
    const atTitle = await waitScreen("title", titleTimeout);
    summary.loadSeconds = Number(((Date.now() - t0) / 1000).toFixed(1));
    summary.downloadMB = Number((bytes / 1048576).toFixed(1));
    summary.requests = requests;
    await sleep(3000);  // the title's first seconds: anything it logs counts
    const pageError = latest.error;
    const before = latest.audio, blocks = latest.blocks;
    await shot("01_title");
    // (page.evaluate from here on: it counts as a user gesture)
    const overlay = await page.evaluate(() => getComputedStyle(document.querySelector("#loading")).display).catch(() => "?");
    summary.renderer = await page.evaluate(() => {
      const gl = document.createElement("canvas").getContext("webgl2");
      if (!gl) return "no WebGL2";
      const ext = gl.getExtension("WEBGL_debug_renderer_info");
      return ext ? gl.getParameter(ext.UNMASKED_RENDERER_WEBGL) : gl.getParameter(gl.RENDERER);
    }).catch(() => "unknown");
    console.log(`[${engine}] ${summary.version}, renderer ${summary.renderer}: ${atTitle ? "title" : "no title"} after ${summary.loadSeconds} s, ` +
                `${summary.downloadMB} MB in ${requests} requests`);
    ok(!!atTitle && !pageError && overlay === "none", `reached the title screen${pageError ? " (page says: " + pageError + ")" : ""}`);
    ok(errors.length === 0, `no console errors, page errors or failed requests${errors.length ? ": " + errors.slice(0, 5).join(" | ") : ""}`);
    summary.fidelityAtStart = atTitle ? atTitle.fidelity : null;

    if (play && atTitle) {
      // ---- sound waits for the first input, then plays -------------------------------------------------------
      // an empty corner of the title (the attract tower plays on the right), not a button
      await page.mouse.click(620, 60);
      let after = null;
      for (let i = 0; i < 20; i++) {
        await sleep(250);
        after = latest.audio;
        if (after.peak > 0.001 && after.states.every((s) => s === "running")) break;
      }
      if (blocks && engine === "chromium")
        ok(before.states.every((s) => s !== "running") || before.peak === 0,
           `no sound before the first click (contexts ${before.states.join(",") || "none"}, peak ${before.peak.toFixed(4)})`);
      else console.log(`[${engine}] INFO before the first click: contexts ${before.states.join(",") || "none"}, peak ` +
                       `${before.peak.toFixed(4)} (${engine === "firefox" ? "Firefox lets Web Audio start without input by default"
                                                                       : "this browser lets pages play sound without input"})`);
      ok(after.peak > 0.001 && after.states.includes("running"),
         `sound plays after the first click (contexts ${after.states.join(",")}, peak ${after.peak.toFixed(3)})`);

      // ---- a setting survives a reload: GRAPHICS FIDELITY one step down, LARGER TEXT on --------------------------
      const fid0 = atTitle.fidelity;
      await page.mouse.click(300, 737);           // SETTINGS (the title's left column at 1600x900)
      const inSettings = await waitScreen("settings", 5000);
      await sleep(800);
      await shot("02_settings");
      await page.mouse.click(903, 501);            // GRAPHICS FIDELITY: the LOW notch (MEDIUM, the web default, is next to it)
      await sleep(600);
      await page.mouse.click(1030, 668);           // LARGER TEXT
      await sleep(600);
      await shot("03_settings_changed");
      const changed = await status();
      await page.mouse.click(800, 778);            // DONE (saves)
      await sleep(2000);
      ok(!!inSettings && changed.fidelity !== fid0 && changed.largeText,
         `Settings changed GRAPHICS FIDELITY ${fid0} -> ${changed?.fidelity} and LARGER TEXT -> ${changed?.largeText}`);
      await page.reload({ waitUntil: "load" });
      latest = { status: null, error: null, audio: { peak: 0, states: [] }, blocks: null };   // only the new page's reports
      const again = await waitScreen("title", titleTimeout);
      await sleep(1500);
      await shot("04_after_reload");
      ok(!!again && again.fidelity === changed.fidelity && again.largeText === true,
         `after a reload GRAPHICS FIDELITY is ${again?.fidelity} and LARGER TEXT ${again?.largeText} (kept in IndexedDB)`);

      // ---- Alt+Enter asks the browser for fullscreen (for information: a headless browser has no real screen) ----
      await page.keyboard.press("Alt+Enter");
      await sleep(2500);
      const fs = await page.evaluate(() => !!document.fullscreenElement);
      const fsStatus = await status();
      console.log(`[${engine}] INFO Alt+Enter: the page ${fs ? "went" : "did not go"} fullscreen, FULLSCREEN reads ${fsStatus?.fullscreen}`);
      if (fs) { await page.keyboard.press("Alt+Enter"); await sleep(2500); }
      const fsAfter = await page.evaluate(() => !!document.fullscreenElement);
      console.log(`[${engine}] INFO after a second Alt+Enter the page is ${fsAfter ? "still" : "not"} fullscreen, FULLSCREEN reads ${(await status())?.fullscreen}`);

      // ---- a short scripted shift ------------------------------------------------------------------------------
      await page.mouse.click(300, 550);            // START SHIFT
      await sleep(1500);
      await shot("05_intro");
      let inShift = null;
      for (let i = 0; i < 12 && !inShift; i++) {
        await page.keyboard.press("Enter");        // through the intro card(s)
        inShift = await waitScreen("shift", 1500);
      }
      await sleep(4000);  // the doors and the countdown
      await shot("06_shift");
      // a simple player: let everyone in, pick a rider in the car and send the car to their floor; now and then
      // go and fetch whoever is waiting on another floor
      for (let i = 0; i < 14; i++) {
        await page.keyboard.press("Space");
        await sleep(1200);
        await page.keyboard.press("ArrowRight");
        await sleep(200);
        await page.keyboard.press("Enter");
        await sleep(2400);
        if (i % 3 === 2) {
          await page.keyboard.press(i % 2 ? "ArrowUp" : "ArrowDown");
          await sleep(200);
          await page.keyboard.press("Enter");
          await sleep(2400);
        }
        if (i === 7) await shot("07_playing");
      }
      const played = await status();
      ok(!!inShift, "START SHIFT and Enter through the intro start a shift");
      ok(!!played && played.screen === "shift" && played.time > (inShift?.time ?? 0) + 10 && (played.delivered > 0 || played.score > 0),
         `the scripted player scores (shift clock ${played?.time} s, ${played?.delivered} delivered, score ${played?.score})`);
      await page.keyboard.press("Escape");
      const paused = await waitScreen("pause", 3000);
      await sleep(800);
      await shot("08_paused");
      ok(!!paused, "Esc pauses the shift");
      ok(errors.length === 0, `still no console errors, page errors or failed requests${errors.length ? ": " + errors.slice(0, 5).join(" | ") : ""}`);
    }
    await context.close();
  } catch (e) {
    ok(false, "the check ran: " + (e && e.message ? e.message.split("\n")[0] : e));
  } finally {
    if (browser) await browser.close().catch(() => {});
    summary.results = results;
    summary.passed = results.length > 0 && results.every((r) => r.pass);
    writeFileSync(path.join(out, "console.log"), (log.length > 20000 ? [...log.slice(0, 10000), "[...]", ...log.slice(-10000)] : log).join("\n") + "\n");
    writeFileSync(path.join(out, "summary.json"), JSON.stringify(summary, null, 2) + "\n");
    console.log(`[${engine}] ${summary.passed ? "PASSED" : "FAILED"}; console log and screenshots in ${out}`);
  }
  return summary.passed;
}

let server;
if (serveDir) {
  const dir = path.resolve(serveDir);
  if (!existsSync(path.join(dir, "index.html"))) { console.error(`check-pages: no index.html in ${dir}`); process.exit(2); }
  server = await serve(dir);
  url = `http://127.0.0.1:${server.address().port}/OneMoreFloor/`;
  console.log(`check-pages: serving ${dir} at ${url}`);
}
let allPassed = true;
for (const b of browsers) allPassed = (await check(b, url)) && allPassed;
if (server) server.close();
console.log(allPassed ? "check-pages: PASSED" : "check-pages: FAILED");
process.exit(allPassed ? 0 : 1);
