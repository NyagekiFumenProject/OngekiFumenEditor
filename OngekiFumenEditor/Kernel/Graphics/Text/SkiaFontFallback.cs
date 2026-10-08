using OngekiFumenEditor.Utils.ObjectPool;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Kernel.Graphics.Text
{
    /// <summary>为缺失字形匹配系统字体；度量和绘制共用同一组字体片段。</summary>
    internal sealed class SkiaFontFallback : IDisposable
    {
        private const int MaxCachedRunCount = 4096;
        private const int MaxCachedFontCount = 256;

        internal readonly record struct FontRun(SKTypeface Typeface, string Text);

        private readonly Dictionary<(IntPtr primary, int codepoint), SKTypeface> resolvedTypefaces = new();
        private readonly HashSet<SKTypeface> fallbackTypefaces = new();
        private readonly Dictionary<(IntPtr primary, string text), FontRun[]> cachedRuns = new();
        private readonly Queue<(IntPtr primary, string text)> runOrder = new();
        private readonly Dictionary<(IntPtr typeface, int size, bool aliased, bool subpixel), SKFont> fonts = new();

        private SKTypeface ResolveTypeface(SKTypeface primary, int codepoint)
        {
            var key = (primary.Handle, codepoint);
            if (resolvedTypefaces.TryGetValue(key, out var resolved))
                return resolved ?? primary;

            if (!primary.ContainsGlyph(codepoint))
            {
                resolved = SKFontManager.Default.MatchCharacter(primary.FamilyName, primary.FontStyle, null, codepoint);
                if (resolved != null)
                    fallbackTypefaces.Add(resolved);
            }

            // null 表示继续使用主字体，包括系统也没有相应字形的情况。
            resolvedTypefaces[key] = resolved;
            return resolved ?? primary;
        }

        internal FontRun[] GetRuns(SKTypeface primary, string text)
        {
            var key = (primary.Handle, text);
            if (cachedRuns.TryGetValue(key, out var cached))
                return cached;

            using var runs = ObjectPool.GetPooledList<FontRun>();
            SKTypeface runTypeface = null;
            var start = 0;
            var offset = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                var typeface = ResolveTypeface(primary, rune.Value);
                if (runTypeface != null && runTypeface.Handle != typeface.Handle)
                {
                    runs.Add(new(runTypeface, text.Substring(start, offset - start)));
                    start = offset;
                }
                runTypeface = typeface;
                offset += rune.Utf16SequenceLength;
            }
            if (runTypeface != null)
                runs.Add(new(runTypeface, start == 0 ? text : text.Substring(start)));

            cached = runs.ToArray();
            cachedRuns[key] = cached;
            runOrder.Enqueue(key);
            while (cachedRuns.Count > MaxCachedRunCount && runOrder.TryDequeue(out var oldest))
                cachedRuns.Remove(oldest);
            return cached;
        }

        internal SKFont GetFont(SKTypeface typeface, int size, bool aliased, bool subpixel = false)
        {
            var key = (typeface.Handle, size, aliased, subpixel);
            if (fonts.TryGetValue(key, out var font))
                return font;

            if (fonts.Count >= MaxCachedFontCount)
                ClearFonts();
            font = new SKFont(typeface, size)
            {
                Hinting = SKFontHinting.Full,
                Edging = aliased ? SKFontEdging.Alias : subpixel ? SKFontEdging.SubpixelAntialias : SKFontEdging.Antialias,
                Subpixel = subpixel,
            };
            fonts[key] = font;
            return font;
        }

        internal void MeasureText(SKTypeface primary, string text, int size, bool aliased, SKPaint paint, out SKRect bounds, bool subpixel = false)
        {
            bounds = SKRect.Empty;
            var penX = 0f;
            foreach (var run in GetRuns(primary, text))
            {
                var font = GetFont(run.Typeface, size, aliased, subpixel);
                var advance = font.MeasureText(run.Text, out var runBounds, paint);
                // 排版盒保留空格的推进宽度和字体行高，同时容纳越出行盒的字形。
                // 仅使用墨迹范围会丢掉首尾留白，且让基线随字符串内容变化。
                font.GetFontMetrics(out var metrics);
                var layoutBounds = new SKRect(0, metrics.Ascent, advance, metrics.Descent);
                runBounds = runBounds.IsEmpty ? layoutBounds : SKRect.Union(layoutBounds, runBounds);
                if (!runBounds.IsEmpty)
                {
                    runBounds.Offset(penX, 0);
                    bounds = bounds.IsEmpty ? runBounds : SKRect.Union(bounds, runBounds);
                }
                penX += advance;
            }
        }

        internal void ClearFonts()
        {
            foreach (var font in fonts.Values)
                font.Dispose();
            fonts.Clear();
        }

        internal void Clear()
        {
            ClearFonts();
            cachedRuns.Clear();
            runOrder.Clear();
            resolvedTypefaces.Clear();
            foreach (var typeface in fallbackTypefaces)
                typeface.Dispose();
            fallbackTypefaces.Clear();
        }

        public void Dispose() => Clear();
    }
}
