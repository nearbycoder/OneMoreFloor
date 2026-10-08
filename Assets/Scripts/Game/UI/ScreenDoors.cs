using System;
using UnityEngine;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>
    /// A pair of brass-trimmed enamel elevator doors over the whole screen, for going into and out of a shift: they
    /// slide shut (with the door sound), the change happens behind them (the sky, the light, the framing), a ding, and
    /// they open on the new scene. REDUCED MOTION fades them in and out instead of sliding. Clicks, keys and the pad
    /// are held while they're moving or shut.
    /// </summary>
    public sealed class ScreenDoors : MonoBehaviour
    {
        public static ScreenDoors Instance { get; private set; }
        /// <summary>The doors are closing, shut or opening.</summary>
        public static bool Busy => Instance != null && Instance.phase != Phase.Idle;

        public const float CloseTime = 0.34f, HoldTime = 0.16f, OpenTime = 0.42f;
        public const float FadeIn = 0.2f, FadeOut = 0.26f;
        /// <summary>The whole transition as designed: sliding, and with REDUCED MOTION.</summary>
        public const float SlideTotal = CloseTime + HoldTime + OpenTime, FadeTotal = FadeIn + HoldTime + FadeOut;

        enum Phase { Idle, Closing, Shut, Opening }
        Phase phase;
        float t;
        Action change;
        RectTransform root, left, right;
        readonly System.Collections.Generic.List<TMPro.TextMeshProUGUI> kickers = new System.Collections.Generic.List<TMPro.TextMeshProUGUI>(),
            titles = new System.Collections.Generic.List<TMPro.TextMeshProUGUI>();
        CanvasGroup group;
        Image blocker;
        bool fade;

        /// <summary>Self-tests: how shut the doors were (0 open, 1 shut) when the last change ran, and how long the
        /// last transition's animation took, from the call to fully open (not counting the frame that made the change).</summary>
        public float ShutAtChange { get; private set; } = -1f;
        public float LastDuration { get; private set; } = -1f;
        /// <summary>0 open, 1 shut: the doors' position (or, with REDUCED MOTION, their opacity).</summary>
        public float Shut { get; private set; }
        /// <summary>Self-tests: closing (before the change) or opening (after it).</summary>
        public bool Closing => phase == Phase.Closing;
        public bool Opening => phase == Phase.Opening;
        /// <summary>Self-tests: how far the left leaf sits from its shut position, in canvas units (0 with REDUCED MOTION).</summary>
        public float LeftOffset => left.anchoredPosition.x;
        float started;
        int startFrame;
        float clock, longest;
        bool afterChange;
        /// <summary>Self-tests: the length of the frame that made the last change (hidden behind the shut doors).</summary>
        public float ChangeFrame { get; private set; }
        /// <summary>Self-tests: the longest frame of the last transition (each phase can overrun by up to one).</summary>
        public float Longest => longest;

        public static ScreenDoors Create(Transform canvas)
        {
            var rt = UiKit.Stretch("ScreenDoors", canvas);
            var d = rt.gameObject.AddComponent<ScreenDoors>();
            Instance = d;
            d.root = rt;
            d.group = rt.gameObject.AddComponent<CanvasGroup>();
            d.group.blocksRaycasts = false;
            d.group.interactable = false;
            d.blocker = rt.gameObject.AddComponent<Image>();
            d.blocker.color = new Color(0, 0, 0, 0);
            d.blocker.raycastTarget = true;
            d.left = d.Door("Left", 0f);
            d.right = d.Door("Right", 1f);
            d.Place(0f);
            rt.gameObject.SetActive(false);
            return d;
        }

        /// <summary>One door: an enamel leaf with a brass frame, two brass bands and half a sunburst at the seam.</summary>
        RectTransform Door(string name, float side)
        {
            bool isLeft = side < 0.5f;
            var door = UiKit.Rect("Door" + name, root, new Vector2(isLeft ? 0f : 0.5f, 0f), new Vector2(isLeft ? 0.5f : 1f, 1f),
                                  new Vector2(isLeft ? 1f : 0f, 0.5f), Vector2.zero, Vector2.zero);
            // the shadow each leaf throws on the scene as it slides over it; everything else is clipped to the leaf,
            // so the medallion at the seam is only ever half on each one
            var shadow = UiKit.Image("Shadow", door, Deco.Shadow, new Color(1, 1, 1, 0.9f), Vector2.zero);
            Stretch(shadow.rectTransform, new Vector2(-40, -40), new Vector2(40, 40));
            var outer = door;
            door = UiKit.Stretch("Leaf", outer);
            door.gameObject.AddComponent<RectMask2D>();
            var face = UiKit.Image("Face", door, null, Palette.Hex(0x2A1E2E), Vector2.zero);
            Deco.Fill(face.rectTransform);
            // a lighter panel inset in the leaf, and the brass frame round it
            var inset = UiKit.Image("Inset", door, null, Palette.Hex(0x3A2838), Vector2.zero);
            Stretch(inset.rectTransform, new Vector2(56, 56), new Vector2(-56, -56));
            Frame(door, 44f, 4f, new Color(0.84f, 0.66f, 0.29f, 0.9f));
            Frame(door, 64f, 2f, new Color(0.84f, 0.66f, 0.29f, 0.55f));
            // brass bands a quarter and seven eighths of the way up, clear of the caption
            foreach (float y in new[] { 0.25f, 0.88f })
            {
                var band = UiKit.Image("Band", door, null, new Color(0.84f, 0.66f, 0.29f, 0.8f), Vector2.zero);
                var b = band.rectTransform;
                b.anchorMin = new Vector2(0f, y);
                b.anchorMax = new Vector2(1f, y);
                b.sizeDelta = new Vector2(-128f, 6f);
                b.anchoredPosition = Vector2.zero;
            }
            // half a sunburst of brass rays fanning out from the middle of the seam
            var burst = UiKit.Rect("Sunburst", door, new Vector2(isLeft ? 1f : 0f, 0.5f), new Vector2(isLeft ? 1f : 0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-60f, 60f, i / 8f);
                var ray = UiKit.Image("Ray" + i, burst, null, new Color(0.95f, 0.78f, 0.42f, i % 2 == 0 ? 0.75f : 0.4f), new Vector2(i % 2 == 0 ? 170f : 120f, 4f));
                var r = ray.rectTransform;
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.localEulerAngles = new Vector3(0, 0, isLeft ? 180f - a : a);
            }
            UiKit.Image("Hub", burst, UiKit.Circle, Palette.Hex(0xF2C66B), new Vector2(90, 90)).rectTransform.anchoredPosition = Vector2.zero;
            UiKit.Image("HubInner", burst, UiKit.Circle, Palette.Hex(0x2A1E2E), new Vector2(62, 62)).rectTransform.anchoredPosition = Vector2.zero;
            // the destination, centred on the seam (each leaf draws its own half), and the hotel's line under the hub
            var kicker = UiKit.Text("Kicker", burst, "", 26, Deco.Gold, UiKit.Signage, TMPro.TextAlignmentOptions.Center, new Vector2(1400, 40), new Vector2(0, 300));
            kicker.characterSpacing = 14f;
            Deco.Shadowed(kicker, 0.7f, 0.8f, 0.3f);
            var title = Deco.Gilded(UiKit.Text("Title", burst, "", 96, Color.white, UiKit.Display, TMPro.TextAlignmentOptions.Center, new Vector2(1400, 130), new Vector2(0, 222)));
            var line = UiKit.Text("Line", burst, "THE SHUFFLETON  ·  EST. 1929", 20, Deco.Muted, UiKit.Signage, TMPro.TextAlignmentOptions.Center, new Vector2(1400, 34), new Vector2(0, -196));
            line.characterSpacing = 12f;
            kickers.Add(kicker);
            titles.Add(title);
            // the seam: a bright brass edge where the leaves meet
            var edge = UiKit.Image("Edge", door, null, new Color(0.96f, 0.82f, 0.48f, 1f), Vector2.zero);
            var e = edge.rectTransform;
            e.anchorMin = new Vector2(isLeft ? 1f : 0f, 0f);
            e.anchorMax = new Vector2(isLeft ? 1f : 0f, 1f);
            e.pivot = new Vector2(isLeft ? 1f : 0f, 0.5f);
            e.sizeDelta = new Vector2(6f, 0f);
            e.anchoredPosition = Vector2.zero;
            burst.SetAsLastSibling();
            return outer;
        }

        static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void Frame(RectTransform door, float inset, float width, Color c)
        {
            // four thin bars, inset from the leaf's edges
            void Bar(Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
            {
                var img = UiKit.Image("Frame", door, null, c, Vector2.zero);
                img.rectTransform.anchorMin = aMin;
                img.rectTransform.anchorMax = aMax;
                img.rectTransform.offsetMin = oMin;
                img.rectTransform.offsetMax = oMax;
            }
            Bar(new Vector2(0, 0), new Vector2(1, 0), new Vector2(inset, inset), new Vector2(-inset, inset + width));
            Bar(new Vector2(0, 1), new Vector2(1, 1), new Vector2(inset, -inset - width), new Vector2(-inset, -inset));
            Bar(new Vector2(0, 0), new Vector2(0, 1), new Vector2(inset, inset), new Vector2(inset + width, -inset));
            Bar(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-inset - width, inset), new Vector2(-inset, -inset));
        }

        /// <summary>
        /// Close the doors, run <paramref name="action"/> behind them, then open. While a transition is already
        /// running, the request is dropped (a second click on the same button).
        /// </summary>
        public static void Run(Action action, string kicker = "", string title = "The Shuffleton")
        {
            if (Instance == null) { action?.Invoke(); return; }
            Instance.Begin(action, kicker, title);
        }

        void Begin(Action action, string kicker, string title)
        {
            if (phase != Phase.Idle) return;
            change = action;
            foreach (var k in kickers) k.text = (kicker ?? "").ToUpperInvariant();
            foreach (var tt in titles) tt.text = title ?? "";
            fade = SaveData.Current.ReducedMotion;
            phase = Phase.Closing;
            t = 0f;
            started = UiTime.Now;
            startFrame = Time.frameCount;
            clock = 0f;
            longest = 0f;
            afterChange = false;
            ShutAtChange = -1f;
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            group.blocksRaycasts = true;
            Place(0f);
            AudioDirector.Instance?.Sfx(fade ? "ui_whoosh" : "door_close", fade ? 0.4f : 0.6f);
        }

        void Update()
        {
            if (phase == Phase.Idle) return;
            float dt = UiTime.Dt;
            if (afterChange)
            {
                // the frame that made the change (a shift starting, say) can be slow: it stays hidden behind the
                // shut doors and doesn't eat into the hold, so they still open smoothly
                afterChange = false;
                ChangeFrame = dt;
                return;
            }
            t += dt;
            clock += dt;
            longest = Mathf.Max(longest, dt);
            switch (phase)
            {
                case Phase.Closing:
                {
                    float k = Mathf.Clamp01(t / (fade ? FadeIn : CloseTime));
                    Place(fade ? k : Ease.InOutCubic(k));
                    if (k >= 1f)
                    {
                        phase = Phase.Shut;
                        t = 0f;
                        ShutAtChange = Shut;
                        Debug.Log($"[Doors] shut after {UiTime.Now - started:0.00} s ({Time.frameCount - startFrame} frames)");
                        try { change?.Invoke(); }
                        catch (Exception ex) { Debug.LogException(ex); }
                        change = null;
                        afterChange = true;
                        // the doors are shut: let the camera land on the new framing before they open
                        GameRoot.Instance?.Rig?.Snap();
                        AudioDirector.Instance?.Sfx("ding_quick", 0.55f);
                    }
                    break;
                }
                case Phase.Shut:
                    if (t >= HoldTime)
                    {
                        phase = Phase.Opening;
                        t = 0f;
                        if (!fade) AudioDirector.Instance?.Sfx("door_open", 0.55f);
                    }
                    break;
                case Phase.Opening:
                {
                    float k = Mathf.Clamp01(t / (fade ? FadeOut : OpenTime));
                    Place(1f - (fade ? k : Ease.OutCubic(k)));
                    if (k >= 1f)
                    {
                        phase = Phase.Idle;
                        group.blocksRaycasts = false;
                        LastDuration = clock;
                        Debug.Log($"[Doors] open again after {UiTime.Now - started:0.00} s: {clock:0.00} s of animation over {Time.frameCount - startFrame} frames "
                                  + $"(longest {longest * 1000f:0} ms), plus the {ChangeFrame * 1000f:0} ms frame that made the change behind them");
                        root.gameObject.SetActive(false);
                    }
                    break;
                }
            }
        }

        /// <summary>0 open, 1 shut.</summary>
        void Place(float k)
        {
            Shut = k;
            if (fade)
            {
                group.alpha = k;
                left.anchoredPosition = Vector2.zero;
                right.anchoredPosition = Vector2.zero;
                return;
            }
            group.alpha = 1f;
            // each leaf is half the screen wide; open, it sits just past its edge (shadow included)
            float w = root.rect.width * 0.5f + 60f;
            left.anchoredPosition = new Vector2(-w * (1f - k), 0f);
            right.anchoredPosition = new Vector2(w * (1f - k), 0f);
        }
    }
}
