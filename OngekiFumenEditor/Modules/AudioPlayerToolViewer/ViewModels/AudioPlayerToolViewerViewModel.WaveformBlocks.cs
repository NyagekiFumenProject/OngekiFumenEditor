using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using OngekiFumenEditor.Kernel.Graphics.DrawCommands.DefaultDrawCommands;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing;
using OngekiFumenEditor.Modules.AudioPlayerToolViewer.Graphics.WaveformDrawing.DefaultImpls;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.AudioPlayerToolViewer.ViewModels
{
    public partial class AudioPlayerToolViewerViewModel
    {
        private const int WaveformBlockLogicalWidth = 2048;   // 逻辑像素（固定块宽）
        private const int WaveformBlockBleed = 1;             // 图块左右各多烘 1 逻辑像素，避免块边界把折线笔画裁掉一半
        private const int MaxCachedWaveformBlocks = 6;        // 缓存上限
        private const int BlockPrefetchRadius = 1;            // 前后各预取 1 块
        private const int MaxBlockRendersPerFrame = 1;        // 每帧最多烘一块
        private const int MaxWaveformLineWidth = 24;          // 波形线宽设置上界（与 DefaultWaveformDrawing 一致）

        // 波形视图的清屏色，同时也是图块烘焙的底色（读自 DefaultWaveformSettings.WaveformBackgroundColor）。
        // 图块不透明，贴回时是 1:1 直拷。
        // GL 的直线着色器输出「直色 + 覆盖率放在 alpha」，而整条管线用的是 SrcAlpha/OneMinusSrcAlpha 混合，
        // 把这种内容画进透明底会得到 rgb=c*α、alpha=α²（贴回时又被乘一次 α），抗锯齿与重叠笔画的覆盖率会塌掉；
        // 直接烘在当前视图底色上则与实时绘制完全一致，两个后端都不再有 alpha 约定问题。
        private static Vector4 WaveformViewCleanColor => Properties.DefaultWaveformSettings.Default.WaveformBackgroundColor.ToVector4();

        /// <summary>波形本体折线颜色（DefaultWaveformSettings.WaveformFillColor），烘焙与边界补绘共用。</summary>
        private static Vector4 WaveformPolylineColor => Properties.DefaultWaveformSettings.Default.WaveformFillColor.ToVector4();

        /// <summary>波形本体线宽（AudioPlayerToolViewerSetting.WaveformBodyLineWidth，clamp 到 1–24）。</summary>
        private static int WaveformBodyLineWidth => Math.Clamp(Properties.AudioPlayerToolViewerSetting.Default.WaveformBodyLineWidth, 1, MaxWaveformLineWidth);

        /// <summary>
        /// 是否启用离屏分块预渲染（设置页可关）。关闭时不再烘焙/贴回图块，整帧回退实时绘制，
        /// 且 VM 会订阅该设置变化以释放已缓存图块。
        /// </summary>
        private static bool EnableWaveformBlockPrerender => Properties.AudioPlayerToolViewerSetting.Default.EnableWaveformBlockPrerender;

        // 图块缓存与作废图块都由 waveformBlocksLock 保护：失效可能来自 UI 事件线程，烘焙结果来自异步续体。
        private readonly object waveformBlocksLock = new();
        private readonly Dictionary<int, IImage> waveformBlocks = new();
        private readonly List<IImage> pendingBlockDisposal = new();
        private readonly List<IImage> visibleWaveformBlockList = new(4);
        private readonly TextureInstance[] waveformBlockInstance = new TextureInstance[1];
        private readonly List<LineVertex> cachedLeftMarkerList = new(2);
        private readonly List<LineVertex> cachedTrailingMarkerList = new(3);

        private IOffscreenRenderContext blockRenderOffscreenContext;
        private int waveformBlockGeneration;
        private int waveformVisibleBlockFrom;
        private int waveformVisibleBlockTo;
        private bool blockRenderInFlight;
        private bool waveformRenderActive;                    // 控件 Loaded 置 true、UnLoaded 置 false
        private bool waveformBlocksDisposed;

        /// <summary>单个图块覆盖的时间跨度(ms)。</summary>
        private float WaveformBlockSpanMs => WaveformBlockLogicalWidth * DurationMsPerPixel;

        /// <summary>
        /// 波形折线当前是否应该显示：面板开关 + 绘制实现自身的开关。
        /// 只有默认实现有「隐藏波形本体」这个选项，其余实现一律视为显示。
        /// </summary>
        private bool IsWaveformPolylineVisible
        {
            get
            {
                if (!IsShowWaveform)
                    return false;
                return WaveformDrawing?.Options is not DefaultWaveformOption option || option.ShowWaveform;
            }
        }

        /// <summary>计算可见时间范围覆盖的图块下标范围（与绘制侧使用同一套公式）。</summary>
        private static (int from, int to) GetWaveformBlockRange(TimeSpan fromTime, TimeSpan toTime, float spanMs)
        {
            // 时长/像素异常时退化为空范围，视图走回退路径，仍与改造前一致。
            if (!float.IsFinite(spanMs) || spanMs <= 0)
                return (0, -1);

            var iFrom = Math.Max(0, (int)MathF.Floor((float)(fromTime.TotalMilliseconds / spanMs)));
            var iTo = (int)MathF.Floor((float)(toTime.TotalMilliseconds / spanMs));
            return (iFrom, iTo);
        }

        /// <summary>释放图块缓存与在途烘焙（VM 销毁时调用）。</summary>
        private void DisposeWaveformBlocks()
        {
            waveformRenderActive = false;
            waveformBlocksDisposed = true;

            DisposeWaveformRenderLoop();

            DetachWaveformSettingsEvents();

            InvalidateWaveformBlocks();
            FlushPendingBlockDisposal();

            // 在途烘焙的离屏上下文必须结束：控件不再渲染后，GL 后端不会再有帧来 drain 挂起的请求。
            var inFlight = blockRenderOffscreenContext;
            blockRenderOffscreenContext = null;
            inFlight?.Dispose();
        }

        /// <summary>把全部缓存图块标记为作废（缩放、尺寸、音频、峰值数据等变化时调用）。</summary>
        private void InvalidateWaveformBlocks()
        {
            lock (waveformBlocksLock)
            {
                foreach (var image in waveformBlocks.Values)
                    pendingBlockDisposal.Add(image);
                waveformBlocks.Clear();
            }

            blockRenderInFlight = false;
            waveformBlockGeneration++;
            Log.LogDebug($"[Waveform] blocks invalidated, generation={waveformBlockGeneration}");
        }

        /// <summary>
        /// 统一释放上一帧标记作废的图块。此时它们不可能再被在途的命令列表引用
        /// （上一帧提交的列表已经在上一 tick 被 present 并释放）。
        /// </summary>
        private void FlushPendingBlockDisposal()
        {
            List<IImage> disposal = null;
            lock (waveformBlocksLock)
            {
                if (pendingBlockDisposal.Count > 0)
                {
                    disposal = new List<IImage>(pendingBlockDisposal);
                    pendingBlockDisposal.Clear();
                }
            }

            if (disposal is null)
                return;

            foreach (var image in disposal)
                image.Dispose();
        }

        /// <summary>可见图块是否全部就绪；就绪时 <paramref name="blocks"/> 为按时间升序的图块（复用实例，调用方不得保存）。</summary>
        private bool TryGetVisibleWaveformBlocks(int iFrom, int iTo, out List<IImage> blocks)
        {
            blocks = visibleWaveformBlockList;

            lock (waveformBlocksLock)
            {
                for (var i = iFrom; i <= iTo; i++)
                    if (!waveformBlocks.ContainsKey(i))
                        return false;

                visibleWaveformBlockList.Clear();
                for (var i = iFrom; i <= iTo; i++)
                    visibleWaveformBlockList.Add(waveformBlocks[i]);
            }

            return true;
        }

        /// <summary>把图块贴回视口：按时间偏移平移，逻辑尺寸 1:1（纹理实例以中心定位）。</summary>
        private void DrawWaveformBlocks(IDrawCommandListBuilder builder, List<IImage> blocks, int iFrom, TimeSpan fromTime, TimeSpan toTime)
        {
            var durationMs = (toTime - fromTime).TotalMilliseconds;
            var spanMs = WaveformBlockSpanMs;
            // 烘焙时左右各多 1px，贴回时同样加宽；中心不变，因此两块重叠 2px，后贴的覆盖先贴的（几何一致）。
            var bakedWidth = WaveformBlockLogicalWidth + 2 * WaveformBlockBleed;

            for (var i = 0; i < blocks.Count; i++)
            {
                var blockIndex = iFrom + i;
                var xLeft = WaveformGeometry.ProjectX(TimeSpan.FromMilliseconds(blockIndex * spanMs), fromTime, durationMs, viewWidth);

                waveformBlockInstance[0] = new TextureInstance(
                    new Vector2(bakedWidth, viewHeight),
                    new Vector2(xLeft + WaveformBlockLogicalWidth / 2, 0),
                    0f, Vector4.One);

                builder.DrawTexture(blocks[i], waveformBlockInstance);
            }
        }

        /// <summary>补绘被图块裁掉的边界几何：左边界标记，以及末端竖线 + 右边界标记。</summary>
        private void DrawWaveformEdgeMarkers(IDrawCommandListBuilder builder, PeakPointCollection peakData, TimeSpan fromTime, TimeSpan toTime)
        {
            cachedLeftMarkerList.Clear();
            cachedTrailingMarkerList.Clear();
            WaveformGeometry.BuildEdgeMarkers(cachedLeftMarkerList, cachedTrailingMarkerList, peakData, fromTime, toTime,
                viewWidth, viewHeight, WaveformPolylineColor, WaveformGeometry.DefaultEdgeMarkerColor);

            // 与实时绘制同一作用域：标记里的末端竖线带 y 分量，必须同样经过竖直缩放。
            builder.PushModelMatrix(Matrix4x4.CreateScale(1, WaveformVecticalScale, 1f));
            if (cachedLeftMarkerList.Count > 1)
                builder.DrawSimpleLines(cachedLeftMarkerList, WaveformBodyLineWidth);
            if (cachedTrailingMarkerList.Count > 1)
                builder.DrawSimpleLines(cachedTrailingMarkerList, WaveformBodyLineWidth);
            builder.PopModelMatrix();
        }

        /// <summary>每帧调度：在可见范围±预取半径内按「距当前视图由近及远」烘一块缺失图块。</summary>
        private void ScheduleWaveformBlockRender(int iFrom, int iTo)
        {
            if (!waveformRenderActive || usingPeakData is null || !IsWaveformPolylineVisible || !EnableWaveformBlockPrerender)
                return;

            var spanMs = WaveformBlockSpanMs;
            var peakData = usingPeakData;

            for (var renders = 0; renders < MaxBlockRendersPerFrame; renders++)
            {
                if (blockRenderInFlight)
                    return;

                var index = FindNearestMissingWaveformBlock(iFrom, iTo);
                if (index < 0)
                    return;

                blockRenderInFlight = true;
                _ = RenderWaveformBlockAsync(index, spanMs, peakData);
            }
        }

        /// <summary>在可见范围±预取半径内找距当前视图最近的缺失图块，没有则返回 -1。</summary>
        private int FindNearestMissingWaveformBlock(int iFrom, int iTo)
        {
            var rangeFrom = Math.Max(0, iFrom - BlockPrefetchRadius);
            var rangeTo = iTo + BlockPrefetchRadius;

            var bestIndex = -1;
            var bestDistance = int.MaxValue;

            lock (waveformBlocksLock)
            {
                for (var i = rangeFrom; i <= rangeTo; i++)
                {
                    if (waveformBlocks.ContainsKey(i))
                        continue;

                    var distance = i < iFrom ? iFrom - i : i > iTo ? i - iTo : 0;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }
            }

            return bestIndex;
        }

        /// <summary>离屏烘一块波形图块；失败只记日志、不写缓存，视图继续走整帧回退路径。</summary>
        private async Task RenderWaveformBlockAsync(int index, float spanMs, PeakPointCollection peakData)
        {
            var generation = waveformBlockGeneration;
            IOffscreenRenderContext offscreen = null;

            try
            {
                var startMs = index * spanMs;
                var bleedMs = WaveformBlockBleed * DurationMsPerPixel;
                var logicalWidth = WaveformBlockLogicalWidth + 2 * WaveformBlockBleed;   // 左右各多 1px，保证边界笔画不被裁掉
                var deviceWidth = Math.Max(1, (int)MathF.Ceiling(logicalWidth * renderScaleX));
                var deviceHeight = Math.Max(1, (int)MathF.Ceiling(viewHeight * renderScaleY));

                offscreen = renderImpl.CreateOffscreenToImage(new OffscreenRenderOptions
                {
                    Width = deviceWidth,
                    Height = deviceHeight,
                });
                blockRenderOffscreenContext = offscreen;

                using var builder = renderImpl.CreateDrawCommandListBuilder();
                builder.SetCleanColor(WaveformViewCleanColor);                        // 与视图同底色：图块不透明，内容与实时绘制一致
                builder.SetViewport(logicalWidth, viewHeight, renderScaleX, renderScaleY);
                builder.SetCurrentViewMatrix(Matrix4x4.Identity);
                builder.SetCurrentProjectionMatrix(Matrix4x4.CreateOrthographic(logicalWidth, viewHeight, -1, 1));
                builder.SetCurrentRect(new VisibleRect(new Vector2(logicalWidth, 0), new Vector2(0, viewHeight)));
                builder.SetCurrentModelMatrix(Matrix4x4.Identity);

                var points = new List<LineVertex>(4096);                               // 本方法私有
                builder.PushModelMatrix(Matrix4x4.CreateScale(1, WaveformVecticalScale, 1f));
                WaveformGeometry.BuildBody(points, peakData,
                    TimeSpan.FromMilliseconds(startMs - bleedMs), TimeSpan.FromMilliseconds(startMs + spanMs + bleedMs),
                    logicalWidth, viewHeight, rangeMarginPoints: 1, WaveformPolylineColor);   // 左右各多画 1 个峰点，消除块缝断线
                if (points.Count > 0)
                    builder.DrawSimpleLines(points, WaveformBodyLineWidth);
                builder.PopModelMatrix();

                var list = builder.GetDrawCommandList();
                var image = await offscreen.RenderToImageAsync(list, autoDispose: true);

                if (generation != waveformBlockGeneration || waveformBlocksDisposed)
                {
                    // 代数已变，结果作废；已释放时立即丢弃，否则交给下一帧统一释放。
                    if (waveformBlocksDisposed)
                        image.Dispose();
                    else
                        lock (waveformBlocksLock)
                            pendingBlockDisposal.Add(image);
                    return;
                }

                lock (waveformBlocksLock)
                    waveformBlocks[index] = image;

                TrimWaveformBlocks();
                Log.LogDebug($"[Waveform] block #{index} baked ({deviceWidth}x{deviceHeight}) generation={generation}");
            }
            catch (Exception ex)
            {
                // 不写缓存 => 下一帧自然重试，视图继续走回退路径，不崩溃。
                Log.LogError($"[Waveform] bake block #{index} failed: {ex}");
            }
            finally
            {
                if (ReferenceEquals(blockRenderOffscreenContext, offscreen))
                {
                    blockRenderOffscreenContext = null;
                    blockRenderInFlight = false;
                }

                offscreen?.Dispose();
            }
        }

        /// <summary>超出缓存上限时，按「距当前可见块由远及近」淘汰。</summary>
        private void TrimWaveformBlocks()
        {
            lock (waveformBlocksLock)
            {
                while (waveformBlocks.Count > MaxCachedWaveformBlocks)
                {
                    var farthestIndex = -1;
                    var farthestDistance = int.MinValue;

                    foreach (var index in waveformBlocks.Keys)
                    {
                        var distance = index < waveformVisibleBlockFrom ? waveformVisibleBlockFrom - index
                            : index > waveformVisibleBlockTo ? index - waveformVisibleBlockTo
                            : 0;

                        if (distance > farthestDistance)
                        {
                            farthestDistance = distance;
                            farthestIndex = index;
                        }
                    }

                    if (farthestIndex < 0 || !waveformBlocks.Remove(farthestIndex, out var image))
                        break;

                    pendingBlockDisposal.Add(image);
                }
            }
        }
    }
}
