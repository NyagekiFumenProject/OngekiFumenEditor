using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.FastPickLane;

public abstract class FastPickLaneCommandHandler<T, DEF> : CommandHandlerBase<DEF>
    where DEF : FastPickLaneCommandDefinition<T> where T : LaneStartBase
{
    private IEditorDocumentManager EditorDocumentManager => OngekiFumenEditor.Avalonia.IoC.Get<IEditorDocumentManager>();

    public override Task Update(Command command)
    {
        command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
        return Task.CompletedTask;
    }

    public override Task Run(Command command)
    {
        if (EditorDocumentManager.CurrentActivatedEditor is not FumenVisualEditorViewModel editor)
            return Task.CompletedTask;
        if (!editor.IsDesignMode)
        {
            editor.ToastNotify(Lang.EditorMustBeDesignMode);
            return Task.CompletedTask;
        }

        var filterTGrid = TGridCalculator.ConvertYToTGrid_DesignMode(editor.RectInDesignMode.MaxY, editor);
        var selectLane = editor.EditorContext.Fumen.Lanes.OfType<T>().Where(x => x.MaxTGrid <= filterTGrid).OrderBy(x => x.MaxTGrid).LastOrDefault();

        var obj = selectLane?.Children.LastOrDefault() as ConnectableObjectBase;
        obj = obj ?? selectLane;

        if (obj is not null)
            editor.NotifyObjectClicked(obj);

        return Task.CompletedTask;
    }
}

[RegisterSingleton<ICommandHandler>]
public partial class FastPickWallLeftLaneCommandHandler : FastPickLaneCommandHandler<WallLeftStart, FastPickWallLeftLaneCommandDefinition>
{ }

[RegisterSingleton<ICommandHandler>]
public partial class FastPickLeftLaneCommandHandler : FastPickLaneCommandHandler<LaneLeftStart, FastPickLeftLaneCommandDefinition>
{ }

[RegisterSingleton<ICommandHandler>]
public partial class FastPickCenterLaneCommandHandler : FastPickLaneCommandHandler<LaneCenterStart, FastPickCenterLaneCommandDefinition>
{ }

[RegisterSingleton<ICommandHandler>]
public partial class FastPickRightLaneCommandHandler : FastPickLaneCommandHandler<LaneRightStart, FastPickRightLaneCommandDefinition>
{ }

[RegisterSingleton<ICommandHandler>]
public partial class FastPickWallRightLaneCommandHandler : FastPickLaneCommandHandler<WallRightStart, FastPickWallRightLaneCommandDefinition>
{ }
