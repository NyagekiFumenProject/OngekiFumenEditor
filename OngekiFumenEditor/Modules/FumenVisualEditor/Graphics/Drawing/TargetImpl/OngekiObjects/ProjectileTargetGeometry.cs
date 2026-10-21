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
                // 复刻游戏 NotesManager.calcNoteFrameAimBulletCreate：
                // 0 速显式返回 0（命中帧取玩家位置）；负速保留 Min(1, speed) 的负号（命中后才 setup）。
                var frameOffset = speed == 0f
                    ? 0f
                    : (40f - 7.5f) / (.47f * MathF.Min(speed, 1f));
                var offsetMilliseconds = TGridCalculator.FRAME_DURATION * frameOffset;
                TimeSpan targetTime;
                if (!float.IsFinite(offsetMilliseconds))
                    targetTime = TimeSpan.Zero;
                else if (offsetMilliseconds > 0 && offsetMilliseconds >= noteAudioTime.TotalMilliseconds)
                    targetTime = TimeSpan.Zero;
                else if (offsetMilliseconds < 0 && -offsetMilliseconds >= TimeSpan.MaxValue.TotalMilliseconds - noteAudioTime.TotalMilliseconds)
                    targetTime = TimeSpan.MaxValue;
                else
                    targetTime = noteAudioTime - TGridCalculator.ConvertFrameToAudioTime(frameOffset);
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
