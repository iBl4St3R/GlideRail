// GlideRailPanel.cs
using CMS2026UITKFramework;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace GlideRail
{
    /// <summary>
    /// Panel UI GlideRail.
    /// Odpowiada wyłącznie za prezentację i przekazywanie akcji do GlideRailSession.
    /// Stan logiczny trzyma sesja — panel tylko go odczytuje i renderuje.
    /// </summary>
    public class GlideRailPanel
    {
        // ── Referencja do sesji ───────────────────────────────────────────────
        private readonly GlideRailSession _session;

        // ── Panel ─────────────────────────────────────────────────────────────
        private UIPanel _panel;
        private bool _rebuildPending = false;

        // ── Live label handles ────────────────────────────────────────────────
        private UILabelHandle _lblHint;
        private UILabelHandle _lblKfCount;
        private UILabelHandle _lblDurVal;
        private UILabelHandle _lblSpdVal;
        private UILabelHandle _lblSnsVal;
        private UILabelHandle _lblProgress;

        // ── Paleta ────────────────────────────────────────────────────────────
        private static readonly Color CB = new Color(0.04f, 0.05f, 0.09f, 0.97f);
        private static readonly Color CBRD = new Color(0.28f, 0.48f, 0.90f, 0.55f);
        private static readonly Color CDIM = new Color(0.44f, 0.48f, 0.60f, 1.00f);
        private static readonly Color CVAL = new Color(1.00f, 0.84f, 0.28f, 1.00f);
        private static readonly Color COK = new Color(0.20f, 0.80f, 0.32f, 1.00f);
        private static readonly Color CWRN = new Color(1.00f, 0.72f, 0.10f, 1.00f);
        private static readonly Color CBAD = new Color(0.90f, 0.20f, 0.18f, 1.00f);
        private static readonly Color CCL = new Color(0.44f, 0.08f, 0.08f, 1.00f);
        private static readonly Color CGR = new Color(0.08f, 0.32f, 0.12f, 1.00f);
        private static readonly Color CBL = new Color(0.10f, 0.18f, 0.38f, 1.00f);
        private static readonly Color CMN = new Color(0.26f, 0.09f, 0.09f, 1.00f);
        private static readonly Color CPL = new Color(0.09f, 0.26f, 0.09f, 1.00f);
        private static readonly Color CKFX = new Color(0.38f, 0.07f, 0.07f, 1.00f);
        private static readonly Color CFLYB = new Color(0.38f, 0.68f, 1.00f, 1.00f);

        // KF tile gradient: cyan → magenta
        private static readonly Color KF_CYAN = new Color(0.07f, 0.22f, 0.40f, 1f);
        private static readonly Color KF_MAG = new Color(0.26f, 0.08f, 0.30f, 1f);

        // ── Layout constants ──────────────────────────────────────────────────
        private const float TILE_W = 215f;
        private const float TILE_H = 32f;
        private const float TILE_GAP = 4f;
        private const float PANEL_X = 8f;
        private const float PANEL_BOT_OFFSET = 8f;

        // ═════════════════════════════════════════════════════════════════════

        public GlideRailPanel(GlideRailSession session)
        {
            _session = session;
        }

        // ── Build / Rebuild ───────────────────────────────────────────────────

        public void Build() => RebuildPanel();

        public void Destroy()
        {
            FrameworkAPI.DestroyPanel("GlideRail");
        }

        public void Toggle()
        {
            var p = FrameworkAPI.GetPanel("GlideRail");
            if (p == null) { RebuildPanel(); return; }
            p.Toggle();
        }

        // ── Wywołania z sesji (zdarzenia) ─────────────────────────────────────

        /// <summary>Wywołaj gdy lista keyframe'ów się zmieni — panel przebuduje się w następnej klatce.</summary>
        public void OnKeyframesChanged()
        {
            // Aktualizacja live labels jeśli panel już istnieje
            RefreshKfLabel();
            // Pełny rebuild dla zmiany kafelków (deferred — bezpiecznie)
            _rebuildPending = true;
        }

        public void OnPlaybackTick(float t, float dur)
        {
            _lblProgress?.SetText($"▶  {t:F1}s / {dur:F0}s");
        }

        public void OnPlaybackEnd()
        {
            _lblProgress?.SetText("");
            _lblHint?.SetText("Playback complete.");
            _lblHint?.SetColor(COK);
        }

        public void OnCursorModeChanged(bool uiMode)
        {
            _lblHint?.SetText(uiMode
                ? "UI MODE  —  cursor free  (F9 = back to fly)"
                : "FLY MODE  —  mouse look active  (F9 = cursor)");
            _lblHint?.SetColor(uiMode ? CWRN : CFLYB);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PANEL BUILDER
        // ═════════════════════════════════════════════════════════════════════

        private void RebuildPanel()
        {
            FrameworkAPI.DestroyPanel("GlideRail");

            int sw = Screen.width;
            int sh = Screen.height;

            // ── Dynamiczna wysokość zależna od liczby keyframe'ów ─────────────
            int kfCount = _session.Keyframes.Count;
            int perRow = Math.Max(1, (int)((sw - 55f) / (TILE_W + 5f)));
            int kfRows = kfCount > 0
                ? (int)Math.Ceiling((double)kfCount / perRow) : 0;

            // Bazowa wysokość: hint(20) + sep + controls row(44) + padding
            // + opcjonalnie nagłówek KF (20) + kafelki
            int baseH = 148;
            int tilesH = kfRows > 0 ? 20 + (int)(kfRows * (TILE_H + TILE_GAP)) : 0;
            int panH = baseH + tilesH;
            int panY = sh - panH - (int)PANEL_BOT_OFFSET;

            // ── Utwórz panel ─────────────────────────────────────────────────
            var p = UIPanel.Create("GlideRail", PANEL_X, panY, sw - 16, panH);

            // Przycisk zamknij
            p.AddTitleButton("✕", () =>
            {
                _session.StopFly();
                _session.StopPlayback();
                FrameworkAPI.DestroyPanel("GlideRail");
            }, CCL);

            p.Build(49000);
            p.SetScrollbarVisible(false);
            p.SetDragWhenScrollable(false);

            // Styl panelu
            StylePanel(p);

            // ── HINT BAR ─────────────────────────────────────────────────────
            p.AddSpace(2f);
            _lblHint = p.AddRow(15f, 2f).AddLabel(
                "WASD = move   Mouse = look   Q/E = roll   Shift = fast   " +
                "F5 = add KF   F6 = remove last   F9 = cursor",
                (float)(sw - 40), CFLYB);
            _lblHint.SetFontSize(10);

            p.AddSeparator();

            // ── MAIN CONTROLS ROW ─────────────────────────────────────────────
            BuildControlsRow(p, sw);

            // ── KEYFRAME TILES ────────────────────────────────────────────────
            if (kfCount > 0)
                BuildKfTiles(p, sw, perRow);

            p.AddSpace(3f);

            // ── UPDATE CALLBACK ───────────────────────────────────────────────
            p.SetUpdateCallback(dt =>
            {
                // Deferred rebuild
                if (_rebuildPending)
                {
                    _rebuildPending = false;
                    MelonCoroutines.Start(RebuildDeferred());
                    return;
                }

                // Live progress
                if (_session.IsPlaying)
                    _lblProgress?.SetText(
                        $"▶  {_session.PlaybackTime:F1}s / {_session.PlayDur:F0}s");
            });

            _panel = p;
        }

        // ── Controls row ──────────────────────────────────────────────────────

        private void BuildControlsRow(UIPanel p, int sw)
        {
            var rc = p.AddRow(30f, 5f);

            // ── FLY toggle ────────────────────────────────────────────────────
            rc.AddButton(_session.IsFlyActive ? "⏹ Stop Fly" : "✈ Start Fly", 90f, () =>
            {
                if (_session.IsFlyActive) _session.StopFly();
                else _session.StartFly();
                _rebuildPending = true;
            }, _session.IsFlyActive ? CCL : CBL);

            rc.AddLabel("│", 10f, CDIM);

            // ── Playback ──────────────────────────────────────────────────────
            rc.AddButton("▶  Play", 80f, () =>
            {
                if (_session.Keyframes.Count < 2)
                {
                    _lblHint?.SetText("Need at least 2 keyframes!");
                    _lblHint?.SetColor(CWRN);
                    return;
                }
                _session.StartPlayback();
                _lblHint?.SetText("Playing camera path...");
                _lblHint?.SetColor(COK);
            }, CGR);

            rc.AddButton("⏹  Stop", 68f, () =>
            {
                _session.StopPlayback();
                _lblHint?.SetText("Stopped.");
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddLabel("│", 10f, CDIM);

            // ── Duration ──────────────────────────────────────────────────────
            rc.AddLabel("Dur:", 28f, CDIM).SetFontSize(10);
            _lblDurVal = rc.AddLabel($"{_session.PlayDur:F0}s", 30f, CVAL);
            _lblDurVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.PlayDur = Mathf.Max(2f, _session.PlayDur - 1f);
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.PlayDur = Mathf.Min(180f, _session.PlayDur + 1f);
                _lblDurVal?.SetText($"{_session.PlayDur:F0}s");
            }, CPL);

            // ── Speed ─────────────────────────────────────────────────────────
            rc.AddLabel("Spd:", 28f, CDIM).SetFontSize(10);
            _lblSpdVal = rc.AddLabel($"{_session.FlySpeed:F0}", 28f, CVAL);
            _lblSpdVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.FlySpeed = Mathf.Max(1f, _session.FlySpeed - 2f);
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.FlySpeed = Mathf.Min(80f, _session.FlySpeed + 2f);
                _lblSpdVal?.SetText($"{_session.FlySpeed:F0}");
            }, CPL);

            // ── Sensitivity ───────────────────────────────────────────────────
            rc.AddLabel("Sns:", 28f, CDIM).SetFontSize(10);
            _lblSnsVal = rc.AddLabel($"{_session.FlySens:F1}", 28f, CVAL);
            _lblSnsVal.SetFontSize(11);
            rc.AddButton("−", 20f, () =>
            {
                _session.FlySens = Mathf.Max(0.1f, _session.FlySens - 0.2f);
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CMN);
            rc.AddButton("+", 20f, () =>
            {
                _session.FlySens = Mathf.Min(10f, _session.FlySens + 0.2f);
                _lblSnsVal?.SetText($"{_session.FlySens:F1}");
            }, CPL);

            rc.AddLabel("│", 10f, CDIM);

            // ── KF count ─────────────────────────────────────────────────────
            int kfc = _session.Keyframes.Count;
            _lblKfCount = rc.AddLabel(
                $" KF: {kfc} ",
                58f,
                kfc >= 2 ? COK : kfc == 1 ? CWRN : CDIM);
            _lblKfCount.SetFontSize(12);

            // ── KF actions ────────────────────────────────────────────────────
            rc.AddButton("[F5]  + KF", 88f, () =>
            {
                _session.AddKeyframe();
                _lblHint?.SetText($"Keyframe #{_session.Keyframes.Count} added.");
                _lblHint?.SetColor(CVAL);
            }, CBL);

            rc.AddButton("[F6] ✕ Last", 86f, () =>
            {
                if (_session.Keyframes.Count == 0) return;
                _session.RemoveLastKeyframe();
                _lblHint?.SetText($"Last KF removed. {_session.Keyframes.Count} remaining.");
                _lblHint?.SetColor(CDIM);
            }, CCL);

            rc.AddButton("🗑 Clear", 70f, () =>
            {
                _session.ClearKeyframes();
                _lblHint?.SetText("All keyframes cleared.");
                _lblHint?.SetColor(CDIM);
            }, new Color(0.40f, 0.06f, 0.06f, 1f));

            // ── Progress readout ──────────────────────────────────────────────
            _lblProgress = rc.AddLabel("", 110f, COK);
            _lblProgress.SetFontSize(10);
        }

        // ── Keyframe tiles ────────────────────────────────────────────────────

        private void BuildKfTiles(UIPanel p, int sw, int perRow)
        {
            p.AddSeparator();

            var lhdr = p.AddRow(16f, 2f).AddLabel(
                $" Keyframes ({_session.Keyframes.Count})" +
                "  ·  click = jump  ·  ✕ = delete  ·  cyan → magenta = path direction",
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

                    // Kolor kafla — gradient cyan → magenta
                    var tileColor = Color.Lerp(KF_CYAN, KF_MAG, tf);

                    // Etykieta: numer + pozycja XYZ
                    string lbl = $" #{ki + 1}" +
                                 $"  {kf.Position.x:F0}" +
                                 $" {kf.Position.y:F0}" +
                                 $" {kf.Position.z:F0}";

                    // Prędkość lokalna — pokaż jeśli inna niż 1
                    if (Math.Abs(kf.SpeedMultiplier - 1f) > 0.05f)
                        lbl += $"  ×{kf.SpeedMultiplier:F1}";

                    // Jump button
                    var jumpBtn = tileRow.AddButton(
                        lbl, TILE_W - 30f,
                        () =>
                        {
                            _session.JumpToKeyframe(capI);
                            _lblHint?.SetText($"Jumped to KF #{capI + 1}");
                            _lblHint?.SetColor(CVAL);
                        },
                        tileColor);

                    // Delete button
                    tileRow.AddButton("✕", 26f,
                        () =>
                        {
                            _session.RemoveKeyframe(capI);
                        },
                        CKFX);
                }
            }
        }

        // ── Panel styling ─────────────────────────────────────────────────────

        private static void StylePanel(UIPanel p)
        {
            var ve = UIRuntime.WrapVE(p.GetPanelRawPtr());
            var st = UIRuntime.GetStyle(ve);
            S.BgColor(st, CB);
            S.BorderRadius(st, 10f);
            S.BorderColor(st, CBRD);
            S.BorderWidth(st, 1.5f);
        }

        // ── Live label refresh (bez pełnego rebuildu) ─────────────────────────

        private void RefreshKfLabel()
        {
            if (_lblKfCount == null) return;
            int kfc = _session.Keyframes.Count;
            _lblKfCount.SetText($" KF: {kfc} ");
            _lblKfCount.SetColor(kfc >= 2 ? COK : kfc == 1 ? CWRN : CDIM);
        }

        // ── Deferred rebuild coroutine ────────────────────────────────────────

        private IEnumerator RebuildDeferred()
        {
            yield return null; // poczekaj jedną klatkę — panel zostanie zniszczony bezpiecznie
            RebuildPanel();
        }
    }
}