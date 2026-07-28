using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.EditorSvgObjectControlProvider.ViewModels.ObjectProperty.Operation
{
	public class SvgPrefabOperationViewModel : ObservableObject
	{
		public SvgPrefabBase SvgPrefab { get; }

		public SvgPrefabOperationViewModel(SvgPrefabBase svgPrefab)
		{
			SvgPrefab = svgPrefab;
		}

		public void OnGenerateLaneToEditor()
		{
			if (IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
			{
				MessageBox.Show(Lang.MustMakeEditorActive);
				return;
			}

			if (!editor.IsDesignMode)
			{
				MessageBox.Show(Lang.EditorMustBeDesignMode);
				return;
			}

			if (SvgPrefab.ProcessingDrawingGroup is not DrawingGroup drawingGroup)
			{
				MessageBox.Show(Lang.SvgContentNotSupport);
				return;
			}

			if (SvgPrefab.ShowOriginColor)
			{
				MessageBox.Show(Lang.UncheckShowOriginColor);
				return;
			}

			var baseCanvasX = XGridCalculator.ConvertXGridToX(SvgPrefab.XGrid, editor);
			var baseCanvasY = TGridCalculator.ConvertTGridToY_DesignMode(SvgPrefab.TGrid, editor);

			var segments = SvgPrefab.GenerateLineSegments();

			var genStarts = new List<ConnectableStartObject>();

			foreach (var seg in segments)
			{
				var laneColor = SvgPrefab.PickSimilarLaneColor(seg.Color);
				var points = seg.RelativePoints;

				LaneStartBase targetObject = laneColor?.LaneType switch
				{
					LaneType.Left => new LaneLeftStart(),
					LaneType.Center => new LaneCenterStart(),
					LaneType.Right => new LaneRightStart(),
					LaneType.Colorful => new ColorfulLaneStart(),
					_ => null
				};

				if (targetObject is null)
					continue;

				void CommomBuildUp(Vector2 relativePoint, ConnectableObjectBase obj)
				{
					var actualCanvasX = baseCanvasX + relativePoint.X;
					var actualCanvasY = baseCanvasY + relativePoint.Y;

					//Log.LogDebug($"{relativePoint}  ->  {new Vector2((float)actualCanvasX, (float)actualCanvasY)}");
					var tGrid = TGridCalculator.ConvertYToTGrid_DesignMode(actualCanvasY, editor);
					var xGrid = XGridCalculator.ConvertXToXGrid(actualCanvasX, editor);

					obj.XGrid = xGrid;
					obj.TGrid = tGrid;
				}

				var firstP = points[0];
				var startObj = LambdaActivator.CreateInstance(targetObject.GetType()) as ConnectableStartObject;
				CommomBuildUp(firstP, startObj);

				foreach (var childP in points.Skip(1).SkipLast(1))
				{
					var nextObj = targetObject.CreateChildObject();
					CommomBuildUp(childP, nextObj);
					startObj.AddChildObject(nextObj);
				}

				var lastP = points.LastOrDefault();
				var endObj = targetObject.CreateChildObject();
				CommomBuildUp(lastP, endObj);
				startObj.AddChildObject(endObj);

				var r = startObj.InterpolateCurve().ToArray();

				var subGenStarts = startObj.InterpolateCurve(SvgPrefab.CurveInterpolaterFactory).ToArray();
				if (targetObject is IColorfulLane lane)
				{
					//染色
					var colorId = ColorIdConst.AllColors.FirstOrDefault(x => x.Color == laneColor?.Color);
					var brightness = (int)SvgPrefab.ColorfulLaneBrightness.CurrentValue;
					subGenStarts
						.SelectMany(x => x.Children.AsEnumerable<ConnectableObjectBase>().Append(x))
						.OfType<IColorfulLane>()
						.ForEach(x =>
						{
							x.ColorId = colorId;
							x.Brightness = brightness;
						});
				}

				genStarts.AddRange(subGenStarts);
			}

			editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.SvgGenerateLane.ToLocalizedString(), () =>
			{
				editor.Fumen.AddObjects(genStarts);
			}, () =>
			{
				editor.Fumen.RemoveObjects(genStarts);
			}));
		}
	}
}



