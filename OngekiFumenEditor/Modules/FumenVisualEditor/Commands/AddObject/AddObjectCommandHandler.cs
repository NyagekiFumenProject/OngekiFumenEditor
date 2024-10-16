using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.AddObject;

[CommandHandler]
public class AddObjectCommandHandler<D> : CommandHandlerBase<D> where D : AddObjectCommandDefinition
{
    public override void Update(Command command)
    {
        base.Update(command);
        command.Enabled = true;
    }
    
    public override Task Run(Command command)
    {
        var cmd = (AddObjectCommandDefinition)command.CommandDefinition;
        var ongekiObj = cmd.CreateOngekiObject();

        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        editor!.MoveObjectTo(ongekiObj, editor.CurrentCursorPosition!.Value);
        editor.Fumen.AddObject(ongekiObj);
        editor.InteractiveManager.GetInteractive(ongekiObj).OnMoveCanvas(ongekiObj, editor.CurrentCursorPosition.Value, editor);
        editor.NotifyObjectClicked(ongekiObj);

        return Task.CompletedTask;
    }
}

[CommandHandler]
public class AddObjectLaneLeftCommandHandler : AddObjectCommandHandler<AddObjectLaneLeftCommandDefinition>
{ }

[CommandHandler]
public class AddObjectLaneCenterCommandHandler : AddObjectCommandHandler<AddObjectLaneCenterCommandDefinition>
{ }
[CommandHandler]
public class AddObjectLaneRightCommandHandler : AddObjectCommandHandler<AddObjectLaneRightCommandDefinition>
{ }
[CommandHandler]
public class AddObjectWallLeftCommandHandler : AddObjectCommandHandler<AddObjectWallLeftCommandDefinition>
{ }
[CommandHandler]
public class AddObjectWallRightCommandHandler : AddObjectCommandHandler<AddObjectWallRightCommandDefinition>
{ }
