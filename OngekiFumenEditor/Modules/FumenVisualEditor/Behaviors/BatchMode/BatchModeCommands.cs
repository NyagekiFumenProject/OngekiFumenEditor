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

public abstract class BatchModeSubmodeCommandDefinition<T> : CommandDefinition
    where T : BatchModeSubmode
{
    private BatchModeSubmode Submode;

    public BatchModeSubmodeCommandDefinition()
    {
        Submode = BatchModeBehavior.Submodes.Keys.OfType<T>().Single();
        Name = $"BatchSubmode.{Submode.ResourceKey}";
        Text = Submode.DisplayName;
    }

    public override string Name { get; }
    public override string Text { get; }

    public override string ToolTip =>
        (typeof(T).IsSubclassOf(typeof(BatchModeFilterSubmode)) ? "Filter " : string.Empty) +
        $"{Submode.DisplayName} ({KeyBindingDefinition.FormatToExpression(BatchModeBehavior.Submodes[Submode])})";

    public override Uri IconSource =>
        new Uri($"pack://application:,,,/OngekiFumenEditor;component/Resources/Icons/Batch/{Submode.ResourceKey}.png");
}

public abstract class BatchModeSubmodeCommandHandler<TCommandDefinition, TBatchSubmode> : CommandHandlerBase<TCommandDefinition>
    where TCommandDefinition : BatchModeSubmodeCommandDefinition<TBatchSubmode>
    where TBatchSubmode : BatchModeSubmode
{
    private IEditorDocumentManager Editor;
    private BatchModeSubmode Submode;

    public BatchModeSubmodeCommandHandler()
    {
        Editor = IoC.Get<IEditorDocumentManager>();
        Submode = BatchModeBehavior.Submodes.Keys.OfType<TBatchSubmode>().Single();
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

[CommandDefinition] public class BatchModeLaneLeftCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputLaneLeft>;
[CommandHandler] public class BatchModeLaneLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeLaneLeftCommandDefinition, BatchModeInputLaneLeft>;

[CommandDefinition] public class BatchModeLaneCenterCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputLaneCenter>;
[CommandHandler] public class BatchModeLaneCenterCommandHandler : BatchModeSubmodeCommandHandler<BatchModeLaneCenterCommandDefinition, BatchModeInputLaneCenter>;

[CommandDefinition] public class BatchModeLaneRightCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputLaneRight>;
[CommandHandler] public class BatchModeLaneRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeLaneRightCommandDefinition, BatchModeInputLaneRight>;

[CommandDefinition] public class BatchModeWallLeftCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputWallLeft>;
[CommandHandler] public class BatchModeWallLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeWallLeftCommandDefinition, BatchModeInputWallLeft>;

[CommandDefinition] public class BatchModeWallRightCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputWallRight>;
[CommandHandler] public class BatchModeWallRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeWallRightCommandDefinition, BatchModeInputWallRight>;

[CommandDefinition] public class BatchModeLaneColorfulCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputLaneColorful>;
[CommandHandler] public class BatchModeLaneColorfulCommandHandler : BatchModeSubmodeCommandHandler<BatchModeLaneColorfulCommandDefinition, BatchModeInputLaneColorful>;

[CommandDefinition] public class BatchModeTapCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputTap>;
[CommandHandler] public class BatchModeTapCommandHandler : BatchModeSubmodeCommandHandler<BatchModeTapCommandDefinition, BatchModeInputTap>;

[CommandDefinition] public class BatchModeHoldCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputHold>;
[CommandHandler] public class BatchModeHoldCommandHandler : BatchModeSubmodeCommandHandler<BatchModeHoldCommandDefinition, BatchModeInputHold>;

[CommandDefinition] public class BatchModeFlickCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputFlick>;
[CommandHandler] public class BatchModeFlickCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFlickCommandDefinition, BatchModeInputFlick>;

[CommandDefinition] public class BatchModeBellCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputNormalBell>;
[CommandHandler] public class BatchModeBellCommandHandler : BatchModeSubmodeCommandHandler<BatchModeBellCommandDefinition, BatchModeInputNormalBell>;

[CommandDefinition] public class BatchModeLaneBlockCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputLaneBlock>;
[CommandHandler] public class BatchModeLaneBlockCommandHandler : BatchModeSubmodeCommandHandler<BatchModeLaneBlockCommandDefinition, BatchModeInputLaneBlock>;

[CommandDefinition] public class BatchModeClipboardCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeInputClipboard>;
[CommandHandler] public class BatchModeClipboardCommandHandler : BatchModeSubmodeCommandHandler<BatchModeClipboardCommandDefinition, BatchModeInputClipboard>;

[CommandDefinition] public class BatchModeFilterLanesCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeFilterLanes>;
[CommandHandler] public class BatchModeFilterLanesCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterLanesCommandDefinition, BatchModeFilterLanes>;
[CommandDefinition] public class BatchModeFilterDockableObjectsCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeFilterDockableObjects>;
[CommandHandler] public class BatchModeFilterDockableObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterDockableObjectsCommandDefinition, BatchModeFilterDockableObjects>;

[CommandDefinition] public class BatchModeFilterFloatingObjectsCommandDefinition : BatchModeSubmodeCommandDefinition<BatchModeFilterFloatingObjects>;
[CommandHandler] public class BatchModeFilterFloatingObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterFloatingObjectsCommandDefinition, BatchModeFilterFloatingObjects>;