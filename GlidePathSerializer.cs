using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GlideRail
{
    /// <summary>
    /// Serializuje/deserializuje listę keyframe'ów do stringa
    /// nadającego się jako argument komendy GlideRailPlay.
    /// Format: Base64(JSON) — zwięzłe, kopiowalne w konsoli.
    /// </summary>
    public static class GlidePathSerializer
    {
        public static string Serialize(
            IReadOnlyList<GlideKeyframe> kfs, float duration)
        {
            // FAZA 2 — pełna implementacja JSON → Base64
            // Na razie zwraca placeholder
            return "PLACEHOLDER";
        }

        public static List<GlideKeyframe> Deserialize(
            string data, out float duration)
        {
            duration = 12f;
            // FAZA 2
            return null;
        }
    }
}