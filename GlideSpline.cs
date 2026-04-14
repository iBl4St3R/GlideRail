using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlideRail
{
    public static class GlideSpline
    {
        // ── Catmull-Rom (1 scalar) ────────────────────────────────────────────
        private static float CR(float a, float b, float c, float d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b
                + (-a + c) * t
                + (2f * a - 5f * b + 4f * c - d) * t2
                + (-a + 3f * b - 3f * c + d) * t3);
        }

        /// <summary>
        /// Samples the spline at normalized time nt ∈ [0,1].
        /// Returns interpolated position and rotation.
        /// </summary>
        public static (Vector3 pos, Quaternion rot) Sample(
            IReadOnlyList<GlideKeyframe> kfs, float nt)
        {
            int n = kfs.Count;
            if (n == 0) return (Vector3.zero, Quaternion.identity);
            if (n == 1) return (kfs[0].Position, kfs[0].Rotation);

            float ft = Mathf.Clamp01(nt) * (n - 1);
            int i = Mathf.Clamp((int)ft, 0, n - 2);
            float f = ft - i;

            int ia = Math.Max(0, i - 1), ib = i,
                ic = Math.Min(n - 1, i + 1), id = Math.Min(n - 1, i + 2);

            // Position
            var pos = new Vector3(
                CR(kfs[ia].Position.x, kfs[ib].Position.x,
                   kfs[ic].Position.x, kfs[id].Position.x, f),
                CR(kfs[ia].Position.y, kfs[ib].Position.y,
                   kfs[ic].Position.y, kfs[id].Position.y, f),
                CR(kfs[ia].Position.z, kfs[ib].Position.z,
                   kfs[ic].Position.z, kfs[id].Position.z, f));

            // Rotation — Catmull-Rom on quaternion components + hemisphere flip
            var qa = kfs[ia].Rotation; var qb = kfs[ib].Rotation;
            var qc = kfs[ic].Rotation; var qd = kfs[id].Rotation;

            if (Quaternion.Dot(qb, qa) < 0f)
                qa = new Quaternion(-qa.x, -qa.y, -qa.z, -qa.w);
            if (Quaternion.Dot(qb, qc) < 0f)
                qc = new Quaternion(-qc.x, -qc.y, -qc.z, -qc.w);
            if (Quaternion.Dot(qc, qd) < 0f)
                qd = new Quaternion(-qd.x, -qd.y, -qd.z, -qd.w);

            var rot = new Quaternion(
                CR(qa.x, qb.x, qc.x, qd.x, f),
                CR(qa.y, qb.y, qc.y, qd.y, f),
                CR(qa.z, qb.z, qc.z, qd.z, f),
                CR(qa.w, qb.w, qc.w, qd.w, f));

            float mag = Mathf.Sqrt(rot.x * rot.x + rot.y * rot.y
                                 + rot.z * rot.z + rot.w * rot.w);
            if (mag > 0.0001f)
                rot = new Quaternion(rot.x / mag, rot.y / mag, rot.z / mag, rot.w / mag);

            return (pos, rot);
        }

        /// <summary>
        /// Samples the spline speed multiplier at normalized time nt.
        /// Used to modulate playback velocity per-segment.
        /// </summary>
        public static float SampleSpeed(IReadOnlyList<GlideKeyframe> kfs, float nt)
        {
            int n = kfs.Count;
            if (n == 0) return 1f;
            if (n == 1) return kfs[0].SpeedMultiplier;

            float ft = Mathf.Clamp01(nt) * (n - 1);
            int i = Mathf.Clamp((int)ft, 0, n - 2);
            float f = ft - i;

            int ia = Math.Max(0, i - 1), ib = i,
                ic = Math.Min(n - 1, i + 1), id = Math.Min(n - 1, i + 2);

            return CR(kfs[ia].SpeedMultiplier, kfs[ib].SpeedMultiplier,
                      kfs[ic].SpeedMultiplier, kfs[id].SpeedMultiplier, f);
        }
    }
}