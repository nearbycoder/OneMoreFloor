using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OneMoreFloor.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace OneMoreFloor
{
    /// <summary>
    /// Records a scripted gameplay demo: title, roster, Saturday's intro, a minute of the bot playing
    /// (with an on-screen cursor where it clicks and captions), then the results.
    /// Launch with -omfDemo &lt;outdir&gt; -omfDemoPass video|audio. The video pass renders offline at a
    /// locked 30 fps and pipes frames to ffmpeg (outdir/video.mp4). The audio pass plays the same script
    /// in real time with -omfRecordAudio and writes where the script started (outdir/audio_offset.txt).
    /// The simulation steps on a fixed clock with a fixed seed, so both passes play the same shift.
    /// </summary>
    public sealed class DemoReel : MonoBehaviour
    {
        const int Fps = 30, W = 1920, H = 1080;
        const int ShiftIndex = 5;
        const ulong Seed = 21;

        string outDir;
        bool video;
        bool recording;
        int frames;
        Process ffmpeg;
        Stream pipe;

        GameRoot root;
        RectTransform overlay;
        RectTransform cursor;
        Image cursorImg, cursorShadow, fade;
        CanvasGroup cursorGroup;
        Vector2 cursorPos, cursorVel;
        float cursorAlpha, cursorPress;
        Func<Vector2?> cursorTarget;
        readonly List<(Func<Vector2?> At, Color Col)> clicks = new List<(Func<Vector2?>, Color)>();
        float clickTravel;
        bool showBot;
        readonly List<(Image Img, float T)> ripples = new List<(Image, float)>();

        public static void TryStart(GameRoot root)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-omfDemo");
            if (i < 0 || i + 1 >= args.Length) return;
            int p = Array.IndexOf(args, "-omfDemoPass");
            var reel = root.gameObject.AddComponent<DemoReel>();
            reel.outDir = args[i + 1];
            reel.video = p < 0 || p + 1 >= args.Length || args[p + 1] != "audio";
            reel.root = root;
            Directory.CreateDirectory(reel.outDir);
            UnityEngine.Random.InitState(1929);
            root.FixedSeed = Seed;
            Application.runInBackground = true;
            if (reel.video)
            {
                Time.captureFramerate = Fps;
                AudioListener.volume = 0f;
            }
        }

        void Start()
        {
            var save = SaveData.Current;
            int[] stars = { 3, 3, 2, 3, 2 };
            for (int k = 0; k < stars.Length; k++)
            {
                var def = ShiftCatalog.Get(k);
                save.Stars[k] = stars[k];
                save.Best[k] = def.Stars[stars[k] - 1] + 1850 * (k + 1);
                save.Plays[k] = 2 + k;
            }
            save.EndingSeen = false;
            root.Runner.InputEnabled = false;
            var module = EventSystem.current ? EventSystem.current.GetComponent<BaseInputModule>() : null;
            if (module) module.enabled = false;
            if (root.Coach) root.Coach.gameObject.SetActive(false);
            BuildOverlay();
            root.Runner.BotActed += OnBotActed;
            StartCoroutine(Script());
        }

        void OnDestroy()
        {
            if (root && root.Runner) root.Runner.BotActed -= OnBotActed;
        }

        // ---------------------------------------------------------------- script

        IEnumerator Script()
        {
            for (float t = 0f; t < 1.2f; t += UiTime.Dt) yield return null;   // let the title settle before the first frame
            fade.color = Color.black;
            Begin();

            yield return FadeTo(0f, 0.9f);
            yield return Wait(1.4f);

            // title -> roster
            yield return ClickButton(b => HasText(b, "ROSTER"), 1.1f);
            yield return Wait(1.6f);

            // browse the roster, pick Saturday
            yield return HoverButton(b => HasText(b, "Fangs"), 0.9f);
            yield return Wait(0.5f);
            yield return ClickButton(b => HasText(b, "Surf"), 0.8f);
            yield return Wait(2.8f);

            // intro -> clock in
            yield return ClickButton(b => HasText(b, "CLOCK IN"), 0.9f);
            var runner = root.Runner;
            runner.AutoBot = Bot.Strong(Seed);
            showBot = true;
            float shiftStart = due;

            yield return Wait(3.4f);
            yield return Caption("SATURDAY · SURF'S UP", "The hotel shuffles its floors every time you stop. Today the Ocean drops in.", 4.6f);
            yield return Wait(2.4f);
            yield return Caption("RUN THE CAR", "Click a floor or a panel button to send it. Click guests to let them in.", 4.6f);
            yield return Wait(3.0f);
            yield return Caption("READ THE FORECAST", "The cards on the panel show how the tower shuffles at your next stops.", 4.6f);
            yield return Wait(2.0f);
            yield return HoverGuest(3.6f);
            yield return Wait(2.6f);
            yield return Caption("TIPS & STREAKS", "Quick, kind deliveries pay. Too many complaints and you're fired.", 4.6f);
            yield return Wait(shiftStart + 62f - due);

            // jump to the end of the shift
            showBot = false;
            clicks.Clear();
            yield return FadeTo(1f, 0.6f);
            cursorAlpha = 0f;
            var sim = runner.Sim;
            runner.FastForward(sim.TimeLeft + 0.1f);
            var later = Card("LATER THAT SHIFT…", 0f);
            yield return Wait(1.6f);
            yield return FadeGroup(later, 0f, 0.35f);
            Destroy(later.gameObject);
            yield return FadeTo(0f, 0.6f);
            Debug.Log($"[Demo] shift: score {sim.Score} stars {sim.StarCount} delivered {sim.DeliveredCount} complaints {sim.Complaints}");
            yield return Wait(9.5f);

            // end card
            yield return FadeTo(1f, 0.8f);
            var end = EndCard();
            yield return FadeGroup(end, 1f, 0.6f);
            yield return Wait(3.6f);
            yield return FadeGroup(end, 0f, 0.6f);
            yield return Wait(0.3f);
            Finish();
        }

        // Script time since the first recorded frame, and when the current step is due. Every step is
        // scheduled from the previous step's deadline (not from when it actually finished), so per-frame
        // overshoot doesn't accumulate and the 30 fps video pass stays in step with the real-time audio pass.
        float clock, due;

        void Update()
        {
            if (recording) clock += UiTime.Dt;
        }

        void Begin()
        {
            recording = true;
            if (video)
            {
                var psi = new ProcessStartInfo("ffmpeg",
                    $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {W}x{H} -r {Fps} -i - -vf vflip -c:v libx264 -preset slow -crf 16 -pix_fmt yuv420p -movflags +faststart \"{Path.Combine(outDir, "video.mp4")}\"")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                };
                ffmpeg = Process.Start(psi);
                pipe = ffmpeg.StandardInput.BaseStream;
                Debug.Log("[Demo] recording video");
            }
            else
            {
                var tap = AudioTap.Instance;
                double offset = tap ? tap.SecondsWritten : 0;
                File.WriteAllText(Path.Combine(outDir, "audio_offset.txt"), offset.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
                Debug.Log($"[Demo] audio starts at {offset:0.000}s");
            }
        }

        void Finish()
        {
            recording = false;
            if (video)
            {
                pipe.Flush();
                pipe.Close();
                ffmpeg.WaitForExit();
                Debug.Log($"[Demo] wrote {frames} frames ({frames / (float)Fps:0.0}s), ffmpeg exit {ffmpeg.ExitCode}");
            }
            else
            {
                File.WriteAllText(Path.Combine(outDir, "audio_length.txt"), clock.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                AudioTap.Instance?.Finish();
                Debug.Log($"[Demo] audio pass done ({clock:0.0}s)");
            }
            Application.Quit(0);
        }

        void LateUpdate()
        {
            TickCursor(UiTime.Dt);
            if (!recording || !video) return;
            Canvas.ForceUpdateCanvases();
            var tex = Shots.CaptureTexture(W, H);
            var bytes = tex.GetRawTextureData();
            pipe.Write(bytes, 0, bytes.Length);
            Destroy(tex);
            frames++;
        }

        IEnumerator Wait(float seconds)
        {
            due += seconds;
            while (clock < due) yield return null;
        }

        /// <summary>Run <paramref name="step"/> with 0..1 progress over the next <paramref name="seconds"/> of script time.</summary>
        IEnumerator Span(float seconds, Action<float> step, bool advance = true)
        {
            float start = advance ? due : clock, end = start + seconds;
            if (advance) due = end;
            while (clock < end)
            {
                step(Mathf.Clamp01((clock - start) / seconds));
                yield return null;
            }
            step(1f);
        }

        // ---------------------------------------------------------------- cursor

        Vector2? ToOverlay(Vector2 screenPoint)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screenPoint, root.UiCam, out var local) ? local : (Vector2?)null;
        }

        Vector2? OfRect(RectTransform rt)
        {
            if (!rt) return null;
            return ToOverlay(RectTransformUtility.WorldToScreenPoint(root.UiCam, rt.TransformPoint(rt.rect.center)));
        }

        Vector2? OfWorld(Vector3 world)
        {
            var sp = root.WorldCam.WorldToScreenPoint(world);
            return sp.z > 0 ? ToOverlay(sp) : null;
        }

        static bool HasText(UiButton b, string text)
        {
            foreach (var t in b.GetComponentsInChildren<TMP_Text>())
                if (t.text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static UiButton Find(Func<UiButton, bool> match)
        {
            foreach (var b in FindObjectsByType<UiButton>(FindObjectsSortMode.None))
                if (b.isActiveAndEnabled && b.Interactable && match(b)) return b;
            return null;
        }

        IEnumerator MoveTo(Func<Vector2?> target, float seconds)
        {
            cursorAlpha = 1f;
            cursorTarget = target;
            yield return Wait(seconds);
        }

        IEnumerator HoverButton(Func<UiButton, bool> match, float travel)
        {
            var b = Find(match);
            if (!b) { Debug.LogWarning("[Demo] no button to hover"); yield break; }
            var rt = (RectTransform)b.transform;
            yield return MoveTo(() => OfRect(rt), travel);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
            yield return Wait(0.6f);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
        }

        IEnumerator ClickButton(Func<UiButton, bool> match, float travel)
        {
            var b = Find(match);
            if (!b) { Debug.LogWarning("[Demo] no button to click"); yield break; }
            var rt = (RectTransform)b.transform;
            yield return MoveTo(() => OfRect(rt), travel);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerEnterHandler);
            yield return Wait(0.45f);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerDownHandler);
            cursorPress = 1f;
            Ripple(cursorPos, Deco.Gold);
            yield return Wait(0.1f);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(b.gameObject, Pointer(), ExecuteEvents.pointerExitHandler);
            b.Click();
            cursorTarget = null;
        }

        static PointerEventData Pointer() => new PointerEventData(EventSystem.current);

        void OnBotActed(Bot.Action action, int arg)
        {
            if (!showBot) return;
            var runner = root.Runner;
            Func<Vector2?> at;
            Color col = Deco.Gold;
            if (action == Bot.Action.Send)
            {
                var rt = runner.Hud.Panel.SlotRect(arg);
                at = () => OfRect(rt);
            }
            else
            {
                var w = runner.GuestPoint(arg);
                if (!w.HasValue) return;
                var fixedAt = OfWorld(w.Value);
                at = () => fixedAt;
                if (action == Bot.Action.Drop) col = Palette.Hex(0xE0604A);
            }
            clicks.Add((at, col));
            while (clicks.Count > 2) clicks.RemoveAt(0);
        }

        void TickCursor(float dt)
        {
            if (showBot && clicks.Count > 0)
            {
                cursorAlpha = 1f;
                var target = clicks[0].At();
                clickTravel += dt;
                if (!target.HasValue) { clicks.RemoveAt(0); clickTravel = 0f; }
                else
                {
                    cursorPos = Vector2.SmoothDamp(cursorPos, target.Value, ref cursorVel, 0.06f, 9000f, dt);
                    if ((cursorPos - target.Value).sqrMagnitude < 14f * 14f || clickTravel > 0.3f)
                    {
                        cursorPos = target.Value;
                        cursorVel = Vector2.zero;
                        cursorPress = 1f;
                        Ripple(cursorPos, clicks[0].Col);
                        clicks.RemoveAt(0);
                        clickTravel = 0f;
                    }
                }
            }
            else if (cursorTarget != null)
            {
                var target = cursorTarget();
                if (target.HasValue) cursorPos = Vector2.SmoothDamp(cursorPos, target.Value, ref cursorVel, 0.16f, 6000f, dt);
            }

            cursorPress = Mathf.Max(0f, cursorPress - dt * 7f);
            cursorGroup.alpha = Mathf.MoveTowards(cursorGroup.alpha, cursorAlpha, dt * 4f);
            cursor.anchoredPosition = cursorPos;
            cursor.localScale = Vector3.one * (1f - 0.14f * cursorPress);

            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.T += dt / 0.42f;
                if (r.T >= 1f) { Destroy(r.Img.gameObject); ripples.RemoveAt(i); continue; }
                float e = 1f - (1f - r.T) * (1f - r.T);
                r.Img.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(14f, 84f, e);
                var c = r.Img.color; c.a = 0.95f * (1f - r.T); r.Img.color = c;
                ripples[i] = r;
            }
        }

        void Ripple(Vector2 at, Color col)
        {
            var img = UiKit.Image("Ripple", overlay, UiKit.Ring, col, new Vector2(14, 14), at);
            img.transform.SetSiblingIndex(cursor.GetSiblingIndex());
            img.gameObject.layer = GameRoot.UiLayer;
            ripples.Add((img, 0f));
        }

        // ---------------------------------------------------------------- hover a guest

        IEnumerator HoverGuest(float hold)
        {
            var runner = root.Runner;
            var sim = runner.Sim;
            int pid = PickGuest(-1);
            if (pid < 0) yield break;
            showBot = false;
            clicks.Clear();
            Func<Vector2?> at = () => { var w = runner.GuestPoint(pid); return w.HasValue ? OfWorld(w.Value) : null; };
            yield return MoveTo(at, 0.7f);
            runner.ShowHover(pid);
            var cap = StartCoroutine(Caption("HOVER ANYONE", "See where a guest is headed and how long they'll wait.", hold + 0.6f, false));
            due += hold;
            while (clock < due)
            {
                var p = sim.Find(pid);
                if (p == null || p.State == PState.Done || !runner.GuestPoint(pid).HasValue)
                {
                    pid = PickGuest(pid);
                    if (pid < 0) break;
                    runner.ShowHover(pid);
                }
                yield return null;
            }
            runner.ShowHover(-1);
            cursorTarget = null;
            showBot = true;
            yield return cap;
        }

        /// <summary>The guest with the furthest trip ahead, preferring the interesting kinds.</summary>
        int PickGuest(int except)
        {
            var runner = root.Runner;
            var sim = runner.Sim;
            int pick = -1;
            float best = -1f;
            foreach (var p in sim.All)
            {
                if (p.Id == except || p.State == PState.Done || !runner.GuestPoint(p.Id).HasValue || !sim.B.Has(p.Dest)) continue;
                float score = p.State == PState.Waiting
                    ? Mathf.Abs(sim.B.SlotOf(p.At) - sim.Car.Pos) + 1f
                    : Mathf.Abs(sim.B.SlotOf(p.Dest) - sim.Car.Pos);
                if (p.Kind == Kind.Vampire || p.Kind == Kind.Swimmer) score += 2.5f;
                if (score > best) { best = score; pick = p.Id; }
            }
            return pick;
        }

        // ---------------------------------------------------------------- overlay

        void BuildOverlay()
        {
            var go = new GameObject("DemoOverlay");
            go.layer = GameRoot.UiLayer;
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = root.UiCam;
            canvas.planeDistance = 5f;
            canvas.sortingOrder = 500;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.matchWidthOrHeight = 0.6f;
            overlay = (RectTransform)go.transform;

            fade = UiKit.Stretch("Fade", overlay).gameObject.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0);
            fade.raycastTarget = false;

            var arrow = MakeArrow();
            var c = UiKit.Rect("Cursor", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.1f, 0.95f), Vector2.zero, new Vector2(40, 60));
            cursor = c;
            cursorGroup = c.gameObject.AddComponent<CanvasGroup>();
            cursorGroup.alpha = 0f;
            cursorShadow = UiKit.Image("Shadow", c, arrow, new Color(0, 0, 0, 0.35f), new Vector2(40, 60), new Vector2(3, -5));
            cursorImg = UiKit.Image("Arrow", c, arrow, Color.white, new Vector2(40, 60));
            cursorPos = new Vector2(520, -470);
            GameRoot.SetLayerRecursive(go, GameRoot.UiLayer);
        }

        IEnumerator FadeTo(float alpha, float seconds)
        {
            float from = fade.color.a;
            return Span(seconds, t => fade.color = new Color(0, 0, 0, Mathf.Lerp(from, alpha, Ease.InOutCubic(t))));
        }

        IEnumerator FadeGroup(CanvasGroup g, float alpha, float seconds)
        {
            float from = g.alpha;
            return Span(seconds, t => g.alpha = Mathf.Lerp(from, alpha, t));
        }

        IEnumerator Caption(string title, string body, float seconds, bool advance = true)
        {
            var rt = Deco.Panel("Caption", overlay, new Vector2(560, 172), new Vector2(-646, -404), small: true);
            rt.SetSiblingIndex(fade.transform.GetSiblingIndex());
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            Deco.Label("Title", rt, title, 22, new Vector2(500, 30), new Vector2(0, 52), TextAlignmentOptions.Center);
            Deco.Divider(rt, 320, new Vector2(0, 28), 0.8f);
            var text = Deco.Shadowed(UiKit.Text("Body", rt, body, 27, Palette.Cream, UiKit.Body, TextAlignmentOptions.Center, new Vector2(492, 84), new Vector2(0, -22)));
            text.textWrappingMode = TextWrappingModes.Normal;
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            var home = rt.anchoredPosition;
            yield return Span(seconds, u =>
            {
                float t = u * seconds;
                float a = Mathf.Min(Mathf.Clamp01(t / 0.35f), Mathf.Clamp01((seconds - t) / 0.35f));
                g.alpha = a;
                rt.anchoredPosition = home + new Vector2(0, -18f * (1f - Ease.OutCubic(a)));
            }, advance);
            Destroy(rt.gameObject);
        }

        CanvasGroup Card(string text, float alpha)
        {
            var rt = UiKit.Stretch("Card", overlay);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 1f - alpha;
            Deco.Gilded(UiKit.Text("Text", rt, text, 64, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1400, 90)));
            Deco.Divider(rt, 420, new Vector2(0, -64));
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            return g;
        }

        CanvasGroup EndCard()
        {
            var rt = UiKit.Stretch("End", overlay);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            Deco.Label("Eyebrow", rt, "THE SHUFFLETON · EST. 1929", 22, new Vector2(900, 30), new Vector2(0, 150), TextAlignmentOptions.Center);
            Deco.Gilded(UiKit.Text("Logo", rt, "One More Floor", 136, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(1600, 170), new Vector2(0, 40)));
            Deco.Divider(rt, 560, new Vector2(0, -62));
            Deco.Shadowed(UiKit.Text("Tag", rt, "A hotel that rearranges its floors every time you stop.", 32, Palette.Cream, UiKit.Body, TextAlignmentOptions.Center, new Vector2(1400, 44), new Vector2(0, -118)));
            GameRoot.SetLayerRecursive(rt.gameObject, GameRoot.UiLayer);
            return g;
        }

        static Sprite MakeArrow()
        {
            const int w = 80, h = 120;
            var pts = new[] { new Vector2(6, 6), new Vector2(6, 96), new Vector2(27, 76), new Vector2(42, 110), new Vector2(56, 104), new Vector2(41, 71), new Vector2(70, 71) };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var fill = (Color)Palette.Hex(0xFFF6E2);
            var edge = (Color)Palette.Hex(0x24160E);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = float.MaxValue;
                bool inside = false;
                for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
                {
                    Vector2 a = pts[j], b = pts[i];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    d = Mathf.Min(d, (p - (a + ab * t)).magnitude);
                    if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x)) inside = !inside;
                }
                float sd = inside ? -d : d;
                float alpha = Mathf.Clamp01(4.5f - sd);
                float inner = Mathf.Clamp01(-sd - 4f);
                var c = Color.Lerp(edge, fill, inner);
                c.a = alpha;
                px[(h - 1 - y) * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
