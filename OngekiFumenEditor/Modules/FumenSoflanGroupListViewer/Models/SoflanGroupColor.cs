using System;
using System.Numerics;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>稳定的组颜色：同一组 ID 在任何会话/启动下颜色一致。</summary>
    public static class SoflanGroupColor
    {
        /// <summary>
        /// 按组 ID 生成稳定、鲜艳的伪随机颜色：以黄金角步进色相，避免相邻 ID
        /// 落到相近色相；固定高饱和/高亮度，保证在深色画布上足够明亮。
        /// 同一组 ID 在任何会话中颜色一致。
        /// </summary>
        public static Vector4 Get(int soflanGroup)
        {
            var id = soflanGroup < 0 ? 0 : soflanGroup;
            var hue = id == 0 ? 0f : (id * 137.508f) % 360f;
            var (r, g, b) = HsvToRgb(hue, 0.9f, 1f);
            return new Vector4(r, g, b, 1f);
        }

        private static (float r, float g, float b) HsvToRgb(float h, float s, float v)
        {
            h = Math.Clamp(h, 0, 360);
            s = Math.Clamp(s, 0, 1);
            v = Math.Clamp(v, 0, 1);

            var c = v * s;
            var x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            var m = v - c;

            (float r, float g, float b) rgb;
            if (h < 60) rgb = (c, x, 0);
            else if (h < 120) rgb = (x, c, 0);
            else if (h < 180) rgb = (0, c, x);
            else if (h < 240) rgb = (0, x, c);
            else if (h < 300) rgb = (x, 0, c);
            else rgb = (c, 0, x);

            return (rgb.r + m, rgb.g + m, rgb.b + m);
        }
    }
}
