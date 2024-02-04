using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Parser.DefaultImpl.Ogkr.Rules;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.Ogkr
{
	public static class StandardizeFormat
	{
		public record ProcessTask(bool IsSuccess)
		{
			public string Message { get; set; }
			public OngekiFumen SerializedFumen { get; set; }
		}

		public static async Task<OngekiFumen> CopyFumenObject(OngekiFumen fumen)
		{
			var tmpFilePath = Path.GetTempFileName() + ".ogkr";
			var serializer = IoC.Get<IFumenParserManager>().GetSerializer(tmpFilePath);
			var deserializer = IoC.Get<IFumenParserManager>().GetDeserializer(tmpFilePath);

			await File.WriteAllBytesAsync(tmpFilePath, await serializer.SerializeAsync(fumen));
			using var stream = File.OpenRead(tmpFilePath);
			var newFumen = await deserializer.DeserializeAsync(stream);

			return newFumen;
		}

		public static async Task<ProcessTask> Process(OngekiFumen currentFumen)
		{
			var fumen = await CopyFumenObject(currentFumen);

			var generatedSoflans = fumen.Soflans.GenerateDurationSoflans(fumen.BpmList).ToArray();

			//directly removes objects which not belong to ongeki.
			fumen.SvgPrefabs.Clear();
			fumen.Comments.Clear();
			//interpolate soflans
			fumen.RemoveObjects(fumen.Soflans.OfType<OngekiObjectBase>().ToArray());
			fumen.AddObjects(generatedSoflans);

			if (!CheckFumenIsSerializable(fumen, out var msg))
				return new(false) { Message = msg };

			var laneMap = new Dictionary<ConnectableStartObject, List<ConnectableStartObject>>();
			var curveFactory = XGridLimitedCurveInterpolaterFactory.Default;

			foreach ((var beforeLane, var genLanes) in InterpolateAll.Calculate(fumen, curveFactory))
				laneMap[beforeLane] = genLanes.ToList();

			var curveStarts = laneMap.Keys.ToList();

			var affactObjects = InterpolateAll.CalculateAffectedDockableObjects(fumen, curveStarts).ToArray();

			foreach (var item in laneMap)
			{
				var beforeLane = item.Key;
				var afterLanes = item.Value;

				PostProcessInterpolatedConnectableStart(beforeLane, afterLanes);

				fumen.RemoveObject(beforeLane);
				fumen.AddObjects(afterLanes);
			}

			foreach (var obj in affactObjects)
			{
				var tGrid = obj.TGrid;
				var beforeXGrid = obj.XGrid;
				var beforeLane = obj.ReferenceLaneStart;

				(var afterLane, var afterXGrid) =
					//考虑到处理HoldEnd的refLane之前，已经被前者Hold处理过了
					(obj.ReferenceLaneStart is not null && laneMap.TryGetValue(obj.ReferenceLaneStart, out var genStarts) ? genStarts : Enumerable.Empty<ConnectableStartObject>())
					.Where(x => tGrid >= x.MinTGrid && tGrid <= x.MaxTGrid)
					.Select(x => (x, x.CalulateXGrid(tGrid)))
					.Where(x => x.Item2 is not null)
					.OrderBy(x => x.Item2)
					.FirstOrDefault();

				obj.ReferenceLaneStart = afterLane as LaneStartBase;
			}

			var grids = fumen.GetAllDisplayableObjects()
				.SelectMany(x => new GridBase[] { (x as IHorizonPositionObject)?.XGrid, (x as ITimelineObject)?.TGrid })
				.OfType<GridBase>();
			foreach (var grid in grids)
			{
				RecalcGrid(grid);
			}

			return new(true) { SerializedFumen = fumen };
		}

		private static bool CheckFumenIsSerializable(OngekiFumen fumen, out string msg)
		{
			var checkRules = IoC.GetAll<IFumenCheckRule>().OfType<IOngekiFumenCheckRule>();
			if (checkRules.Any(x => x.CheckRule(fumen, null).Any(x => x.Severity == RuleSeverity.Error)))
			{
				msg = Resources.FumenContainUngenerateError;
				return false;
			}

			msg = string.Empty;
			return true;
		}

		private static void RecalcGrid(GridBase grid)
		{
			var fixedPointPart = grid.Unit - (int)grid.Unit;

			grid.Grid = (int)Math.Floor(grid.GridRadix * fixedPointPart + 0.5f) + grid.Grid;
			grid.Unit = (int)grid.Unit;

			grid.NormalizeSelf();
		}

		private static void PostProcessInterpolatedConnectableStart(ConnectableStartObject rawStart, List<ConnectableStartObject> genStarts)
		{

		}
	}
}
