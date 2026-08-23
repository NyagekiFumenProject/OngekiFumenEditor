#nullable enable

using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Gekimini.Avalonia.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.DropActions;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

public partial class SvgPrefabOperationViewModel : ViewModelBase
{
    public SvgPrefabBase SvgPrefab { get; }

    public SvgPrefabOperationViewModel(SvgPrefabBase svgPrefab)
    {
        SvgPrefab = svgPrefab ?? throw new ArgumentNullException(nameof(svgPrefab));
    }

    [RelayCommand]
    private void GenerateLanes()
    {
        Log.LogInfo($"GenerateLanes triggered ({SvgPrefab.GetType().Name}).");
        var editor = IoC.Get<IFumenObjectPropertyBrowser>().Editor;
        if (editor is null)
        {
            _ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.MustMakeEditorActive);
            return;
        }

        if (!editor.IsDesignMode)
        {
            _ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.EditorMustBeDesignMode);
            return;
        }

        if (SvgPrefab.Picture is null)
        {
            _ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.SvgContentNotSupport);
            return;
        }

        if (SvgPrefab.ShowOriginColor)
        {
            _ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.UncheckShowOriginColor);
            return;
        }

        var generatedLanes = GenerateLaneObjects(editor).ToArray();
        if (generatedLanes.Length == 0)
        {
            _ = IoC.Get<IDialogManager>().ShowMessageDialog(Lang.SvgContentNotSupport);
            return;
        }

        editor.UndoRedoManager.ExecuteAction(LambdaUndoAction.Create(Lang.B.SvgGenerateLane.ToLocalizedString(), () =>
        {
            editor.EditorContext.Fumen.AddObjects(generatedLanes);
        }, () =>
        {
            editor.EditorContext.Fumen.RemoveObjects(generatedLanes);
        }));
    }

    internal IEnumerable<ConnectableStartObject> GenerateLaneObjects(FumenVisualEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var baseCanvasX = XGridCalculator.ConvertXGridToX(SvgPrefab.XGrid, editor);
        var baseCanvasY = TGridCalculator.ConvertTGridToY_DesignMode(SvgPrefab.TGrid, editor);

        foreach (var segment in SvgPrefab.GenerateLineSegments())
        {
            var laneColor = SvgPrefab.PickSimilarLaneColor(segment.Color);
            if (laneColor is null || segment.RelativePoints.Count < 2)
                continue;

            var laneType = laneColor.Value.LaneType;
            var startObject = CreateLaneStart(laneType);
            if (startObject is null)
                continue;

            (TGrid TGrid, XGrid XGrid)? TryConvertPoint(System.Numerics.Vector2 relativePoint)
            {
                var actualCanvasX = baseCanvasX + relativePoint.X;
                var actualCanvasY = baseCanvasY + relativePoint.Y;

                //Log.LogDebug($"{relativePoint}  ->  {new Vector2((float)actualCanvasX, (float)actualCanvasY)}");
                var tGrid = TGridCalculator.ConvertYToTGrid_DesignMode(actualCanvasY, editor);
                var xGrid = XGridCalculator.ConvertXToXGrid(actualCanvasX, editor);
                if (tGrid is null ||
                    !float.IsFinite(tGrid.Unit) ||
                    !float.IsFinite(xGrid.Unit))
                {
                    return null;
                }

                return (tGrid, xGrid);
            }

            var convertedPoints = segment.RelativePoints
                .Select(TryConvertPoint)
                .ToArray();
            if (convertedPoints.Any(x => x is null))
                continue;

            static void CommonBuildUp((TGrid TGrid, XGrid XGrid) point, ConnectableObjectBase obj)
            {
                obj.TGrid = point.TGrid;
                obj.XGrid = point.XGrid;
            }

            CommonBuildUp(convertedPoints[0]!.Value, startObject);
            foreach (var childPoint in convertedPoints.Skip(1).SkipLast(1))
            {
                var nextObject = CreateLaneNext(laneType);
                CommonBuildUp(childPoint!.Value, nextObject);
                startObject.AddChildObject(nextObject);
            }

            var endObject = CreateLaneNext(laneType);
            CommonBuildUp(convertedPoints[^1]!.Value, endObject);
            startObject.AddChildObject(endObject);

            var interpolated = startObject.InterpolateCurve(
                () => CreateLaneStart(laneType)!,
                () => CreateLaneNext(laneType),
                SvgPrefab.CurveInterpolaterFactory).ToArray();
            if (laneType == LaneType.Colorful)
            {
                //染色
                var colorId = ColorIdConst.SvgPrefabColors.FirstOrDefault(x => x.Color == laneColor.Value.Color);
                var brightness = (int)SvgPrefab.ColorfulLaneBrightness.CurrentValue;
                foreach (var colorfulLane in interpolated
                             .SelectMany(x => x.Children.Cast<ConnectableObjectBase>().Append(x))
                             .OfType<IColorfulLane>())
                {
                    colorfulLane.ColorId = colorId;
                    colorfulLane.Brightness = brightness;
                }
            }

            foreach (var lane in interpolated)
                yield return lane;
        }
    }

    private static LaneStartBase? CreateLaneStart(LaneType laneType) => laneType switch
    {
        LaneType.Left => new LaneLeftStart(),
        LaneType.Center => new LaneCenterStart(),
        LaneType.Right => new LaneRightStart(),
        LaneType.Colorful => new ColorfulLaneStart(),
        _ => null
    };

    private static ConnectableChildObjectBase CreateLaneNext(LaneType laneType) => laneType switch
    {
        LaneType.Left => new LaneLeftNext(),
        LaneType.Center => new LaneCenterNext(),
        LaneType.Right => new LaneRightNext(),
        LaneType.Colorful => new ColorfulLaneNext(),
        _ => throw new ArgumentOutOfRangeException(nameof(laneType), laneType, "Unsupported SVG lane type.")
    };
}
