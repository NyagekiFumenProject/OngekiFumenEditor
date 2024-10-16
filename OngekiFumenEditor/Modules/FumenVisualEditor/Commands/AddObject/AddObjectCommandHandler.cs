using System;
using System.Threading.Tasks;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Modules.UndoRedo;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Views;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.AddObject;

[CommandHandler]
public class AddObjectCommandHandler<D> : CommandHandlerBase<D> where D : AddObjectCommandDefinition
{
    private OngekiMovableObjectBase OngekiObject = null;
    
    public override void Update(Command command)
    {
        base.Update(command);
        command.Enabled = true;
    }
    
    public override Task Run(Command command)
    {
        var cmd = (AddObjectCommandDefinition)command.CommandDefinition;
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;

        editor?.UndoRedoManager.ExecuteAction(new LambdaUndoAction(command.CommandDefinition.Name, Redo, Undo));
        
        return Task.CompletedTask;

        void Redo()
        {
            OngekiObject = cmd.CreateOngekiObject();
            editor!.MoveObjectTo(OngekiObject, editor.CurrentCursorPosition!.Value);
            editor.Fumen.AddObject(OngekiObject);
            editor.InteractiveManager.GetInteractive(OngekiObject).OnMoveCanvas(OngekiObject, editor.CurrentCursorPosition.Value, editor);
            if (cmd.AddToSelection)
                editor.NotifyObjectClicked(OngekiObject);
        }

        void Undo()
        {
            editor!.RemoveObject(OngekiObject);
            OngekiObject = null;
        }
    }
}

[CommandHandler] public class AddObjectLaneLeftCommandHandler : AddObjectCommandHandler<AddLaneLeftCommandDefinition> { }
[CommandHandler] public class AddObjectLaneCenterCommandHandler : AddObjectCommandHandler<AddLaneCenterCommandDefinition> { }
[CommandHandler] public class AddObjectLaneRightCommandHandler : AddObjectCommandHandler<AddLaneRightCommandDefinition> { }
[CommandHandler] public class AddObjectWallLeftCommandHandler : AddObjectCommandHandler<AddWallLeftCommandDefinition> { }
[CommandHandler] public class AddObjectWallRightCommandHandler : AddObjectCommandHandler<AddWallRightCommandDefinition> { }
[CommandHandler] public class AddObjectLaneColorfulCommandHandler : AddObjectCommandHandler<AddLaneColorfulCommandDefinition> { }
[CommandHandler] public class AddObjectTapCommandHandler : AddObjectCommandHandler<AddTapCommandDefinition> { }
[CommandHandler] public class AddObjectHoldCommandHandler : AddObjectCommandHandler<AddHoldCommandDefinition> { }
[CommandHandler] public class AddObjectFlickCommandHandler : AddObjectCommandHandler<AddFlickCommandDefinition> { }
[CommandHandler] public class AddObjectFlickReversedCommandHandler : AddObjectCommandHandler<AddFlickReversedCommandDefinition> { }
