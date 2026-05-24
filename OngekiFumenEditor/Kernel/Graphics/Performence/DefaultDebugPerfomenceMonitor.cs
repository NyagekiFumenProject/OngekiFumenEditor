using OngekiFumenEditor.Kernel.Graphics.DrawCommands;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor;
using static OngekiFumenEditor.Kernel.Graphics.IPerfomenceMonitor.ICategorizedPerformenceStatisticsData;

namespace OngekiFumenEditor.Kernel.Graphics.Performence
{
#if DEBUG
	[Export(typeof(IPerfomenceMonitor))]
#endif
	[PartCreationPolicy(CreationPolicy.NonShared)]
	public sealed class DefaultDebugPerfomenceMonitor : IPerfomenceMonitor
	{
		private const int RECORD_LENGTH = 165;

		private sealed class SampleWindow
		{
			private readonly long[] values = new long[RECORD_LENGTH];
			private int index;
			private int count;
			private long sum;

			public double Average => count == 0 ? 0 : (double)sum / count;

			public long Max
			{
				get
				{
					if (count == 0)
						return 0;

					var max = values[0];
					for (var i = 1; i < count; i++)
						max = Math.Max(max, values[i]);
					return max;
				}
			}

			public void Enqueue(long value)
			{
				if (count == values.Length)
					sum -= values[index];
				else
					count++;

				values[index] = value;
				sum += value;

				index = (index + 1) % values.Length;
			}

			public void Clear()
			{
				Array.Clear(values, 0, count);
				index = 0;
				count = 0;
				sum = 0;
			}
		}

		private sealed class CategoryPerformenceData
		{
			private long beginTimestamp;

			public string Name { get; init; } = string.Empty;

			public long CurrentFrameSpendTicks { get; private set; }

			public SampleWindow SpendTicks { get; } = new();

			public void Begin()
			{
				beginTimestamp = Stopwatch.GetTimestamp();
			}

			public bool TryEnd()
			{
				if (beginTimestamp == 0)
					return false;

				CurrentFrameSpendTicks += Stopwatch.GetElapsedTime(beginTimestamp).Ticks;
				beginTimestamp = 0;
				return true;
			}

			public void ResetCurrent()
			{
				beginTimestamp = 0;
				CurrentFrameSpendTicks = 0;
			}

			public void ClearAll()
			{
				SpendTicks.Clear();
				ResetCurrent();
			}
		}

		private sealed class CategorizedPerformenceStatisticsData : ICategorizedPerformenceStatisticsData
		{
			public List<PerformenceItem> PerformenceRankList { get; set; }

			public IEnumerable<PerformenceItem> PerformenceRanks => PerformenceRankList ?? Enumerable.Empty<PerformenceItem>();

			public double AveSpendTicks { get; set; }

			public double MostSpendTicks { get; set; }

		}

		private readonly Dictionary<Type, CategoryPerformenceData> drawCommandDataMap = new();
		private readonly Dictionary<Type, CategoryPerformenceData> drawTargetDataMap = new();

		private readonly SampleWindow onRenderSpendTicks = new();
		private readonly SampleWindow presentSpendTicks = new();
		private readonly SampleWindow totalDrawCall = new();

		private long renderBeginTimestamp;
		private long presentBeginTimestamp;
		private long currentOnRenderSpendTicks;
		private long currentPresentSpendTicks;
		private int currentDrawCall;
		private bool isRendering;
		private bool isPresenting;

		private static CategoryPerformenceData GetCategoryPerformenceData(Dictionary<Type, CategoryPerformenceData> map, Type type)
		{
			if (map.TryGetValue(type, out var data))
				return data;

			data = new CategoryPerformenceData { Name = type.Name };
			map[type] = data;
			return data;
		}

		public void OnBeforeRender()
		{
			foreach (var data in drawTargetDataMap.Values)
				data.ResetCurrent();

			currentOnRenderSpendTicks = 0;
			renderBeginTimestamp = Stopwatch.GetTimestamp();
			isRendering = true;
		}

		public void OnAfterRender()
		{
			if (!isRendering)
				return;

			currentOnRenderSpendTicks = Stopwatch.GetElapsedTime(renderBeginTimestamp).Ticks;
			onRenderSpendTicks.Enqueue(currentOnRenderSpendTicks);

			foreach (var data in drawTargetDataMap.Values)
			{
				if (data.CurrentFrameSpendTicks > 0)
					data.SpendTicks.Enqueue(data.CurrentFrameSpendTicks);
			}

			isRendering = false;
		}

		public void OnBeforePresent()
		{
			foreach (var data in drawCommandDataMap.Values)
				data.ResetCurrent();

			currentDrawCall = 0;
			currentPresentSpendTicks = 0;
			presentBeginTimestamp = Stopwatch.GetTimestamp();
			isPresenting = true;
		}

		public void OnAfterPresent()
		{
			if (!isPresenting)
				return;

			currentPresentSpendTicks = Stopwatch.GetElapsedTime(presentBeginTimestamp).Ticks;
			presentSpendTicks.Enqueue(currentPresentSpendTicks);
			totalDrawCall.Enqueue(currentDrawCall);

			foreach (var data in drawCommandDataMap.Values)
			{
				if (data.CurrentFrameSpendTicks > 0)
					data.SpendTicks.Enqueue(data.CurrentFrameSpendTicks);
			}

			isPresenting = false;
		}

		public void OnBeginDrawCommand(DrawCommand command)
		{
			if (command is null)
				return;

			GetCategoryPerformenceData(drawCommandDataMap, command.GetType()).Begin();
		}

		public void OnAfterDrawCommand(DrawCommand command)
		{
			if (command is null)
				return;

			GetCategoryPerformenceData(drawCommandDataMap, command.GetType()).TryEnd();
		}

		public void OnBeginTargetDrawing(IDrawingTarget target)
		{
			if (target is null)
				return;

			GetCategoryPerformenceData(drawTargetDataMap, target.GetType()).Begin();
		}

		public void OnAfterTargetDrawing(IDrawingTarget target)
		{
			if (target is null)
				return;

			GetCategoryPerformenceData(drawTargetDataMap, target.GetType()).TryEnd();
		}

		public void CountDrawCall()
		{
			currentDrawCall++;
		}

		public void Clear()
		{
			drawCommandDataMap.Clear();
			drawTargetDataMap.Clear();
			onRenderSpendTicks.Clear();
			presentSpendTicks.Clear();
			totalDrawCall.Clear();
			currentOnRenderSpendTicks = 0;
			currentPresentSpendTicks = 0;
			currentDrawCall = 0;
			isRendering = false;
			isPresenting = false;
		}

		private static ICategorizedPerformenceStatisticsData StatisticsPerformenceData(IEnumerable<CategoryPerformenceData> dataEnumerable)
		{
			var dataList = dataEnumerable.ToList();
			if (dataList.Count == 0)
				return new CategorizedPerformenceStatisticsData();

			var list = dataList
				.Select(x => new PerformenceItem(x.Name, x.SpendTicks.Average))
				.OrderByDescending(x => x.AveSpendTicks)
				.ToList();

			return new CategorizedPerformenceStatisticsData()
			{
				AveSpendTicks = dataList.Select(x => x.SpendTicks.Average).Average(),
				MostSpendTicks = dataList.Select(x => x.SpendTicks.Max).DefaultIfEmpty().Max(),
				PerformenceRankList = list
			};
		}

		public ICategorizedPerformenceStatisticsData GetDrawCommandPerformenceData()
		{
			return StatisticsPerformenceData(drawCommandDataMap.Values);
		}

		public ICategorizedPerformenceStatisticsData GetDrawingTargetPerformenceData()
		{
			return StatisticsPerformenceData(drawTargetDataMap.Values);
		}

		public IRenderPerformenceStatisticsData GetRenderPerformenceData()
		{
			var aveOnRenderSpendTicks = onRenderSpendTicks.Average;
			var avePresentSpendTicks = presentSpendTicks.Average;

			return new RenderPerformenceStatisticsData()
			{
				CurrentOnRenderSpendTicks = currentOnRenderSpendTicks,
				AveOnRenderSpendTicks = aveOnRenderSpendTicks,
				AveOnRenderFps = ToFps(aveOnRenderSpendTicks),
				CurrentPresentSpendTicks = currentPresentSpendTicks,
				AvePresentSpendTicks = avePresentSpendTicks,
				AvePresentFps = ToFps(avePresentSpendTicks),
				AveDrawCall = totalDrawCall.Average
			};
		}

		public void FormatStatistics(StringBuilder builder)
		{
			var command = GetDrawCommandPerformenceData();
			var drawingTarget = GetDrawingTargetPerformenceData();
			var render = GetRenderPerformenceData();

			string formatFPS(double fps) => $"{fps,7:0.00}";
			string formatMSec(double ticks) => $"{TimeSpan.FromTicks((long)Math.Max(0, ticks)).TotalMilliseconds:F2}";

			void commandItem(PerformenceItem p, int i)
			{
				if (p is null)
					return;

				builder.AppendLine($"CMD.TOP{i}:{p.Name} {formatMSec(p.AveSpendTicks)}ms avg");
			}

			void targetItem(PerformenceItem p, int i)
			{
				if (p is null)
					return;

				builder.AppendLine($"DT.TOP{i}:{p.Name} {formatMSec(p.AveSpendTicks)}ms avg");
			}

			builder.AppendLine($"OnRender FPS avg:{formatFPS(render.AveOnRenderFps)} ({formatMSec(render.CurrentOnRenderSpendTicks)}ms/{formatMSec(render.AveOnRenderSpendTicks)}ms avg)");
			builder.AppendLine($"Present  FPS avg:{formatFPS(render.AvePresentFps)} ({formatMSec(render.CurrentPresentSpendTicks)}ms/{formatMSec(render.AvePresentSpendTicks)}ms avg)");
			builder.AppendLine($"DrawCall avg:{render.AveDrawCall,6:F1}");
			builder.AppendLine();
			commandItem(command.PerformenceRanks.ElementAtOrDefault(0), 1);
			commandItem(command.PerformenceRanks.ElementAtOrDefault(1), 2);
			commandItem(command.PerformenceRanks.ElementAtOrDefault(2), 3);
			builder.AppendLine();
			targetItem(drawingTarget.PerformenceRanks.ElementAtOrDefault(0), 1);
			targetItem(drawingTarget.PerformenceRanks.ElementAtOrDefault(1), 2);
			targetItem(drawingTarget.PerformenceRanks.ElementAtOrDefault(2), 3);
		}

		private static double ToFps(double ticks)
		{
			return ticks > 0 ? TimeSpan.TicksPerSecond / ticks : 0;
		}
	}
}
