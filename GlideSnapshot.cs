using System.Collections.Generic;

namespace GlideRail
{
    public class GlideSnapshot
    {
        public List<GlideKeyframe> Keyframes { get; }
        public float PlayDur { get; }
        public string Label { get; }

        public GlideSnapshot(
            IReadOnlyList<GlideKeyframe> keyframes,
            float playDur,
            string label)
        {
            Label = label;
            PlayDur = playDur;

            // Deep copy — każdy KF to nowy obiekt
            Keyframes = new List<GlideKeyframe>(keyframes.Count);
            foreach (var kf in keyframes)
                Keyframes.Add(new GlideKeyframe
                {
                    Position = kf.Position,
                    Rotation = kf.Rotation,
                    SpeedMultiplier = kf.SpeedMultiplier
                });
        }
    }
}