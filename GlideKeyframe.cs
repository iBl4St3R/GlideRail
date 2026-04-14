using UnityEngine;

namespace GlideRail
{
    public class GlideKeyframe
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        /// <summary>
        /// Mnożnik prędkości lokalnej w tym keyframe.
        /// 1.0 = normalna, 0.5 = połowa, 2.0 = dwukrotna.
        /// Używany przez spline do modulacji prędkości playbacku.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;
    }
}