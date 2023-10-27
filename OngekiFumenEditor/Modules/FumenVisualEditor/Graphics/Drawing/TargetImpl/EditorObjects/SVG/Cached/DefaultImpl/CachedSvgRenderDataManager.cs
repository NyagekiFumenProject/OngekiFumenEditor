using Caliburn.Micro;
using OngekiFumenEditor.Base.EditorObjects.Svg;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static OngekiFumenEditor.Kernel.Graphics.ILineDrawing;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Graphics.Drawing.TargetImpl.EditorObjects.SVG.Cached.DefaultImpl
{
	[Export(typeof(ICachedSvgRenderDataManager))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class CachedSvgRenderDataManager : ISchedulable, ICachedSvgRenderDataManager
	{
		private class CachedSvgGeneratedData
		{
			public SvgPrefabBase SvgPrefab { get; set; }
			public List<LineVertex> GeneratedPoints { get; set; }
			public int SvgGeometryHashCode { get; set; } = int.MaxValue;
			public Vector2 ViewSize { get; set; } = new(int.MinValue);
			public DateTime LastAccessTime { get; set; }
			public IDrawingContext Target { get; set; }
			public Rect Bound { get; set; }

			public void CleanPoints()
			{
				ObjectPool<List<LineVertex>>.Return(GeneratedPoints);
				GeneratedPoints = default;
			}
		}

		private Dictionary<SvgPrefabBase, CachedSvgGeneratedData> cachedDataMap = new();

		public string SchedulerName { get; } = "CachedSvgManager";

		public TimeSpan ScheduleCallLoopInterval { get; } = TimeSpan.FromSeconds(30);

		public CachedSvgRenderDataManager()
		{
			IoC.Get<ISchedulerManager>().AddScheduler(this);
		}

		public Task OnScheduleCall(CancellationToken cancellationToken)
		{
			var curTime = DateTime.Now;
			foreach (var removeItem in cachedDataMap.Where(x => x.Value.LastAccessTime - curTime > TimeSpan.FromMinutes(10)).ToArray())
				cachedDataMap.Remove(removeItem.Key);
			return Task.CompletedTask;
		}

		private bool CheckCachedDataVailed(IDrawingContext target, CachedSvgGeneratedData data)
		{
			if (!(data.SvgPrefab?.ProcessingDrawingGroup?.GetHashCode() is int curHash && curHash == data.SvgGeometryHashCode))
				return false;

			if (new Vector2(target.ViewWidth, target.ViewHeight) != data.ViewSize)
				return false;

			return true;
		}

		public void OnSchedulerTerm()
		{

		}

		private List<LineVertex> GenerateLineVertexData(SvgPrefabBase svgPrefab)
		{
			var list = ObjectPool<List<LineVertex>>.Get();
			list.Clear();

			var segments = svgPrefab.GenerateLineSegments();

			foreach (var seg in segments)
			{
				var color = new Vector4(seg.Color.R / 255.0f, seg.Color.G / 255.0f, seg.Color.B / 255.0f, seg.Color.A / 255.0f);

				var itor = seg.RelativePoints.GetEnumerator();
				if (itor.MoveNext())
				{
					var point = itor.Current;
					list.Add(new(point, Vector4.Zero, VertexDash.Solider));
					list.Add(new(point, color, VertexDash.Solider));
					while (itor.MoveNext())
					{
						point = itor.Current;
						list.Add(new(point, color, VertexDash.Solider));
					}
					list.Add(new(point, Vector4.Zero, VertexDash.Solider));
				}
			}

			return list;
		}

		public List<LineVertex> GetRenderData(IDrawingContext target, SvgPrefabBase svgPrefab, out bool isCached, out Rect bound)
		{
			var curTime = DateTime.Now;
			isCached = true;

			if (!cachedDataMap.TryGetValue(svgPrefab, out var cachedItem))
			{
				cachedItem = new CachedSvgGeneratedData();
				cachedItem.SvgPrefab = svgPrefab;
				cachedItem.Target = target;
				cachedDataMap[svgPrefab] = cachedItem;
				isCached = false;
			}

			if (!CheckCachedDataVailed(target, cachedItem))
			{
				cachedItem.CleanPoints();
				var genData = GenerateLineVertexData(svgPrefab);
				cachedItem.SvgGeometryHashCode = svgPrefab.ProcessingDrawingGroup?.GetHashCode() ?? MathUtils.Random(int.MinValue, int.MaxValue);
				cachedItem.GeneratedPoints = genData;
				cachedItem.ViewSize = new Vector2(target.ViewWidth, target.ViewHeight);
				cachedItem.Bound = svgPrefab.ProcessingDrawingGroup?.Bounds ?? default;
				isCached = false;
			}

			//update hashcode and access time
			cachedItem.LastAccessTime = curTime;
			bound = cachedItem.Bound;

			return cachedItem.GeneratedPoints;
		}
	}
}
