
using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace GlideRail
{
    public class GlideRailPanel
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT p);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private POINT _savedCursorPos;
        private bool _hasSavedPos;


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


        private GlideTimeline _timeline;

        // ── Timeline constants (muszą zgadzać się z GlideTimeline) ───────────
        private const float ROW_H = 26f;
        private const float SEP_H = 2f;
        private const float SCROLLBAR_H = 14f;

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
            _timeline?.Refresh();
        }

        public void OnPlaybackTick(float t, float dur)
        {
            _lblProgress?.SetText($"▶  {t:F1}s / {dur:F0}s");
            int kfIdx = Mathf.Clamp(
                (int)(_session.PlaybackTime / _session.PlayDur
                    * _session.Keyframes.Count), 0,
                _session.Keyframes.Count - 1);
            _timeline?.SetActiveKf(kfIdx);
        }

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
            _lblHint?.SetText(uiMode
                ? "UI MODE  —  cursor free, use the panel  (F9 = back to fly)"
                : "FLY MODE  —  mouse controls camera  (F9 = cursor)");
            _lblHint?.SetColor(uiMode ? CWRN : CFLYB);

            _btnCursorToggle?.SetText(uiMode ? "🖱  UI Mode [F9]" : "🎮  Fly Mode [F9]");
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
            const int panH = 200;  // stała wysokość
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

            float timelineH = ROW_H * 2 + SEP_H + SCROLLBAR_H + 4f;
            float timelineTop = panH - timelineH - 8f; // było -24f


            _timeline = new GlideTimeline(_session, p);
            _timeline.Build(sw - 16, timelineTop);


            // ── HINT BAR ─────────────────────────────────────────────────────
            p.AddSpace(2f);
            bool uiMode = _session.IsUIMode;
            _lblHint = p.AddRow(15f, 2f).AddLabel(
            "WASD = move   Mouse = look   Q/E = roll   " +
            "R = up   F = down   " +
            "F5 = add KF   F6 = remove last   F9 = cursor",
            (float)(sw - 40),
            uiMode ? CWRN : CFLYB);
            _lblHint.SetFontSize(10);

            p.AddSeparator();

            BuildControlsRow(p, sw);


            p.AddSpace(3f);

            p.SetUpdateCallback(dt =>
            {
                _timeline?.OnUpdate();  // ← dodaj tutaj

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


            // FOOTER stopka
            const float FOOT_H = 14f;
            var footVE = UIRuntime.NewVE();
            var footSt = UIRuntime.GetStyle(footVE);
            S.Position(footSt, "Absolute");
            S.Left(footSt, 0f);
            S.Top(footSt, panH - FOOT_H - 2f);
            S.Width(footSt, (float)(sw - 16));
            S.Height(footSt, FOOT_H);
            p.AddOverlayToPanel(footVE);

            var footerLbl = p.AddLabelToContainer(
                footVE,
                "GlideRail v0.1.0  ·  Made by iBlaster  ·  github.com/iBl4St3R/GlideRail",
                0f, 0f, (float)(sw - 16), FOOT_H,
                new Color(0.28f, 0.48f, 0.90f, 0.40f));
            footerLbl.SetFontSize(9);
            S.TextAlign(
                UIRuntime.GetStyle(UIRuntime.WrapVE(footerLbl.GetRawPtr())),
                TextAnchor.MiddleCenter);


            _panel = p;
            _panel.SetVisible(_panelVisible);
            _session.ResyncCursorAfterRebuild();

            if (_hasSavedPos)
            {
                _hasSavedPos = false;
                SetCursorPos(_savedCursorPos.X, _savedCursorPos.Y);
            }
        }



        // ── Controls row ──────────────────────────────────────────────────────

        private void BuildControlsRow(UIPanel p, int sw)
        {
            var rc = p.AddRow(30f, 5f);

            // ── Cursor toggle ─────────────────────────────────────────────────
            bool uiMode = _session.IsUIMode;
            _btnCursorToggle = rc.AddButton(
            uiMode ? "🖱  UI Mode [F9]" : "🎮  Fly Mode [F9]",
            120f,
            () => _session.ToggleCursor(),
            uiMode ? CUIM : CFLYG);

            rc.AddLabel("│", 10f, CDIM);

            // ── Cinematic Playback ────────────────────────────────────────────
            rc.AddButton("🎬 Cinematic", 90f, () =>
            {
                if (_session.Keyframes.Count < 2)
                {
                    _lblHint?.SetText("Need at least 2 keyframes!");
                    _lblHint?.SetColor(CWRN);
                    return;
                }
                _session.StartCinematicPlay();
            }, new Color(0.35f, 0.10f, 0.45f, 1f));


            // ── Playback ──────────────────────────────────────────────────────
            rc.AddButton("▶  Play", 76f, () =>
            {
                if (_session.Keyframes.Count < 2)
                {
                    _lblHint?.SetText("Need at least 2 keyframes!");
                    _lblHint?.SetColor(CWRN);
                    return;
                }
                _session.StartPlayback();
                _lblHint?.SetText("Playing path...");
                _lblHint?.SetColor(COK);
            }, CGR);

            rc.AddButton("⏹  Stop", 66f, () =>
            {
                _session.StopPlayback();
                _lblHint?.SetText(("Stopped."));
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddLabel("│", 10f, CDIM);

            // ── Path duration ─────────────────────────────────────────────────────────
            rc.AddLabel("Total Time:", 160f, CDIM).SetFontSize(17);
            _lblDurVal = rc.AddLabel($"{_session.PlayDur:F0}s", 40f, CVAL);
            _lblDurVal.SetFontSize(15);
            rc.AddButton("−", 24f, () =>
            {
                _session.PlayDur -= 1f;
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CMN);
            rc.AddButton("+", 24f, () =>
            {
                _session.PlayDur += 1f;
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CPL);

            // ── Move speed ────────────────────────────────────────────────────────────
            rc.AddLabel("Move:", 52f, CDIM).SetFontSize(17);
            _lblSpdVal = rc.AddLabel($"{_session.FlySpeed:F0}", 36f, CVAL);
            _lblSpdVal.SetFontSize(15);
            rc.AddButton("−", 24f, () =>
            {
                _session.FlySpeed -= 2f;
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CMN);
            rc.AddButton("+", 24f, () =>
            {
                _session.FlySpeed += 2f;
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CPL);

            // ── Look sensitivity ──────────────────────────────────────────────────────
            rc.AddLabel("Look:", 52f, CDIM).SetFontSize(17);
            _lblSnsVal = rc.AddLabel($"{_session.FlySens:F1}", 36f, CVAL);
            _lblSnsVal.SetFontSize(15);
            rc.AddButton("−", 24f, () =>
            {
                _session.FlySens -= 0.2f;
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CMN);
            rc.AddButton("+", 24f, () =>
            {
                _session.FlySens += 0.2f;
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CPL);

            rc.AddLabel("│", 10f, CDIM);

            // ── KF count ─────────────────────────────────────────────────────────────
            int kfc = _session.Keyframes.Count;
            _lblKfCount = rc.AddLabel($" KF: {kfc} ", 76f,
            kfc >= 2 ? COK : kfc == 1 ? CWRN : CDIM);
            _lblKfCount.SetFontSize(19);

            rc.AddButton("[F5]  + KF", 86f, () =>
            {
                _session.AddKeyframe();
                _lblHint?.SetText($"Keyframe #{_session.Keyframes.Count} added.");
                _lblHint?.SetColor(CVAL);
            }, CBL);

            rc.AddButton("[F6] ✕ Last", 84f, () =>
            {
                if (_session.Keyframes.Count == 0) return;
                _session.RemoveLastKeyframe();
                _lblHint?.SetText(
                    $"Last KF removed. Remaining: {_session.Keyframes.Count}");
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddButton("🗑 Clear All", 84f, () =>
            {
                _session.ClearKeyframes();
                _lblHint?.SetText("All keyframes cleared.");
                _lblHint?.SetColor(CDIM);
            }, new Color(0.40f, 0.06f, 0.06f, 1f));

            rc.AddLabel("│", 10f, CDIM);

            rc.AddButton("💾 Save", 76f, () =>
            {
                _lblHint?.SetText("Opening save dialog...");
                _lblHint?.SetColor(CDIM);
                _session.SaveToFile(msg => { });  // hint przez _pendingHint w sesji
            }, new Color(0.10f, 0.25f, 0.40f, 1f));

            rc.AddButton("📂 Load", 76f, () =>
            {
                _lblHint?.SetText("Opening load dialog...");
                _lblHint?.SetColor(CDIM);
                _session.LoadFromFile(msg => { });  // hint przez _pendingHint w sesji
            }, new Color(0.25f, 0.15f, 0.38f, 1f));





            _lblProgress = rc.AddLabel("", 108f, COK);
            _lblProgress.SetFontSize(17);
        }


        public void SetPanelVisible(bool visible)
        {
            _panel?.SetVisible(visible);
        }


        // ── Helpers ───────────────────────────────────────────────────────────

        public enum HintType { Info, Warning, Ok }

        public void ShowHint(string text, HintType type)
        {
            _lblHint?.SetText(text);
            _lblHint?.SetColor(type switch
            {
                HintType.Warning => CWRN,
                HintType.Ok => COK,
                _ => CFLYB
            });
        }


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
            if (_session.IsUIMode)
            {
                GetCursorPos(out _savedCursorPos);
                _hasSavedPos = true;
            }
            yield return null;
            RebuildPanel();
        }
    }
}