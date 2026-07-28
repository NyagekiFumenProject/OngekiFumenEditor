using Gekimini.Avalonia.Framework.Commands;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll
{
	public abstract class InterpolateAllCommandHandlerBase<T> : CommandHandlerBase<T> where T : CommandDefinition
	{
		protected void Process(FumenVisualEditorViewModel editor, bool xGridLimit)
		{
			var fumen = editor.Fumen;

			var laneMap = new Dictionary<ConnectableStartObject, List<ConnectableStartObject>>();

			var curveFactory = xGridLimit ? XGridLimitedCurveInterpolaterFactory.Default : default;

			foreach ((var beforeLane, var genLanes) in Utils.Ogkr.InterpolateAll.Calculate(fumen, curveFactory))
				laneMap[beforeLane] = genLanes.ToList();

			var curveStarts = laneMap.Keys.ToList();

			var affactObjects = Utils.Ogkr.InterpolateAll.CalculateAffectedDockableObjects(fumen, curveStarts).ToArray();

			var redoAction = new System.Action(() => { });

			var undoAction = new System.Action(() => { });

			foreach (var item in laneMap)
			{
				var beforeLane = item.Key;
				var afterLanes = item.Value;

				redoAction += () =>
				{
					fumen.RemoveObject(beforeLane);
					fumen.AddObjects(afterLanes);
				};

				undoAction += () =>
				{
					fumen.AddObject(beforeLane);
					fumen.RemoveObjects(afterLanes);
				};
			}

			foreach (var obj in affactObjects)
			{
				var tGrid = obj.TGrid;
				var beforeXGrid = obj.XGrid;
				var beforeLane = obj.ReferenceLaneStart;

				(var afterLane, var afterXGrid) = laneMap[obj.ReferenceLaneStart]
					.Where(x => tGrid >= x.MinTGrid && tGrid <= x.MaxTGrid)
					.Select(x => (x, x.CalulateXGrid(tGrid)))
					.Where(x => x.Item2 is not null)
					.OrderBy(x => x.Item2)
					.FirstOrDefault();

				redoAction += () =>
				{
					obj.ReferenceLaneStart = afterLane as LaneStartBase;
					//obj.XGrid = afterXGrid;
				};

				undoAction += () =>
				{
					obj.ReferenceLaneStart = beforeLane;
					//obj.XGrid = beforeXGrid;
				};
			}

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.CommandInterpolateAll.ToLocalizedString(), redoAction, undoAction));
			Log.LogInfo(Lang.InterpolateComplete.Format(curveStarts.Count, laneMap.Values.Select(x => x.Count).Sum(), affactObjects.Count()));
		}
	}
}




