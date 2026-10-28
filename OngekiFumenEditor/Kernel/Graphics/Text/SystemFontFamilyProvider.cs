using OngekiFumenEditor.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OngekiFumenEditor.Kernel.Graphics.Text
{
    /// <summary>
    /// 系统字体家族名枚举，来源为 Skia 自己的字体管理器（与绘制侧 <c>SKTypeface.FromFamilyName</c> 同一份家族表）。
    /// </summary>
    public static class SystemFontFamilyProvider
    {
        private static readonly Lazy<string[]> familyNames =
            new(LoadFamilyNames, LazyThreadSafetyMode.ExecutionAndPublication);

        public static IReadOnlyList<string> GetFamilyNames() => familyNames.Value;

        private static string[] LoadFamilyNames()
        {
            try
            {
                return SKFontManager.Default.FontFamilies
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Log.LogWarn($"Failed to enumerate system font families: {ex.Message}");
                return Array.Empty<string>();
            }
        }
    }
}
