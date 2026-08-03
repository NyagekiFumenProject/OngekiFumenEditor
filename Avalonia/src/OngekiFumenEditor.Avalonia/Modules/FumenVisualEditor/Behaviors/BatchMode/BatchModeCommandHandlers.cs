using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Behaviors.BatchMode;

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

[RegisterSingleton<ICommandHandler>] public partial class BatchModeLaneLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneLeft>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeLaneCenterCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneCenter>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeLaneRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneRight>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeWallLeftCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputWallLeft>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeWallRightCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputWallRight>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeLaneColorfulCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneColorful>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeTapCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputTap>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeHoldCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputHold>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeFlickCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputFlick>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeBellCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputNormalBell>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeLaneBlockCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputLaneBlock>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeClipboardCommandHandler : BatchModeSubmodeCommandHandler<BatchModeInputClipboard>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeFilterLanesCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterLanes>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeFilterDockableObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterDockableObjects>;
[RegisterSingleton<ICommandHandler>] public partial class BatchModeFilterFloatingObjectsCommandHandler : BatchModeSubmodeCommandHandler<BatchModeFilterFloatingObjects>;


