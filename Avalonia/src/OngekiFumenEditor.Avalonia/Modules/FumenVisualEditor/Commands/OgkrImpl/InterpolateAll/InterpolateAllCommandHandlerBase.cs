using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.DefaultImpl.Factory;
using OngekiFumenEditor.Avalonia.Kernel.CurveInterpolater.OgkrImpl.Factory;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.InterpolateAll;

public abstract class InterpolateAllCommandHandlerBase<T> : CommandHandlerBase<T>
    where T : CommandDefinition
{
    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly IDialogManager dialogManager;
    private readonly bool xGridLimit;

    protected InterpolateAllCommandHandlerBase(
        IEditorDocumentManager editorDocumentManager,
        IDialogManager dialogManager,
        bool xGridLimit)
    {
        this.editorDocumentManager = editorDocumentManager;
        this.dialogManager = dialogManager;
        this.xGridLimit = xGridLimit;
    }

    public override Task Update(Command command)
    {
        command.Enabled = editorDocumentManager.CurrentActivatedEditor?.Fumen is not null;
        return Task.CompletedTask;
    }

    public override async Task Run(Command command)
    {
        if (editorDocumentManager.CurrentActivatedEditor is not { Fumen: not null } editor)
            return;

        if (!await dialogManager.ShowComfirmDialog(Lang.ComfirmInterpolateMessage, Lang.Warning))
            return;

        editor.LockAllUserInteraction();
        try
        {
            Process(editor, xGridLimit);
        }
        finally
        {
            editor.UnlockAllUserInteraction();
        }
    }

    protected virtual bool Process(FumenVisualEditorViewModel editor, bool xGridLimit)
    {
        var fumen = editor.Fumen;
        if (fumen is null)
            return false;

        var laneMap = new Dictionary<ConnectableStartObject, List<ConnectableStartObject>>();
        var curveFactory = ResolveCurveInterpolaterFactory(xGridLimit);

        foreach ((var beforeLane, var genLanes) in Utils.Ogkr.InterpolateAll.Calculate(fumen, curveFactory))
            laneMap[beforeLane] = genLanes.ToList();

        if (laneMap.Count == 0)
            return false;

        var curveStarts = laneMap.Keys.ToList();
        var laneMapByRecordId = laneMap.ToDictionary(x => x.Key.RecordId, x => x.Value);
        var affectedObjects = new List<(ILaneDockable Object, LaneStartBase BeforeLane, LaneStartBase AfterLane)>();

        foreach (var obj in Utils.Ogkr.InterpolateAll.CalculateAffectedDockableObjects(fumen, curveStarts))
        {
            if (obj.ReferenceLaneStart is not { } beforeLane ||
                !laneMapByRecordId.TryGetValue(beforeLane.RecordId, out var generatedLanes))
                continue;

            var afterLane = generatedLanes
                .Where(x => obj.TGrid >= x.MinTGrid && obj.TGrid <= x.MaxTGrid)
                .Select(x => (Lane: x, XGrid: x.CalulateXGrid(obj.TGrid)))
                .Where(x => x.XGrid is not null)
                .OrderBy(x => x.XGrid)
                .Select(x => x.Lane)
                .OfType<LaneStartBase>()
                .FirstOrDefault();

            if (afterLane is not null)
                affectedObjects.Add((obj, beforeLane, afterLane));
        }

        var redoAction = new Action(() =>
        {
            foreach (var (beforeLane, afterLanes) in laneMap)
            {
                fumen.RemoveObject(beforeLane);
                fumen.AddObjects(afterLanes);
            }

            foreach (var affectedObject in affectedObjects)
                affectedObject.Object.ReferenceLaneStart = affectedObject.AfterLane;
        });

        var undoAction = new Action(() =>
        {
            foreach (var (beforeLane, afterLanes) in laneMap)
            {
                fumen.AddObject(beforeLane);
                fumen.RemoveObjects(afterLanes);
            }

            foreach (var affectedObject in affectedObjects)
                affectedObject.Object.ReferenceLaneStart = affectedObject.BeforeLane;
        });

        editor.UndoRedoManager.ExecuteAction(
            LambdaUndoAction.Create(Lang.B.CommandInterpolateAll.ToLocalizedString(), redoAction, undoAction));
        Log.LogInfo(Lang.InterpolateComplete.Format(
            curveStarts.Count,
            laneMap.Values.Select(x => x.Count).Sum(),
            affectedObjects.Count));
        return true;
    }

    internal static ICurveInterpolaterFactory ResolveCurveInterpolaterFactory(bool xGridLimit) =>
        xGridLimit ? XGridLimitedCurveInterpolaterFactory.Default : DefaultCurveInterpolaterFactory.Default;
}
