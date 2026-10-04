using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>A gold (primary) or enamel button with depth, hover sheen, focus glow, press punch and sounds.</summary>
    public sealed class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Action OnClick;
        public Image Bg;
        public TextMeshProUGUI Label;
        public bool Interactable = true;
        /// <summary>The screen's default action (glows while the mouse is in use; Enter runs it).</summary>
        public bool Focused;
        /// <summary>Set by <see cref="UiNav"/> when the gamepad/keys focus ring is on this button.</summary>
        public bool NavFocus;
        Image sheen, glow;
        Color baseCol = Color.white, hoverCol = Color.white;
        Color labelCol;
        float hover, hoverShown, press, punch, focusShown, t;
        float labelY;

        public static UiButton Create(Transform parent, string text, Vector2 pos, Vector2 size, Action onClick, bool primary = false, float fontSize = 34f)
        {
            var root = UiKit.Rect("Btn_" + text, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var b = root.gameObject.AddComponent<UiButton>();
            b.glow = UiKit.Image("Glow", root, Deco.Shadow, new Color(1f, 0.78f, 0.35f, 0f), size + new Vector2(70, 70));
            UiKit.Image("Shadow", root, Deco.Shadow, new Color(1, 1, 1, 0.85f), size + new Vector2(36, 36), new Vector2(0, -9));
            var bg = UiKit.Image("Face", root, primary ? Deco.GoldFace : Deco.EnamelFace, Color.white, size, Vector2.zero, true);
            b.sheen = UiKit.Image("Sheen", root, Deco.Mask, new Color(1f, 0.97f, 0.88f, 0f), size - new Vector2(4, 4));
            b.Bg = bg;
            b.OnClick = onClick;
            b.Label = UiKit.Text("Label", root, text, fontSize, primary ? Palette.Hex(0x3A230C) : Palette.Cream, UiKit.Signage, TextAlignmentOptions.Center, size);
            b.Label.characterSpacing = 3f;
            if (!primary) Deco.Shadowed(b.Label, 0.8f, 0.7f, 0.2f);
            b.labelCol = b.Label.color;
            return b;
        }

        /// <summary>Make an existing graphic (a card) behave like a button: hover glow, lift and click.</summary>
        public static UiButton Attach(RectTransform root, Image face, Action onClick)
        {
            var b = root.gameObject.AddComponent<UiButton>();
            b.Bg = face;
            face.raycastTarget = true;
            b.OnClick = onClick;
            b.baseCol = face.color;
            b.hoverCol = face.color;
            b.glow = UiKit.Image("Glow", root, Deco.Shadow, new Color(1f, 0.78f, 0.35f, 0f), root.sizeDelta + new Vector2(80, 80));
            b.glow.transform.SetAsFirstSibling();
            return b;
        }

        public void SetText(string t) => Label.text = t;

        public void SetColors(Color normal, Color hovered)
        {
            baseCol = normal;
            hoverCol = hovered;
            Bg.color = normal;
        }

        public void SetInteractable(bool on)
        {
            Interactable = on;
            Bg.color = on ? baseCol : new Color(0.5f, 0.47f, 0.45f, 0.8f);
            if (Label) Label.alpha = on ? 1f : 0.45f;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Interactable) return;
            hover = 1f;
            AudioDirector.Instance?.Sfx("ui_hover", 0.5f, 1f, 0f, 0.02f, 0.05f);
        }

        public void OnPointerExit(PointerEventData e) { hover = 0f; press = 0f; }
        public void OnPointerDown(PointerEventData e) { if (Interactable) press = 1f; }
        public void OnPointerUp(PointerEventData e) => press = 0f;

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            Click();
        }

        public void Click()
        {
            punch = 1f;
            AudioDirector.Instance?.Sfx("ui_click", 0.8f);
            OnClick?.Invoke();
        }

        void Start()
        {
            if (Label) labelY = Label.rectTransform.anchoredPosition.y;
        }

        void Update()
        {
            float dt = UiTime.Dt;
            t += dt;
            punch = Mathf.Max(0f, punch - dt * 4f);
            hoverShown = Mathf.Lerp(hoverShown, Interactable ? hover : 0f, Ease.Damp(16f, dt));
            bool focus = UiNav.Driving ? NavFocus : Focused;
            focusShown = Mathf.Lerp(focusShown, focus && Interactable ? (UiNav.Driving ? 1.6f : 1f) : 0f, Ease.Damp(UiNav.Driving ? 16f : 8f, dt));
            float h = Mathf.Max(hoverShown, focusShown * 0.6f);
            var target = Vector3.one * (1f + 0.035f * h - 0.04f * press + 0.06f * Mathf.Sin(punch * Mathf.PI));
            transform.localScale = Vector3.Lerp(transform.localScale, target, Ease.Damp(18f, dt));
            if (Interactable) Bg.color = Color.Lerp(Bg.color, Color.Lerp(baseCol, hoverCol, hoverShown), Ease.Damp(14f, dt));
            if (sheen) sheen.color = new Color(1f, 0.97f, 0.88f, 0.13f * hoverShown + 0.08f * press);
            if (glow) glow.color = new Color(1f, 0.78f, 0.35f, Mathf.Max(0.55f * hoverShown, focusShown * (0.45f + 0.15f * Mathf.Sin(t * 3f))));
            if (Label && sheen) Label.rectTransform.anchoredPosition = new Vector2(0, labelY - 2f * press);
        }
    }

    /// <summary>Horizontal slider 0..1: a recessed track, a gold fill and a brass knob.</summary>
    public sealed class UiSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action<float> Changed;
        public float Value;
        public bool NavFocus;
        Image focusGlow;
        RectTransform track;
        Image fill, knob;
        TextMeshProUGUI readout;
        const float W = 380f;
        float hover;

        public static UiSlider Create(Transform parent, string label, Vector2 pos, float value, Action<float> changed)
        {
            var root = UiKit.Rect("Slider_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(720, 70));
            var focusGlow = UiKit.Image("Focus", root, Deco.Shadow, new Color(1f, 0.78f, 0.35f, 0f), new Vector2(800, 110));
            Deco.Label("Label", root, label, 26, new Vector2(240, 50), new Vector2(-240, 0), TextAlignmentOptions.Left, Palette.Cream);
            var bg = UiKit.Image("Track", root, Deco.Recess, Color.white, new Vector2(W, 20), new Vector2(80, 0), true);
            var hit = UiKit.Image("Hit", bg.transform, null, new Color(0, 0, 0, 0), new Vector2(W + 40, 60), Vector2.zero, true);
            var s = bg.gameObject.AddComponent<UiSlider>();
            s.track = bg.rectTransform;
            s.fill = UiKit.Image("Fill", bg.transform, Deco.GoldPill, Color.white, new Vector2(W, 14));
            s.fill.rectTransform.pivot = new Vector2(0, 0.5f);
            s.fill.rectTransform.anchoredPosition = new Vector2(-W * 0.5f + 3f, 0);
            s.knob = UiKit.Image("Knob", bg.transform, Deco.Knob, Color.white, new Vector2(40, 40));
            UiKit.Image("KnobShadow", s.knob.transform, Deco.Shadow, new Color(1, 1, 1, 0.8f), new Vector2(64, 64), new Vector2(0, -5)).transform.SetAsFirstSibling();
            s.readout = UiKit.Text("Value", root, "", 24, Deco.Muted, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(80, 40), new Vector2(330, 0));
            hit.transform.SetAsFirstSibling();
            s.Changed = changed;
            s.focusGlow = focusGlow;
            s.Set(value, false);
            return s;
        }

        public void Set(float v, bool notify = true)
        {
            Value = Mathf.Clamp01(v);
            fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(14f, (W - 6f) * Value), 14f);
            knob.rectTransform.anchoredPosition = new Vector2(-W * 0.5f + W * Value, 0);
            readout.text = Mathf.RoundToInt(Value * 100f).ToString();
            if (notify) Changed?.Invoke(Value);
        }

        void SetFromPointer(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local);
            Set((local.x + W * 0.5f) / W);
        }

        public void OnPointerDown(PointerEventData e) { SetFromPointer(e); AudioDirector.Instance?.Sfx("ui_click", 0.5f); }
        public void OnDrag(PointerEventData e) => SetFromPointer(e);
        public void OnPointerEnter(PointerEventData e) => hover = 1f;
        public void OnPointerExit(PointerEventData e) => hover = 0f;

        void Update()
        {
            float f = NavFocus ? 1f : 0f;
            float s = Mathf.Lerp(knob.rectTransform.localScale.x, 1f + 0.12f * Mathf.Max(hover, f), Ease.Damp(14f, UiTime.Dt));
            knob.rectTransform.localScale = Vector3.one * s;
            focusGlow.color = Color.Lerp(focusGlow.color, new Color(1f, 0.78f, 0.35f, 0.32f * f), Ease.Damp(14f, UiTime.Dt));
        }
    }

    /// <summary>On/off switch: a recessed slot with a brass knob, gold when on.</summary>
    public sealed class UiToggle : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        float hover;
        public void OnPointerEnter(PointerEventData e) => hover = 1f;
        public void OnPointerExit(PointerEventData e) => hover = 0f;
        public Action<bool> Changed;
        public bool On;
        public bool NavFocus;
        Image fill, knob, focusGlow;
        TextMeshProUGUI state;
        float shown;

        public static UiToggle Create(Transform parent, string label, Vector2 pos, bool on, Action<bool> changed)
        {
            var root = UiKit.Rect("Toggle_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(720, 64));
            Deco.Label("Label", root, label, 26, new Vector2(480, 50), new Vector2(-120, 0), TextAlignmentOptions.Left, Palette.Cream);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var focusGlow = UiKit.Image("Focus", root, Deco.Shadow, new Color(1f, 0.78f, 0.35f, 0f), new Vector2(800, 104));
            focusGlow.transform.SetAsFirstSibling();
            var bg = UiKit.Image("Switch", root, Deco.Recess, Color.white, new Vector2(104, 46), new Vector2(278, 0));
            var t = root.gameObject.AddComponent<UiToggle>();
            t.fill = UiKit.Image("Fill", bg.transform, Deco.GoldPill, Color.white, new Vector2(96, 38));
            t.knob = UiKit.Image("Knob", bg.transform, Deco.Knob, Color.white, new Vector2(40, 40));
            UiKit.Image("KnobShadow", t.knob.transform, Deco.Shadow, new Color(1, 1, 1, 0.8f), new Vector2(64, 64), new Vector2(0, -5)).transform.SetAsFirstSibling();
            t.state = UiKit.Text("State", root, "", 20, Deco.Muted, UiKit.Signage, TextAlignmentOptions.Right, new Vector2(80, 40), new Vector2(180, 0));
            t.Changed = changed;
            t.focusGlow = focusGlow;
            t.Set(on, false);
            t.shown = on ? 1f : 0f;
            t.Apply();
            return t;
        }

        public void Set(bool on, bool notify = true)
        {
            On = on;
            state.text = on ? "ON" : "OFF";
            state.color = on ? Deco.Gold : Deco.Muted;
            if (notify) Changed?.Invoke(on);
        }

        void Apply()
        {
            float e = Ease.OutCubic(shown);
            fill.color = new Color(1, 1, 1, e);
            knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-29f, 29f, e), 0);
        }

        void Update()
        {
            shown = Mathf.MoveTowards(shown, On ? 1f : 0f, UiTime.Dt * 6f);
            Apply();
            float f = NavFocus ? 1f : 0f;
            float s = Mathf.Lerp(knob.rectTransform.localScale.x, 1f + 0.12f * Mathf.Max(hover, f), Ease.Damp(14f, UiTime.Dt));
            knob.rectTransform.localScale = Vector3.one * s;
            focusGlow.color = Color.Lerp(focusGlow.color, new Color(1f, 0.78f, 0.35f, 0.32f * f), Ease.Damp(14f, UiTime.Dt));
        }

        public void OnPointerClick(PointerEventData e)
        {
            Set(!On);
            AudioDirector.Instance?.Sfx("ui_click", 0.7f);
        }
    }

    /// <summary>Base for full-screen menus: fade + slide in/out, Escape shortcut (Enter and pad via UiNav).</summary>
    public abstract class UiScreen : MonoBehaviour
    {
        /// <summary>Every screen, for <see cref="UiNav"/>.</summary>
        public static readonly System.Collections.Generic.List<UiScreen> All = new System.Collections.Generic.List<UiScreen>();
        protected RectTransform Root;
        protected CanvasGroup Group;
        float show, target;
        public bool Visible => target > 0.5f;
        public Action Primary, Back;

        protected void Init(string name)
        {
            Root = (RectTransform)transform;
            if (!All.Contains(this)) All.Add(this);
            Group = gameObject.AddComponent<CanvasGroup>();
            Group.alpha = 0f;
            Group.blocksRaycasts = false;
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
            target = 1f;
            Group.blocksRaycasts = true;
            transform.SetAsLastSibling();
            AudioDirector.Instance?.Sfx("ui_whoosh", 0.4f);
        }

        public virtual void Hide()
        {
            target = 0f;
            Group.blocksRaycasts = false;
        }

        protected virtual void Update()
        {
            float dt = UiTime.Dt;
            show = Mathf.MoveTowards(show, target, dt * 4f);
            Group.alpha = Ease.OutCubic(show);
            Root.anchoredPosition = new Vector2(0, -30f * (1f - Ease.OutCubic(show)));
            if (show <= 0f && target <= 0f) { gameObject.SetActive(false); return; }
            if (target < 0.5f) return;
            // Enter / A and B are handled by UiNav (it knows about the focus ring)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.escapeKey.wasPressedThisFrame && Back != null) { AudioDirector.Instance?.Sfx("ui_back", 0.8f); Back(); }
        }

        protected static Image Dim(Transform parent, float alpha = 0.55f) => Deco.Dim(parent, alpha);

        /// <summary>A framed enamel card. Returns the card rect (children are centred on it).</summary>
        protected static RectTransform Card(Transform parent, Vector2 size, Vector2 pos) => Deco.Panel("Card", parent, size, pos);

        /// <summary>A card heading: gilded title over a brass rule.</summary>
        protected static TextMeshProUGUI Heading(Transform card, string text, float y, float width, float size = 76f)
        {
            var t = Deco.Gilded(UiKit.Text("Heading", card, text, size, Color.white, UiKit.Display, TextAlignmentOptions.Center, new Vector2(width, size * 1.3f), new Vector2(0, y)));
            Deco.Divider(card, width * 0.62f, new Vector2(0, y - size * 0.66f));
            return t;
        }
    }
}
