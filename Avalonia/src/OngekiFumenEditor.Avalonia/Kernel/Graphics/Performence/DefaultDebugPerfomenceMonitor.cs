using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using Injectio.Attributes;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Avalonia.Kernel.Graphics.IPerfomenceMonitor.IDrawingPerformenceStatisticsData;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence
{
#if DEBUG
	[RegisterTransient<IPerfomenceMonitor>]
#endif
	public class DefaultDebugPerfomenceMonitor : IPerfomenceMonitor
	{
		const int RECORD_LENGTH = 165;

		private sealed class LockFreeSampleBuffer
		{
			private readonly long[] values;
			private readonly long[] sequenceNumbers;
			private readonly int capacity;
			private long nextSequence;

			public LockFreeSampleBuffer(int capacity)
			{
				this.capacity = capacity;
				values = new long[capacity];
				sequenceNumbers = new long[capacity];
				Array.Fill(sequenceNumbers, -1L);
			}

			public void Enqueue(long value)
			{
				var sequence = Interlocked.Increment(ref nextSequence) - 1;
				var index = (int)(sequence % capacity);

				// Invalidate the slot before replacing its value; readers discard an in-flight slot.
				Volatile.Write(ref sequenceNumbers[index], long.MinValue);
				Volatile.Write(ref values[index], value);
				Volatile.Write(ref sequenceNumbers[index], sequence);
			}

			public long[] Snapshot()
			{
				var end = Volatile.Read(ref nextSequence);
				if (end <= 0)
					return Array.Empty<long>();

				var count = (int)Math.Min(end, capacity);
				var start = end - count;
				var snapshot = new long[count];
				var actualCount = 0;

				for (var sequence = start; sequence < end; sequence++)
				{
					var index = (int)(sequence % capacity);
					var sequenceBefore = Volatile.Read(ref sequenceNumbers[index]);
					var value = Volatile.Read(ref values[index]);
					var sequenceAfter = Volatile.Read(ref sequenceNumbers[index]);

					if (sequenceBefore == sequence && sequenceAfter == sequence)
						snapshot[actualCount++] = value;
				}

				if (actualCount != snapshot.Length)
					Array.Resize(ref snapshot, actualCount);

				return snapshot;
			}
		}

		private class DrawingPerformenceData
		{
			public string Name { get; init; }
			private LockFreeSampleBuffer drawingSpendTicks = new(RECORD_LENGTH);
			private LockFreeSampleBuffer drawCall = new(RECORD_LENGTH);
			private int drawCallCount;
			private long onBeginDrawingTicks;

			public long OnBeginDrawingTicks => Volatile.Read(ref onBeginDrawingTicks);

			public void SetOnBeginDrawingTicks(long value) => Volatile.Write(ref onBeginDrawingTicks, value);

			public void IncrementDrawCall() => Interlocked.Increment(ref drawCallCount);

			public void RecordDrawing(long spendTicks)
			{
				Volatile.Read(ref drawingSpendTicks).Enqueue(spendTicks);
				Volatile.Read(ref drawCall).Enqueue(Volatile.Read(ref drawCallCount));
			}

			public void RecordTargetDrawing(long spendTicks) => Volatile.Read(ref drawingSpendTicks).Enqueue(spendTicks);

			public (long[] SpendTicks, long[] DrawCalls) Snapshot() =>
				(Volatile.Read(ref drawingSpendTicks).Snapshot(), Volatile.Read(ref drawCall).Snapshot());

			public void ClearAll()
			{
				// Swap only the sample windows; an active render pair may still use the timestamp and count.
				Interlocked.Exchange(ref drawingSpendTicks, new LockFreeSampleBuffer(RECORD_LENGTH));
				Interlocked.Exchange(ref drawCall, new LockFreeSampleBuffer(RECORD_LENGTH));
			}

			public void ResetDrawCallCount() => Interlocked.Exchange(ref drawCallCount, 0);
		}

		private class DrawingTargetPerformenceData : DrawingPerformenceData
		{

		}

		private ImmutableDictionary<IDrawing, DrawingPerformenceData> drawDataMap = ImmutableDictionary<IDrawing, DrawingPerformenceData>.Empty;
		private ImmutableDictionary<IDrawingTarget, DrawingTargetPerformenceData> drawTargetDataMap = ImmutableDictionary<IDrawingTarget, DrawingTargetPerformenceData>.Empty;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private DrawingPerformenceData GetDrawingPerformenceData(IDrawing d) => GetOrAdd(ref drawDataMap, d, static key => new DrawingPerformenceData() { Name = key.GetType().Name });

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private DrawingTargetPerformenceData GetDrawingTargetPerformenceData(IDrawingTarget d) => GetOrAdd(ref drawTargetDataMap, d, static key => new DrawingTargetPerformenceData() { Name = key.GetType().Name });

		private static TValue GetOrAdd<TKey, TValue>(ref ImmutableDictionary<TKey, TValue> map, TKey key, Func<TKey, TValue> valueFactory)
			where TKey : notnull
			where TValue : class
		{
			while (true)
			{
				var current = Volatile.Read(ref map);
				if (current.TryGetValue(key, out var existing))
					return existing;

				var created = valueFactory(key);
				var updated = current.Add(key, created);
				if (ReferenceEquals(Interlocked.CompareExchange(ref map, updated, current), current))
					return created;
			}
		}

		private LockFreeSampleBuffer renderSpendTicks = new(RECORD_LENGTH);
		private LockFreeSampleBuffer uiRenderSpendTicks = new(RECORD_LENGTH);
		private LockFreeSampleBuffer totalDrawCall = new(RECORD_LENGTH);
		private long currentDrawCall = 0;
		private long currentBeginRenderTick = 0;

		public void OnBeforeRender()
		{
			Interlocked.Exchange(ref currentDrawCall, 0);
			Volatile.Write(ref currentBeginRenderTick, Stopwatch.GetTimestamp());
		}

		public void OnBeginTargetDrawing(IDrawingTarget drawingTarget)
		{
			var data = GetDrawingTargetPerformenceData(drawingTarget);
			data.SetOnBeginDrawingTicks(Stopwatch.GetTimestamp());
		}

		public void OnBeginDrawing(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			data.SetOnBeginDrawingTicks(Stopwatch.GetTimestamp());
		}

		public void CountDrawCall(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			data.IncrementDrawCall();
			Interlocked.Increment(ref currentDrawCall);
		}

		public void OnAfterDrawing(IDrawing drawing)
		{
			var data = GetDrawingPerformenceData(drawing);
			var tickDiff = Stopwatch.GetTimestamp() - data.OnBeginDrawingTicks;
			data.RecordDrawing(tickDiff);
		}

		public void OnAfterTargetDrawing(IDrawingTarget drawing)
		{
			var data = GetDrawingTargetPerformenceData(drawing);
			var tickDiff = Stopwatch.GetTimestamp() - data.OnBeginDrawingTicks;
			data.RecordTargetDrawing(tickDiff);
		}

		public void OnAfterRender()
		{
			var tickDiff = Stopwatch.GetTimestamp() - Volatile.Read(ref currentBeginRenderTick);
			Volatile.Read(ref renderSpendTicks).Enqueue(tickDiff);
			Volatile.Read(ref totalDrawCall).Enqueue(Volatile.Read(ref currentDrawCall));

			foreach (var data in Volatile.Read(ref drawDataMap).Values)
				data.ResetDrawCallCount();
		}

		public void Clear()
		{
			foreach (var data in Volatile.Read(ref drawDataMap).Values)
				data.ClearAll();

			foreach (var data in Volatile.Read(ref drawTargetDataMap).Values)
				data.ClearAll();
		}

		public struct DrawingPerformenceStatisticsData : IDrawingPerformenceStatisticsData
		{
			public List<PerformenceItem> PerformenceRankList { get; set; }

			public IEnumerable<PerformenceItem> PerformenceRanks => PerformenceRankList ?? Enumerable.Empty<PerformenceItem>();

			public double AveSpendTicks { get; set; }

			public double MostSpendTicks { get; set; }
		}

		private IDrawingPerformenceStatisticsData StatisticsPerformenceData(IEnumerable<DrawingPerformenceData> dataList)
		{
			var snapshots = dataList
				.Select(x => (x.Name, Data: x.Snapshot()))
				.Where(x => x.Data.SpendTicks.Length > 0)
				.ToArray();

			if (snapshots.Length == 0)
				return default;

			var ave = snapshots.Select(x => x.Data.SpendTicks.Average()).Average();
			var most = MostFrequentAverage(snapshots.SelectMany(x => x.Data.SpendTicks));

			var list = snapshots
				.Select(x => new { TotalCost = x.Data.SpendTicks.Sum(), Data = x })
				.OrderByDescending(x => x.TotalCost)
				.Select(x => new PerformenceItem(
					x.Data.Name,
					x.Data.Data.SpendTicks.Average(),
					x.Data.Data.DrawCalls.Length == 0 ? 0 : (int)x.Data.Data.DrawCalls.Average()))
				.ToList();

			return new DrawingPerformenceStatisticsData()
			{
				AveSpendTicks = ave,
				MostSpendTicks = most,
				PerformenceRankList = list
			};
		}

		private static double MostFrequentAverage(IEnumerable<long> values)
		{
			var group = values
				.GroupBy(x => (int)x)
				.OrderByDescending(x => x.Count())
				.FirstOrDefault();

			return group is null ? 0 : group.Average();
		}

		private static long MostFrequentValue(IEnumerable<long> values)
		{
			var group = values
				.GroupBy(x => (int)x)
				.OrderByDescending(x => x.Count())
				.FirstOrDefault();

			return group?.Key ?? 0;
		}

		public IDrawingPerformenceStatisticsData GetDrawingPerformenceData()
		{
			return StatisticsPerformenceData(Volatile.Read(ref drawDataMap).Values);
		}

		public IDrawingPerformenceStatisticsData GetDrawingTargetPerformenceData()
		{
			return StatisticsPerformenceData(Volatile.Read(ref drawTargetDataMap).Values);
		}

		public IRenderPerformenceStatisticsData GetRenderPerformenceData()
		{
			var renderSpendTicks = Volatile.Read(ref this.renderSpendTicks).Snapshot();
			var uiRenderSpendTicks = Volatile.Read(ref this.uiRenderSpendTicks).Snapshot();
			var totalDrawCall = Volatile.Read(ref this.totalDrawCall).Snapshot();

			return new RenderPerformenceStatisticsData()
			{
				AveSpendTicks = renderSpendTicks.Length == 0 ? 0 : renderSpendTicks.Average(),
				AveUIRenderSpendTicks = uiRenderSpendTicks.Length == 0 ? 0 : uiRenderSpendTicks.Average(),
				MostUIRenderSpendTicks = MostFrequentValue(uiRenderSpendTicks),
				MostSpendTicks = MostFrequentValue(renderSpendTicks),
				AveDrawCall = totalDrawCall.Length == 0 ? 0 : (int)totalDrawCall.Average()
			};
		}

		public void PostUIRenderTime(TimeSpan ts)
		{
			Volatile.Read(ref uiRenderSpendTicks).Enqueue(ts.Ticks);
		}

		public void FormatStatistics(StringBuilder builder)
		{
			var drawing = GetDrawingPerformenceData();
			var drawingTarget = GetDrawingTargetPerformenceData();

			if (drawing is null || drawingTarget is null)
				return;

			var drawingTop = drawing.PerformenceRanks.FirstOrDefault();
			var render = GetRenderPerformenceData();

			string formatFPS(double ticks) => $"{1.0 / TimeSpan.FromTicks((int)ticks).TotalSeconds,7:0.00}";
			string formatMSec(double ticks) => $"{TimeSpan.FromTicks((int)ticks).TotalMilliseconds:F2}";

			void dip(PerformenceItem p, int i)
			{
				if (p is null)
					return;
				builder.AppendLine($"D.TOP{i}:{p.Name} {p.AveDrawCall} dc ({formatMSec(p.AveSpendTicks)}ms) ");
			}

			void dipt(PerformenceItem p, int i)
			{
				if (p is null)
					return;
				builder.AppendLine($"DT.TOP{i}:{p.Name} {formatMSec(p.AveSpendTicks)}ms ");
			}

			builder.AppendLine($"UI.FPS:{formatFPS(render.AveUIRenderSpendTicks)}({formatFPS(render.MostUIRenderSpendTicks)}) / R.FPS {formatFPS(render.AveSpendTicks)}({formatFPS(render.MostSpendTicks)}) D.FPS:{formatFPS(drawing.AveSpendTicks)}({formatFPS(drawing.MostSpendTicks)})");
			builder.AppendLine($"DC:{render.AveDrawCall,6} D.Top.DC:{drawingTop.AveDrawCall,6}");
			builder.AppendLine();
			dip(drawing.PerformenceRanks.ElementAtOrDefault(0), 1);
			dip(drawing.PerformenceRanks.ElementAtOrDefault(1), 2);
			dip(drawing.PerformenceRanks.ElementAtOrDefault(2), 3);
			builder.AppendLine();
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(0), 1);
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(1), 2);
			dipt(drawingTarget.PerformenceRanks.ElementAtOrDefault(2), 3);
		}
	}
}



