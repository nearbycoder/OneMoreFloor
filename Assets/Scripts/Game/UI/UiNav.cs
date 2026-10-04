using System.Collections.Generic;
using UnityEngine;

namespace OneMoreFloor
{
    /// <summary>
    /// Gamepad and arrow-key navigation for the menus. Each frame it looks at the topmost visible screen,
    /// moves a focus ring between that screen's buttons, sliders and toggles by their on-screen positions,
    /// and activates the focused one on A / Enter. Left/right adjusts a focused slider or toggle. B goes back.
    /// With the mouse in use the screens keep their own default-button glow, and Enter runs that default.
    /// </summary>
    public sealed class UiNav : MonoBehaviour
    {
        /// <summary>True while the focus ring is showing (driven by pad or keys).</summary>
        public static bool Driving { get; private set; }
        /// <summary>The widget with the focus ring (null when the mouse is in charge).</summary>
        public static Component Focus { get; private set; }

        UiScreen screen;
        Component selected;
        bool wasDriving;
        readonly List<Component> widgets = new List<Component>();
        Camera uiCam;

        void LateUpdate()
        {
            var top = TopScreen();
            if (top != screen)
            {
                screen = top;
                selected = null;
            }
            bool prevDriving = wasDriving;
            Driving = false;
            Focus = null;
            wasDriving = false;
            if (screen == null) return;

            Collect(screen);
            if (selected != null && !widgets.Contains(selected)) selected = null;
            if (selected == null) selected = Default();
            Driving = Controls.KeyNav && selected != null;
            wasDriving = Driving;
            if (Driving) Focus = selected;
            foreach (var w in widgets) SetFocus(w, Driving && w == selected);

            if (Controls.Cancel && screen.Back != null)
            {
                AudioDirector.Instance?.Sfx("ui_back", 0.8f);
                screen.Back();
                return;
            }
            if (Controls.Submit)
            {
                if (Driving) Activate(selected);
                else if (screen.Primary != null) { AudioDirector.Instance?.Sfx("ui_click", 0.8f); screen.Primary(); }
                return;
            }
            if (Controls.NavX == 0 && Controls.NavY == 0 || selected == null) return;
            if (!Driving || !prevDriving) return;   // the first press only shows the ring
            if (Controls.NavX != 0 && Adjust(selected, Controls.NavX)) return;
            var next = Neighbour(selected, new Vector2(Controls.NavX, Controls.NavY));
            if (next != null)
            {
                selected = next;
                AudioDirector.Instance?.Sfx("ui_hover", 0.5f, 1f, 0f, 0.02f, 0.05f);
            }
        }

        static UiScreen TopScreen()
        {
            UiScreen best = null;
            int bestIndex = -1;
            foreach (var s in UiScreen.All)
            {
                if (s == null || !s.Visible || !s.isActiveAndEnabled) continue;
                int i = s.transform.GetSiblingIndex();
                if (i > bestIndex) { bestIndex = i; best = s; }
            }
            return best;
        }

        void Collect(UiScreen s)
        {
            widgets.Clear();
            foreach (var b in s.GetComponentsInChildren<UiButton>()) if (b.Interactable && b.isActiveAndEnabled) widgets.Add(b);
            foreach (var sl in s.GetComponentsInChildren<UiSlider>()) if (sl.isActiveAndEnabled) widgets.Add(sl);
            foreach (var t in s.GetComponentsInChildren<UiToggle>()) if (t.isActiveAndEnabled) widgets.Add(t);
        }

        Component Default()
        {
            foreach (var w in widgets) if (w is UiButton b && b.Focused) return w;
            Component best = null;
            float bestY = float.MinValue;
            foreach (var w in widgets)
            {
                var p = ScreenPos(w);
                if (p.y > bestY + 1f) { bestY = p.y; best = w; }
            }
            return best;
        }

        Vector2 ScreenPos(Component w)
        {
            if (!uiCam) { var canvas = w.GetComponentInParent<Canvas>(); uiCam = canvas ? canvas.rootCanvas.worldCamera : null; }
            var rt = (RectTransform)(w is UiSlider ? w.transform.parent : w.transform);
            return RectTransformUtility.WorldToScreenPoint(uiCam, rt.TransformPoint(rt.rect.center));
        }

        /// <summary>The nearest widget in a direction, favouring ones straight ahead over ones off to the side.</summary>
        Component Neighbour(Component from, Vector2 dir)
        {
            var p = ScreenPos(from);
            Component best = null;
            float bestScore = float.MaxValue;
            foreach (var w in widgets)
            {
                if (w == from) continue;
                var d = ScreenPos(w) - p;
                float along = Vector2.Dot(d, dir);
                if (along < 8f) continue;
                float side = Mathf.Abs(dir.x != 0 ? d.y : d.x);
                float score = along + side * 2.2f;
                if (score < bestScore) { bestScore = score; best = w; }
            }
            return best;
        }

        static bool Adjust(Component w, int dx)
        {
            if (w is UiSlider s)
            {
                s.Set(Mathf.Round((s.Value + dx * 0.05f) * 20f) / 20f);
                AudioDirector.Instance?.Sfx("ui_click", 0.4f, 1f + s.Value * 0.3f);
                return true;
            }
            if (w is UiToggle t && (dx > 0) != t.On)
            {
                t.Set(dx > 0);
                AudioDirector.Instance?.Sfx("ui_click", 0.7f);
                return true;
            }
            return false;
        }

        static void Activate(Component w)
        {
            switch (w)
            {
                case UiButton b: if (b.Interactable) b.Click(); break;
                case UiToggle t: t.Set(!t.On); AudioDirector.Instance?.Sfx("ui_click", 0.7f); break;
            }
        }

        static void SetFocus(Component w, bool on)
        {
            switch (w)
            {
                case UiButton b: b.NavFocus = on; break;
                case UiSlider s: s.NavFocus = on; break;
                case UiToggle t: t.NavFocus = on; break;
            }
        }
    }
}
