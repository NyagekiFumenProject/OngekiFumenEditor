using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Input;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Kernel.KeyBinding;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Utils;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Behaviors.BatchMode;

public abstract class BatchModeSubmodeCommandHandler<TCommandDefinition> : CommandHandlerBase<TCommandDefinition>
    where TCommandDefinition : BatchModeSubmode
{
    private IEditorDocumentManager Editor;
    private BatchModeSubmode Submode;

    public BatchModeSubmodeCommandHandler()
    {
        Editor = IoC.Get<IEditorDocumentManager>();
        Submode = BatchModeBehavior.Submodes.OfType<TCommandDefinition>().Single();
    }

    public override void Update(Command command)
    {
        base.Update(command);

        if (Editor.CurrentActivatedEditor is not null && Editor.CurrentActivatedEditor.IsBatchMode) {
            command.Enabled = true;
            command.Checked = Editor.CurrentActivatedEditor.BatchModeBehavior.CurrentSubmode == Submode;
        }
        else {
            command.Enabled = false;
            command.Checked = false;
        }
    }

    public override Task Run(Command command)
    {
        Editor.CurrentActivatedEditor.BatchModeBehavior.CurrentSubmode = Submode;
        return Task.CompletedTask;
    }
}

[CommandHandler] public class BatchModeLaneLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneLeft>;
[CommandHandler] public class BatchModeLaneCenterCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneCenter>;
[CommandHandler] public class BatchModeLaneRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneRight>;
[CommandHandler] public class BatchModeWallLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputWallLeft>;
[CommandHandler] public class BatchModeWallRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputWallRight>;
[CommandHandler] public class BatchModeLaneColorfulCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneColorful>;
[CommandHandler] public class BatchModeTapCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputTap>;
[CommandHandler] public class BatchModeHoldCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputHold>;
[CommandHandler] public class BatchModeFlickCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputFlick>;
[CommandHandler] public class BatchModeBellCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputNormalBell>;
[CommandHandler] public class BatchModeLaneBlockCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneBlock>;
[CommandHandler] public class BatchModeClipboardCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputClipboard>;
[CommandHandler] public class BatchModeFilterLanesCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterLanes>;
[CommandHandler] public class BatchModeFilterDockableObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterDockableObjects>;
[CommandHandler] public class BatchModeFilterFloatingObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterFloatingObjects>;