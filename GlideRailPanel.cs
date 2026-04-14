// GlideRailPanel.cs
using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace GlideRail
{
    public class GlideRailPanel
    {
        private readonly GlideRailSession _session;

        private UIPanel _panel;
        private bool _rebuildPending = false;
        private bool _panelVisible = false;

        // ── Live handles ──────────────────────────────────────────────────────
        private UILabelHandle _lblHint;
        private UILabelHandle _lblKfCount;
        private UILabelHandle _lblDurVal;
        private UILabelHandle _lblSpdVal;
        private UILabelHandle _lblSnsVal;
        private UILabelHandle _lblProgress;

        // Przycisk kursora — aktualizowany live bez rebuildu
        private UIButtonHandle _btnCursorToggle;

        // ── Paleta ────────────────────────────────────────────────────────────
        private static readonly Color CB = new Color(0.04f, 0.05f, 0.09f, 0.97f);
        private static readonly Color CBRD = new Color(0.28f, 0.48f, 0.90f, 0.55f);
        private static readonly Color CDIM = new Color(0.44f, 0.48f, 0.60f, 1.00f);
        private static readonly Color CVAL = new Color(1.00f, 0.84f, 0.28f, 1.00f);
        private static readonly Color COK = new Color(0.20f, 0.80f, 0.32f, 1.00f);
        private static readonly Color CWRN = new Color(1.00f, 0.72f, 0.10f, 1.00f);
        private static readonly Color CCL = new Color(0.44f, 0.08f, 0.08f, 1.00f);
        private static readonly Color CGR = new Color(0.08f, 0.32f, 0.12f, 1.00f);
        private static readonly Color CBL = new Color(0.10f, 0.18f, 0.38f, 1.00f);
        private static readonly Color CMN = new Color(0.26f, 0.09f, 0.09f, 1.00f);
        private static readonly Color CPL = new Color(0.09f, 0.26f, 0.09f, 1.00f);
        private static readonly Color CKFX = new Color(0.38f, 0.07f, 0.07f, 1.00f);
        private static readonly Color CFLYB = new Color(0.38f, 0.68f, 1.00f, 1.00f);
        private static readonly Color CUIM = new Color(0.70f, 0.45f, 0.10f, 1.00f);
        private static readonly Color CFLYG = new Color(0.10f, 0.22f, 0.14f, 1.00f);

        private static readonly Color KF_CYAN = new Color(0.07f, 0.22f, 0.40f, 1f);
        private static readonly Color KF_MAG = new Color(0.26f, 0.08f, 0.30f, 1f);

        private const float TILE_W = 215f;
        private const float TILE_H = 32f;
        private const float TILE_GAP = 4f;
        private const float PANEL_X = 8f;
        private const float PANEL_BOT_OFFSET = 8f;
        private const int SORT_ORDER = 800;

        // ═════════════════════════════════════════════════════════════════════

        public GlideRailPanel(GlideRailSession session)
        {
            _session = session;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Build()
        {
            _panelVisible = false;
            RebuildPanel();
        }

        public void Destroy()
        {
            FrameworkAPI.DestroyPanel("GlideRail");
            _panel = null;
        }

        public void Toggle()
        {
            _panelVisible = !_panelVisible;

            if (_panelVisible)
                _session.OnPanelOpened();
            else
                _session.OnPanelClosed();

            if (_panel == null)
            {
                RebuildPanel();
                return;
            }

            _panel.SetVisible(_panelVisible);
        }

        // ── Zdarzenia z sesji ─────────────────────────────────────────────────

        public void OnKeyframesChanged()
        {
            RefreshKfLabel();
            _rebuildPending = true;
        }

        public void OnPlaybackTick(float t, float dur)
            => _lblProgress?.SetText($"▶  {t:F1}s / {dur:F0}s");

        public void OnPlaybackEnd()
        {
            _lblProgress?.SetText("");
            _lblHint?.SetText("Playback complete.");
            _lblHint?.SetColor(COK);
        }

        /// <summary>
        /// Aktualizuje przycisk i hint live — bez pełnego rebuildu.
        /// To jest klucz do naprawienia buga kursora.
        /// </summary>
        public void OnCursorModeChanged(bool uiMode)
        {
            // Hint bar
            _lblHint?.SetText(uiMode
                ? "UI MODE  —  kursor wolny, używaj panelu  (F9 = wróć do lotu)"
                : "FLY MODE  —  mysz steruje kamerą  (F9 = kursor)");
            _lblHint?.SetColor(uiMode ? CWRN : CFLYB);

            // Przycisk — live update, BEZ _rebuildPending
            _btnCursorToggle?.SetText(uiMode ? "🖱  UI Mode" : "🎮  Fly Mode");
            _btnCursorToggle?.SetBgColor(uiMode ? CUIM : CFLYG);
        }

        // ═════════════════════════════════════════════════════════════════════
        // BUILDER
        // ═════════════════════════════════════════════════════════════════════

        private void RebuildPanel()
        {
            FrameworkAPI.DestroyPanel("GlideRail");

            int sw = Screen.width;
            int sh = Screen.height;

            int kfCount = _session.Keyframes.Count;
            int perRow = Math.Max(1, (int)((sw - 55f) / (TILE_W + 5f)));
            int kfRows = kfCount > 0
                ? (int)Math.Ceiling((double)kfCount / perRow) : 0;

            int panH = 148 + (kfRows > 0 ? 20 + (int)(kfRows * (TILE_H + TILE_GAP)) : 0);
            int panY = sh - panH - (int)PANEL_BOT_OFFSET;

            var p = UIPanel.Create("GlideRail", PANEL_X, panY, sw - 16, panH);

            p.AddTitleButton("✕", () =>
            {
                _panelVisible = false;
                _session.OnPanelClosed();
                FrameworkAPI.DestroyPanel("GlideRail");
                _panel = null;
            }, CCL);

            p.Build(SORT_ORDER);
            p.SetScrollbarVisible(false);
            p.SetDragWhenScrollable(false);

            StylePanel(p);

            // ── HINT BAR ─────────────────────────────────────────────────────
            p.AddSpace(2f);
            bool uiMode = _session.IsUIMode;
            _lblHint = p.AddRow(15f, 2f).AddLabel(
                "WASD = ruch   Mouse = widok   Q/E = roll   " +
                "Shift = szybko   Space/Ctrl = góra/dół   " +
                "F5 = add KF   F6 = usuń ostatni   F9 = cursor",
                (float)(sw - 40),
                uiMode ? CWRN : CFLYB);
            _lblHint.SetFontSize(10);

            p.AddSeparator();

            BuildControlsRow(p, sw);

            if (kfCount > 0)
                BuildKfTiles(p, sw, perRow);

            p.AddSpace(3f);

            p.SetUpdateCallback(dt =>
            {
                if (_rebuildPending)
                {
                    _rebuildPending = false;
                    MelonCoroutines.Start(RebuildDeferred());
                    return;
                }

                if (_session.IsPlaying)
                    _lblProgress?.SetText(
                        $"▶  {_session.PlaybackTime:F1}s / {_session.PlayDur:F0}s");
            });

            _panel = p;
            _panel.SetVisible(_panelVisible);
        }

        // ── Controls row ──────────────────────────────────────────────────────

        private void BuildControlsRow(UIPanel p, int sw)
        {
            var rc = p.AddRow(30f, 5f);

            // ── Cursor toggle ─────────────────────────────────────────────────
            bool uiMode = _session.IsUIMode;
            _btnCursorToggle = rc.AddButton(
                uiMode ? "🖱  UI Mode" : "🎮  Fly Mode",
                92f,
                () => _session.ToggleCursor(),
                uiMode ? CUIM : CFLYG);

            rc.AddLabel("│", 10f, CDIM);

            // ── Playback ──────────────────────────────────────────────────────
            rc.AddButton("▶  Play", 76f, () =>
            {
                if (_session.Keyframes.Count < 2)
                {
                    _lblHint?.SetText("Potrzebujesz co najmniej 2 keyframe'y!");
                    _lblHint?.SetColor(CWRN);
                    return;
                }
                _session.StartPlayback();
                _lblHint?.SetText("Odtwarzanie ścieżki...");
                _lblHint?.SetColor(COK);
            }, CGR);

            rc.AddButton("⏹  Stop", 66f, () =>
            {
                _session.StopPlayback();
                _lblHint?.SetText("Zatrzymano.");
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddLabel("│", 10f, CDIM);

            // ── Path duration ─────────────────────────────────────────────────
            rc.AddLabel("Path:", 32f, CDIM).SetFontSize(10);
            _lblDurVal = rc.AddLabel($"{_session.PlayDur:F0}s", 30f, CVAL);
            _lblDurVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.PlayDur -= 1f;
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.PlayDur += 1f;
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CPL);

            // ── Move speed ────────────────────────────────────────────────────
            rc.AddLabel("Move:", 36f, CDIM).SetFontSize(10);
            _lblSpdVal = rc.AddLabel($"{_session.FlySpeed:F0}", 26f, CVAL);
            _lblSpdVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.FlySpeed -= 2f;
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.FlySpeed += 2f;
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CPL);

            // ── Look sensitivity ──────────────────────────────────────────────
            rc.AddLabel("Look:", 36f, CDIM).SetFontSize(10);
            _lblSnsVal = rc.AddLabel($"{_session.FlySens:F1}", 26f, CVAL);
            _lblSnsVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.FlySens -= 0.2f;
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.FlySens += 0.2f;
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CPL);

            rc.AddLabel("│", 10f, CDIM);

            // ── KF count ─────────────────────────────────────────────────────
            int kfc = _session.Keyframes.Count;
            _lblKfCount = rc.AddLabel(
                $" KF: {kfc} ", 56f,
                kfc >= 2 ? COK : kfc == 1 ? CWRN : CDIM);
            _lblKfCount.SetFontSize(12);

            rc.AddButton("[F5]  + KF", 86f, () =>
            {
                _session.AddKeyframe();
                _lblHint?.SetText($"Keyframe #{_session.Keyframes.Count} dodany.");
                _lblHint?.SetColor(CVAL);
            }, CBL);

            rc.AddButton("[F6] ✕ Last", 84f, () =>
            {
                if (_session.Keyframes.Count == 0) return;
                _session.RemoveLastKeyframe();
                _lblHint?.SetText(
                    $"Ostatni KF usunięty. Pozostało: {_session.Keyframes.Count}");
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddButton("🗑 Clear All", 84f, () =>
            {
                _session.ClearKeyframes();
                _lblHint?.SetText("Wszystkie keyframe'y usunięte.");
                _lblHint?.SetColor(CDIM);
            }, new Color(0.40f, 0.06f, 0.06f, 1f));

            _lblProgress = rc.AddLabel("", 108f, COK);
            _lblProgress.SetFontSize(10);
        }

        // ── KF Tiles ──────────────────────────────────────────────────────────

        private void BuildKfTiles(UIPanel p, int sw, int perRow)
        {
            p.AddSeparator();

            var lhdr = p.AddRow(16f, 2f).AddLabel(
                $" Keyframes ({_session.Keyframes.Count})" +
                "  ·  click = skocz  ·  ✕ = usuń  ·  cyan → magenta = kierunek",
                (float)(sw - 44), CDIM);
            lhdr.SetFontSize(10);

            p.AddSpace(2f);

            int kfCount = _session.Keyframes.Count;

            for (int rowStart = 0; rowStart < kfCount; rowStart += perRow)
            {
                var tileRow = p.AddRow(TILE_H + 4f, 4f);
                int rowEnd = Math.Min(rowStart + perRow, kfCount);

                for (int ki = rowStart; ki < rowEnd; ki++)
                {
                    int capI = ki;
                    var kf = _session.Keyframes[ki];
                    float tf = kfCount > 1 ? (float)ki / (kfCount - 1) : 0f;
                    var tileColor = Color.Lerp(KF_CYAN, KF_MAG, tf);

                    string lbl = $" #{ki + 1}" +
                                 $"  {kf.Position.x:F0}" +
                                 $" {kf.Position.y:F0}" +
                                 $" {kf.Position.z:F0}";

                    if (Math.Abs(kf.SpeedMultiplier - 1f) > 0.05f)
                        lbl += $"  ×{kf.SpeedMultiplier:F1}";

                    tileRow.AddButton(lbl, TILE_W - 30f, () =>
                    {
                        _session.JumpToKeyframe(capI);
                        _lblHint?.SetText($"Skoczono do KF #{capI + 1}");
                        _lblHint?.SetColor(CVAL);
                    }, tileColor);

                    tileRow.AddButton("✕", 26f,
                        () => _session.RemoveKeyframe(capI), CKFX);
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void StylePanel(UIPanel p)
        {
            var ve = UIRuntime.WrapVE(p.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, CB);
            S.BorderRadius(st, 10f);
            S.BorderColor(st, CBRD);
            S.BorderWidth(st, 1.5f);
        }

        private void RefreshKfLabel()
        {
            if (_lblKfCount == null) return;
            int kfc = _session.Keyframes.Count;
            _lblKfCount.SetText($" KF: {kfc} ");
            _lblKfCount.SetColor(kfc >= 2 ? COK : kfc == 1 ? CWRN : CDIM);
        }

        private IEnumerator RebuildDeferred()
        {
            yield return null;
            RebuildPanel();
        }
    }
}