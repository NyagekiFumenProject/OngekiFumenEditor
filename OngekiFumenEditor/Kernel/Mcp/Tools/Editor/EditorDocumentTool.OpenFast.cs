using Caliburn.Micro;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorDocumentTool
    {
        [McpServerTool(Name = "editor.open_fast", Title = "Fast Open Fumen", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Fast-open a chart (.ogkr/.nyageki). Without audioPath the audio next to the chart is resolved automatically (exactly like the FastOpen menu command, which may prompt for an audio file when nothing suitable is found); with audioPath the chart and that audio file are opened together.")]
        public async Task<object> OpenFast(string fumenPath, string audioPath = default, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.open_fast";
            McpOperationLogHelper.LogRequest(operationName, new { fumenPath, audioPath, requireConfirmation, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Fast-open chart '{fumenPath}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (string.IsNullOrWhiteSpace(fumenPath) || !File.Exists(fumenPath))
                return Failure(operationName, "FILE_NOT_FOUND", $"Fumen file not found: {fumenPath}");

            if (!FastOpenExtensions.Contains(Path.GetExtension(fumenPath), StringComparer.OrdinalIgnoreCase))
                return Failure(operationName, "UNSUPPORTED_FILE_TYPE", $"editor.open_fast expects one of {string.Join(", ", FastOpenExtensions)}; use editor.open_proj for {FumenVisualEditorProvider.FILE_EXTENSION_NAME}.");

            // 不给音频 → 走真正的 FastOpen：自动按谱面路径找音频，找不到时（与原菜单命令一致）会弹一次音频选择框。
            if (string.IsNullOrWhiteSpace(audioPath))
            {
                bool fastOpened;
                try
                {
                    fastOpened = await RuntimeUiDispatcher.RunAsync(async () => await DocumentOpenHelper.TryOpenOgkrFileAsDocument(fumenPath), cancellationToken);
                }
                catch (Exception ex)
                {
                    return Failure(operationName, "OPEN_FAILED", ex.Message);
                }

                return ReportOpen(operationName, fastOpened, fumenPath, default, default);
            }

            if (!File.Exists(audioPath))
                return Failure(operationName, "AUDIO_NOT_FOUND", $"Audio file not found: {audioPath}");

            EditorProjectDataModel projModel;
            try
            {
                projModel = await RuntimeUiDispatcher.RunAsync(async () => await BuildFumenModelAsync(fumenPath, audioPath, default), cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "LOAD_FAILED", ex.Message);
            }

            bool opened;
            try
            {
                opened = await RuntimeUiDispatcher.RunAsync(async () => await DocumentOpenHelper.TryOpenProject(projModel), cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "OPEN_FAILED", ex.Message);
            }

            return ReportOpen(operationName, opened, fumenPath, audioPath, default);
        }
    }
}
