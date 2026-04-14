// GlideTimeline.cs
using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlideRail
{
    /// <summary>
    /// Poziomy timeline keyframe'ów z scrollem, zoomem (Ctrl+Scroll)
    /// i inspektorem KF.
    /// </summary>
    public class GlideTimeline
    {
        private readonly GlideRailSession _session;
        private readonly UIPanel _parentPanel;

        // ── Layout ────────────────────────────────────────────────────────────
        private const float ROW_H = 26f;   // wysokość rzędu X i rzędu KF
        private const float TILE_W = 36f;   // szerokość kafelka przy zoom=1
        private const float SCROLLBAR_H = 14f;
        private const float SEP_H = 2f;

        // ── Stan ──────────────────────────────────────────────────────────────
        private float _scrollX = 0f;   // offset w px
        private int _activeKf = -1;   // podświetlony KF podczas playback
        private bool _ctrlHeld = false;

        // ── Inspektor ─────────────────────────────────────────────────────────
        private UIPanel _inspector = null;
        private int _inspectorKf = -1;

        private readonly List<UIPanel> _inspectors = new();

        // ── IL2CPP ptrs (kontenery wewnątrz panelu) ───────────────────────────
        private IntPtr _xRowPtr = IntPtr.Zero;
        private IntPtr _kfRowPtr = IntPtr.Zero;
        private IntPtr _scrollbarPtr = IntPtr.Zero;
        private IntPtr _scrollThumb = IntPtr.Zero;

        // ── Kolory ────────────────────────────────────────────────────────────
        private static readonly Color C_TILE_NORM = new Color(0.12f, 0.18f, 0.30f, 1f);
        private static readonly Color C_TILE_ACT = new Color(0.20f, 0.60f, 0.90f, 1f);
        private static readonly Color C_TILE_GROUP = new Color(0.18f, 0.14f, 0.28f, 1f);
        private static readonly Color C_X_BTN = new Color(0.38f, 0.07f, 0.07f, 1f);
        private static readonly Color C_SCROLL_BG = new Color(0.08f, 0.10f, 0.15f, 1f);
        private static readonly Color C_SCROLL_TH = new Color(0.30f, 0.45f, 0.65f, 1f);
        private static readonly Color C_SEP = new Color(0.20f, 0.30f, 0.50f, 0.5f);
        private static readonly Color C_INSP_BG = new Color(0.06f, 0.08f, 0.14f, 0.98f);
        private static readonly Color C_INSP_BRD = new Color(0.28f, 0.48f, 0.90f, 0.70f);

        private static readonly Color KF_CYAN = new Color(0.07f, 0.22f, 0.40f, 1f);
        private static readonly Color KF_MAG = new Color(0.26f, 0.08f, 0.30f, 1f);

        // ═════════════════════════════════════════════════════════════════════

        public GlideTimeline(GlideRailSession session, UIPanel parentPanel)
        {
            _session = session;
            _parentPanel = parentPanel;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Buduje timeline jako overlay w parentPanel.</summary>
        public void Build(float panelW, float timelineTop)
        {
            BuildXRow(panelW, timelineTop);
            BuildKfRow(panelW, timelineTop + ROW_H + SEP_H);
            BuildScrollbar(panelW, timelineTop + ROW_H * 2 + SEP_H * 2);
            RefreshTiles();
        }

        /// <summary>Odśwież po zmianie keyframe'ów.</summary>
        public void Refresh()
        {
            RefreshTiles();
            RefreshScrollbar();
        }

        /// <summary>Podświetl aktualny KF podczas playback.</summary>
        public void SetActiveKf(int index)
        {
            _activeKf = index;
            RefreshTiles();
        }

        /// <summary>Obsługa Ctrl+Scroll z OnUpdate sesji.</summary>
        public void OnUpdate()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            int kfCount = _session.Keyframes.Count;
            float totalW = kfCount * (TileWidth() + 2f);
            float panelW = Screen.width - 20f;
            float maxScroll = Mathf.Max(0f, totalW - panelW);

            _scrollX = Mathf.Clamp(
                _scrollX - scroll * TileWidth() * 3f, 0f, maxScroll);
            RefreshTiles();
            RefreshScrollbar();
        }

        // ═════════════════════════════════════════════════════════════════════
        // BUDOWA
        // ═════════════════════════════════════════════════════════════════════

        private void BuildXRow(float panelW, float top)
        {
            var ve = UIRuntime.NewVE();
            var s = UIRuntime.GetStyle(ve);
            S.Position(s, "Absolute");
            S.Left(s, 0f); S.Top(s, top);
            S.Width(s, panelW); S.Height(s, ROW_H);
            S.Overflow(s, "Hidden");
            _parentPanel.AddOverlayToPanel(ve);
            _xRowPtr = UIRuntime.GetPtr(ve);
        }

        private void BuildKfRow(float panelW, float top)
        {
            var ve = UIRuntime.NewVE();
            var s = UIRuntime.GetStyle(ve);
            S.Position(s, "Absolute");
            S.Left(s, 0f); S.Top(s, top);
            S.Width(s, panelW); S.Height(s, ROW_H);
            S.Overflow(s, "Hidden");
            _parentPanel.AddOverlayToPanel(ve);
            _kfRowPtr = UIRuntime.GetPtr(ve);
        }

        private void BuildScrollbar(float panelW, float top)
        {
            var track = UIRuntime.NewVE();
            var ts = UIRuntime.GetStyle(track);
            S.Position(ts, "Absolute");
            S.Left(ts, 0f); S.Top(ts, top);
            S.Width(ts, panelW); S.Height(ts, SCROLLBAR_H);
            S.BgColor(ts, new Color(0f, 0f, 0f, 0f)); // przezroczysty
            _parentPanel.AddOverlayToPanel(track);
            _scrollbarPtr = UIRuntime.GetPtr(track);

            // Thumb też przezroczysty — scroll działa ale nie widać
            var thumb = UIRuntime.NewVE();
            var ths = UIRuntime.GetStyle(thumb);
            S.Position(ths, "Absolute");
            S.Top(ths, 2f); S.Height(ths, SCROLLBAR_H - 4f);
            S.BgColor(ths, new Color(0f, 0f, 0f, 0f));
            UIRuntime.AddChild(track, thumb);
            _scrollThumb = UIRuntime.GetPtr(thumb);
        }

        // ═════════════════════════════════════════════════════════════════════
        // REFRESH
        // ═════════════════════════════════════════════════════════════════════

        private void RefreshTiles()
        {
            if (_xRowPtr == IntPtr.Zero || _kfRowPtr == IntPtr.Zero) return;

            ClearVE(_xRowPtr);
            ClearVE(_kfRowPtr);

            int kfCount = _session.Keyframes.Count;
            if (kfCount == 0) return;

            float tileW = TileWidth();
            float x = -_scrollX;

            for (int i = 0; i < kfCount; i++)
            {
                int capI = i;
                float tf = kfCount > 1 ? (float)i / (kfCount - 1) : 0f;
                Color tileCol = i == _activeKf
                    ? C_TILE_ACT
                    : Color.Lerp(KF_CYAN, KF_MAG, tf);

                // ── X button ─────────────────────────────────────────────
                AddTileButton(_xRowPtr, "✕", x, tileW, ROW_H - 2f,
                    C_X_BTN, () => _session.RemoveKeyframe(capI));

                // ── KF button ─────────────────────────────────────────────
                AddTileButton(_kfRowPtr, $"#{i + 1}", x, tileW, ROW_H - 2f,
                    tileCol, () => ShowInspector(capI));

                x += tileW + 2f;
            }
        }

        private void RefreshScrollbar()
        {
            if (_scrollThumb == IntPtr.Zero) return;

            float panelW = Screen.width - 20f;
            float tileW = TileWidth();
            int kfCount = _session.Keyframes.Count;
            float totalW = kfCount * (tileW + 2f);
            float ratio = totalW > 0 ? Mathf.Clamp01(panelW / totalW) : 1f;
            float thumbW = Mathf.Max(24f, panelW * ratio);
            float maxScroll = Mathf.Max(0f, totalW - panelW);
            float thumbX = maxScroll > 0
                ? (_scrollX / maxScroll) * (panelW - thumbW)
                : 0f;

            var st = UIRuntime.GetStyle(UIRuntime.WrapVE(_scrollThumb));
            S.Left(st, thumbX);
            S.Width(st, thumbW);
        }

        // ═════════════════════════════════════════════════════════════════════
        // INSPEKTOR
        // ═════════════════════════════════════════════════════════════════════

        private void ShowInspector(int kfIndex)
        {
            // Zamknij wszystkie otwarte
            CloseInspector();

            if (kfIndex < 0 || kfIndex >= _session.Keyframes.Count) return;

            _inspectorKf = kfIndex;
            var kf = _session.Keyframes[kfIndex];

            const float W = 260f;
            const float H = 180f;
            float x = (Screen.width - W) / 2f;
            float y = Screen.height - 200f - H - 20f;

            string panelName = $"GlideInspector_{kfIndex}";
            var p = UIPanel.Create(panelName, x, y, W, H);

            // Capture panelName for close button
            string capName = panelName;
            p.AddTitleButton("✕", () =>
            {
                FrameworkAPI.DestroyPanel(capName);
                _inspectors.RemoveAll(ip => ip == null);
            }, new Color(0.44f, 0.08f, 0.08f, 1f));

            p.Build(850);

            var ve = UIRuntime.WrapVE(p.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, C_INSP_BG);
            S.BorderRadius(st, 8f);
            S.BorderColor(st, C_INSP_BRD);
            S.BorderWidth(st, 1.5f);

            p.AddSpace(4f);
            var titleLbl = p.AddRow(18f, 0f)
                .AddLabel($"  KF #{kfIndex + 1}", W - 30f,
                    new Color(0.70f, 0.85f, 1.00f, 1f));
            titleLbl.SetFontSize(12);

            p.AddSeparator();
            p.AddSpace(2f);

            var posRow = p.AddRow(16f, 2f);
            posRow.AddLabel(
                $"  Pos: {kf.Position.x:F1}, {kf.Position.y:F1}, {kf.Position.z:F1}",
                W - 10f, new Color(0.75f, 0.78f, 0.85f, 1f))
                .SetFontSize(10);

            p.AddSpace(4f);

            var spdRow = p.AddRow(24f, 4f);
            spdRow.AddLabel("  Speed ×", 68f,
                new Color(0.60f, 0.65f, 0.75f, 1f)).SetFontSize(10);

            var spdLbl = spdRow.AddLabel(
                $"{kf.SpeedMultiplier:F1}", 34f,
                new Color(1.00f, 0.84f, 0.28f, 1f));
            spdLbl.SetFontSize(11);

            spdRow.AddButton("−", 22f, () =>
            {
                _session.SetKeyframeSpeed(kfIndex,
                    Mathf.Max(0.1f, _session.Keyframes[kfIndex].SpeedMultiplier - 0.1f));
                spdLbl.SetText($"{_session.Keyframes[kfIndex].SpeedMultiplier:F1}");
            }, new Color(0.26f, 0.09f, 0.09f, 1f));

            spdRow.AddButton("+", 22f, () =>
            {
                _session.SetKeyframeSpeed(kfIndex,
                    Mathf.Min(5f, _session.Keyframes[kfIndex].SpeedMultiplier + 0.1f));
                spdLbl.SetText($"{_session.Keyframes[kfIndex].SpeedMultiplier:F1}");
            }, new Color(0.09f, 0.26f, 0.09f, 1f));

            p.AddSpace(6f);
            p.AddSeparator();
            p.AddSpace(4f);

            var btnRow = p.AddRow(26f, 4f);

            btnRow.AddButton("Skocz", 72f, () =>
            {
                _session.JumpToKeyframe(kfIndex);
                FrameworkAPI.DestroyPanel(capName);
            }, new Color(0.10f, 0.18f, 0.38f, 1f));

            btnRow.AddButton("Podmień", 72f, () =>
            {
                _session.ReplaceKeyframe(kfIndex);
                FrameworkAPI.DestroyPanel(capName);
            }, new Color(0.10f, 0.28f, 0.18f, 1f));

            btnRow.AddButton("Usuń", 60f, () =>
            {
                _session.RemoveKeyframe(kfIndex);
                FrameworkAPI.DestroyPanel(capName);
            }, new Color(0.38f, 0.07f, 0.07f, 1f));

            _inspectors.Add(p);
            _inspector = p;  // zachowaj dla kompatybilności
        }

        public void CloseInspector()
        {
            foreach (var ip in _inspectors)
            {
                if (ip != null)
                    FrameworkAPI.DestroyPanel(ip.Title);
            }
            _inspectors.Clear();
            _inspector = null;
            _inspectorKf = -1;
        }

        // ═════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private float TileWidth() => 43f;

        
        private void AddTileButton(IntPtr containerPtr, string label,
            float x, float w, float h, Color bg, Action onClick)
        {
            var btn = _parentPanel.AddButtonToContainer(
                UIRuntime.WrapVE(containerPtr),
                label, x, 1f, w, h, bg, onClick);

            var st = UIRuntime.GetStyle(UIRuntime.WrapVE(btn));
            S.FontSize(st, 9);
            S.BorderRadius(st, 3f);
        }

        private static void ClearVE(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return;
            var ve = UIRuntime.WrapVE(ptr);
            UIRuntime.VisualElementType.GetMethod("Clear")
                ?.Invoke(ve, null);
        }
    }
}