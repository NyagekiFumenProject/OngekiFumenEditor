using System;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Modules.FumenVisualEditor;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.OngekiObjects
{
    public readonly record struct ProjectileTargetPosition(double LandingXUnit, double EnvelopeMinXUnit, double EnvelopeMaxXUnit);

    public static class ProjectileTargetGeometry
    {
        public static ProjectileTargetPosition Resolve(int randomSeed, Target target, float speed, TimeSpan noteAudioTime, int id, double xUnit, int randomOffsetRange, Func<TimeSpan, double> getPlayerLocation)
        {
            var targetX = xUnit;
            if (target == Target.Player)
            {
                var safeSpeed = speed > 0 ? speed : .0001f;
                var frameOffset = (40f - 7.5f) / (.47f * MathF.Min(safeSpeed, 1));
                var offsetMilliseconds = TGridCalculator.FRAME_DURATION * frameOffset;
                // 先比较时间边界，避免极小正速度产生的无穷大进入 TimeSpan 转换。
                var targetTime = !float.IsFinite(offsetMilliseconds) || offsetMilliseconds >= noteAudioTime.TotalMilliseconds
                    ? TimeSpan.Zero
                    : noteAudioTime - TGridCalculator.ConvertFrameToAudioTime(frameOffset);
                targetX += getPlayerLocation(targetTime);
            }

            var range = Math.Max(0, randomOffsetRange);
            var min = targetX - range;
            var max = targetX + range;
            if (range > 0)
            {
                var seed = unchecked((randomSeed * id + 123) * id ^ id);
                var nonNegativeSeed = Math.Abs((long)seed);
                targetX += -range + (nonNegativeSeed % (range * 2L + 1));
            }

            return new(targetX, min, max);
        }
    }
}
