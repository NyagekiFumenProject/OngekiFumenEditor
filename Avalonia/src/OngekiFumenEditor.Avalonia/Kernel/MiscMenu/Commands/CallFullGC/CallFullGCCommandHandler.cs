using Gekimini.Avalonia.Framework.Commands;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Kernel.MiscMenu.Commands.CallFullGC;

[RegisterSingleton<ICommandHandler>]
public class CallFullGCCommandHandler : CommandHandlerBase<CallFullGCCommandDefinition>
{
    public override Task Run(Command command)
    {
        var before = GC.GetTotalMemory(false);
        var beforePriv = GC.GetTotalAllocatedBytes(false);

        GC.Collect(0, GCCollectionMode.Forced);

        var after = GC.GetTotalMemory(true);
        var afterPriv = GC.GetTotalAllocatedBytes(false);
        Log.LogInfo(
            $"GC called, {FileHelper.FormatFileSize(before)}({FileHelper.FormatFileSize(beforePriv)}) -> {FileHelper.FormatFileSize(after)}({FileHelper.FormatFileSize(afterPriv)})");
        return Task.CompletedTask;
    }
}
