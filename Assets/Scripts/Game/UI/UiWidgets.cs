using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneMoreFloor
{
    /// <summary>A brass pill button with hover lift, press punch and sounds.</summary>
    public sealed class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Action OnClick;
        public Image Bg;
        public TextMeshProUGUI Label;
        public bool Interactable = true;
        public bool Focused;
        Color baseCol, hoverCol;
        float hover, press, punch;

        public static UiButton Create(Transform parent, string text, Vector2 pos, Vector2 size, Action onClick, bool primary = false, float fontSize = 34f)
        {
            var bg = UiKit.Image("Btn_" + text, parent, UiKit.Pill, primary ? Palette.Hex(0xFFC857) : Palette.Hex(0xE9D3A3), size, pos, true);
            var shadow = UiKit.Image("Shadow", bg.transform, UiKit.Pill, new Color(0, 0, 0, 0.35f), size, new Vector2(0, -6));
            shadow.transform.SetAsFirstSibling();
            var b = bg.gameObject.AddComponent<UiButton>();
            b.Bg = bg;
            b.OnClick = onClick;
            b.baseCol = bg.color;
            b.hoverCol = Color.Lerp(bg.color, Color.white, 0.35f);
            b.Label = UiKit.Text("Label", bg.transform, text, fontSize, Palette.Ink, UiKit.Signage, TextAlignmentOptions.Center, size);
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
            Bg.color = on ? baseCol : new Color(0.45f, 0.42f, 0.4f, 0.7f);
            Label.alpha = on ? 1f : 0.5f;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Interactable) return;
            hover = 1f;
            AudioDirector.Instance?.Sfx("ui_hover", 0.5f, 1f, 0f, 0.02f, 0.05f);
        }

        public void OnPointerExit(PointerEventData e) => hover = 0f;
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

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            punch = Mathf.Max(0f, punch - dt * 4f);
            float h = Mathf.Max(hover, Focused ? 0.7f : 0f);
            var target = Vector3.one * (1f + 0.06f * h - 0.05f * press + 0.08f * Mathf.Sin(punch * Mathf.PI));
            transform.localScale = Vector3.Lerp(transform.localScale, target, Ease.Damp(18f, dt));
            if (Interactable) Bg.color = Color.Lerp(Bg.color, Color.Lerp(baseCol, hoverCol, h), Ease.Damp(14f, dt));
        }
    }

    /// <summary>Horizontal slider 0..1 with a brass knob.</summary>
    public sealed class UiSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<float> Changed;
        public float Value;
        RectTransform track;
        Image fill, knob;

        public static UiSlider Create(Transform parent, string label, Vector2 pos, float value, Action<float> changed)
        {
            var root = UiKit.Rect("Slider_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(720, 70));
            UiKit.Text("Label", root, label, 30, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(260, 50), new Vector2(-230, 0));
            var bg = UiKit.Image("Track", root, UiKit.Pill, new Color(1, 1, 1, 0.15f), new Vector2(400, 22), new Vector2(130, 0), true);
            var s = bg.gameObject.AddComponent<UiSlider>();
            s.track = bg.rectTransform;
            s.fill = UiKit.Image("Fill", bg.transform, UiKit.Pill, Palette.Hex(0xFFC857), new Vector2(400, 22));
            s.fill.rectTransform.pivot = new Vector2(0, 0.5f);
            s.fill.rectTransform.anchoredPosition = new Vector2(-200, 0);
            s.knob = UiKit.Image("Knob", bg.transform, UiKit.Circle, Palette.Cream, new Vector2(44, 44));
            s.Changed = changed;
            s.Set(value, false);
            return s;
        }

        public void Set(float v, bool notify = true)
        {
            Value = Mathf.Clamp01(v);
            fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(22f, 400f * Value), 22f);
            knob.rectTransform.anchoredPosition = new Vector2(-200f + 400f * Value, 0);
            if (notify) Changed?.Invoke(Value);
        }

        void SetFromPointer(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local);
            Set((local.x + 200f) / 400f);
        }

        public void OnPointerDown(PointerEventData e) { SetFromPointer(e); AudioDirector.Instance?.Sfx("ui_click", 0.5f); }
        public void OnDrag(PointerEventData e) => SetFromPointer(e);
    }

    /// <summary>On/off pill switch.</summary>
    public sealed class UiToggle : MonoBehaviour, IPointerClickHandler
    {
        public Action<bool> Changed;
        public bool On;
        Image bg, knob;

        public static UiToggle Create(Transform parent, string label, Vector2 pos, bool on, Action<bool> changed)
        {
            var root = UiKit.Rect("Toggle_" + label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(720, 70));
            UiKit.Text("Label", root, label, 30, Palette.Cream, UiKit.Signage, TextAlignmentOptions.Left, new Vector2(420, 50), new Vector2(-150, 0));
            var bg = UiKit.Image("Switch", root, UiKit.Pill, Palette.Brass, new Vector2(110, 52), new Vector2(270, 0), true);
            var t = bg.gameObject.AddComponent<UiToggle>();
            t.bg = bg;
            t.knob = UiKit.Image("Knob", bg.transform, UiKit.Circle, Palette.Cream, new Vector2(42, 42));
            t.Changed = changed;
            t.Set(on, false);
            return t;
        }

        public void Set(bool on, bool notify = true)
        {
            On = on;
            bg.color = on ? Palette.Hex(0x6CCB5F) : new Color(1, 1, 1, 0.18f);
            knob.rectTransform.anchoredPosition = new Vector2(on ? 28 : -28, 0);
            if (notify) Changed?.Invoke(on);
        }

        public void OnPointerClick(PointerEventData e)
        {
            Set(!On);
            AudioDirector.Instance?.Sfx("ui_click", 0.7f);
        }
    }

    /// <summary>Base for full-screen menus: fade + slide in/out, Enter/Escape shortcuts.</summary>
    public abstract class UiScreen : MonoBehaviour
    {
        protected RectTransform Root;
        protected CanvasGroup Group;
        float show, target;
        public bool Visible => target > 0.5f;
        public Action Primary, Back;

        protected void Init(string name)
        {
            Root = (RectTransform)transform;
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
            float dt = Time.unscaledDeltaTime;
            show = Mathf.MoveTowards(show, target, dt * 4f);
            Group.alpha = Ease.OutCubic(show);
            Root.anchoredPosition = new Vector2(0, -40f * (1f - Ease.OutCubic(show)));
            if (show <= 0f && target <= 0f) { gameObject.SetActive(false); return; }
            if (target < 0.5f) return;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && Primary != null) { AudioDirector.Instance?.Sfx("ui_click", 0.8f); Primary(); }
            else if (kb.escapeKey.wasPressedThisFrame && Back != null) { AudioDirector.Instance?.Sfx("ui_back", 0.8f); Back(); }
        }

        protected static Image Dim(Transform parent, float alpha = 0.55f)
        {
            var rt = UiKit.Stretch("Dim", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.06f, 0.04f, 0.08f, alpha);
            return img;
        }

        protected static RectTransform Card(Transform parent, Vector2 size, Vector2 pos, Color? color = null)
        {
            var rim = UiKit.Image("Card", parent, UiKit.Rounded, Palette.Brass, size + new Vector2(14, 14), pos);
            var inner = UiKit.Image("Inner", rim.transform, UiKit.Rounded, color ?? Palette.Hex(0x2A1E2E), size);
            return inner.rectTransform;
        }
    }
}
