using System.Collections.Generic;
using UnityEngine;

namespace GlideRail
{
    /// <summary>
    /// Renderuje wizualizację 3D ścieżki GlideRail w scenie:
    ///   • LineRenderer — spline jako świecący tor (rail coaster)
    ///   • Sfery        — keyframe markers z gradientem cyan→magenta
    /// Żyje razem z sesją — Initialize() / Shutdown() / Refresh()
    /// </summary>
    public class GlideDebugRenderer
    {
        // ── Root object ───────────────────────────────────────────────────────
        private GameObject _root;

        // ── Spline line ───────────────────────────────────────────────────────
        private GameObject _lineGO;
        private LineRenderer _lineR;

        // ── KF sphere markers ─────────────────────────────────────────────────
        private readonly List<GameObject> _spheres = new();

        // ── Kolory toru ───────────────────────────────────────────────────────
        private static readonly Color RAIL_START = new Color(0.25f, 0.65f, 1.00f, 0.90f);
        private static readonly Color RAIL_END = new Color(0.85f, 0.35f, 1.00f, 0.90f);

        // ── Kolory sfer (gradient cyan→magenta, te same co kafelki w panelu) ──
        private static readonly Color KF_CYAN = new Color(0.15f, 0.95f, 0.85f, 1f);
        private static readonly Color KF_MAG = new Color(0.95f, 0.25f, 0.75f, 1f);

        // ── Parametry ─────────────────────────────────────────────────────────
        private const float LINE_WIDTH = 0.05f;
        private const float SPHERE_SCALE = 0.20f;
        private const int SEGS_PER_KF = 30;    // gładkość krzywej między KF

        // ═════════════════════════════════════════════════════════════════════

        public void Initialize()
        {
            _root = new GameObject("GlideRail_DebugRoot");
            Object.DontDestroyOnLoad(_root);

            BuildLineRenderer();

            GlideRailPlugin.Log.Msg("[GlideRail] DebugRenderer initialized.");
        }

        public void Shutdown()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
            _lineGO = null;
            _lineR = null;
            _spheres.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.SetActive(visible);
        }

        // ── Pełny refresh — wywołuj po każdej zmianie keyframe'ów ─────────────

        public void Refresh(IReadOnlyList<GlideKeyframe> keyframes)
        {
            RefreshSpheres(keyframes);
            RefreshSpline(keyframes);
        }

        // ═════════════════════════════════════════════════════════════════════
        // PRIVATE
        // ═════════════════════════════════════════════════════════════════════

        private void BuildLineRenderer()
        {
            _lineGO = new GameObject("GlideRail_SplineLine");
            _lineGO.transform.SetParent(_root.transform, worldPositionStays: false);

            _lineR = _lineGO.AddComponent<LineRenderer>();
            _lineR.useWorldSpace = true;
            _lineR.startWidth = LINE_WIDTH;
            _lineR.endWidth = LINE_WIDTH;
            _lineR.positionCount = 0;
            _lineR.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineR.receiveShadows = false;

            // Material — Sprites/Default działa bez URP/HDRP, daje podstawowy kolor
            try
            {
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.color = Color.white;   // kolor ustawiamy przez vertex colors
                _lineR.material = mat;
                _lineR.startColor = RAIL_START;
                _lineR.endColor = RAIL_END;
            }
            catch
            {
                // Fallback — brak shadera nie crashuje
                GlideRailPlugin.Log.Warning(
                    "[GlideRail] Sprites/Default shader not found — line will be plain.");
            }
        }

        // ── Sfery keyframe'ów ─────────────────────────────────────────────────

        private void RefreshSpheres(IReadOnlyList<GlideKeyframe> keyframes)
        {
            // Usuń stare sfery
            foreach (var s in _spheres)
                if (s != null) Object.Destroy(s);
            _spheres.Clear();

            int n = keyframes.Count;

            for (int i = 0; i < n; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"GlideRail_KF_{i + 1}";
                go.transform.SetParent(_root.transform, worldPositionStays: true);
                go.transform.position = keyframes[i].Position;
                go.transform.localScale = Vector3.one * SPHERE_SCALE;

                // Usuń collider — nie potrzebujemy fizyki
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);

                // Gradient cyan → magenta wzdłuż ścieżki
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    // Nowy material na każdą sferę — unikamy współdzielenia
                    mr.material = new Material(mr.sharedMaterial);
                    mr.material.color = Color.Lerp(KF_CYAN, KF_MAG, t);
                }

                _spheres.Add(go);
            }
        }

        // ── Spline line ───────────────────────────────────────────────────────

        private void RefreshSpline(IReadOnlyList<GlideKeyframe> keyframes)
        {
            if (_lineR == null) return;

            if (keyframes.Count < 2)
            {
                _lineR.positionCount = 0;
                return;
            }

            int segs = keyframes.Count * SEGS_PER_KF;
            int total = segs + 1;

            _lineR.positionCount = total;

            for (int si = 0; si <= segs; si++)
            {
                float nt = (float)si / segs;
                var (pos, _) = GlideSpline.Sample(keyframes, nt);
                _lineR.SetPosition(si, pos);
            }
        }
    }
}