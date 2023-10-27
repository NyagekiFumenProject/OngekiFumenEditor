using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Utils;
using System;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.MiscMenu.Commands.CallFullGC
{
	[CommandHandler]
	public class CallFullGCCommandHandler : CommandHandlerBase<CallFullGCCommandDefinition>
	{
		public override Task Run(Command command)
		{
			var before = GC.GetTotalMemory(false);
			var beforePriv = GC.GetTotalAllocatedBytes(false);
			var info = GC.GetGCMemoryInfo(GCKind.Any);
			GC.Collect(0, GCCollectionMode.Forced);
			var after = GC.GetTotalMemory(true);
			var afterPriv = GC.GetTotalAllocatedBytes(false);
			Log.LogInfo($"GC called, {FileHelper.FormatFileSize(before)}({FileHelper.FormatFileSize(beforePriv)}) -> {FileHelper.FormatFileSize(after)}({FileHelper.FormatFileSize(afterPriv)})");
			return TaskUtility.Completed;
		}
	}
}