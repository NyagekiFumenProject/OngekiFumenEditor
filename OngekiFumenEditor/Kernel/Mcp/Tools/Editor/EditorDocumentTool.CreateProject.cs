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
        [McpServerTool(Name = "editor.create_proj", Title = "Create Project", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Create a new editor project from an audio file, optionally seeded with an existing chart. This mirrors the new-project setup window without showing it: audio duration is measured from the file unless audioDurationMs is given, and baseBpm defaults to the chart's first BPM definition.")]
        public async Task<object> CreateProject(
            string audioPath,
            [Description("Optional existing chart to load into the new project.")] string fumenPath = default,
            [Description("Optional base BPM; defaults to the chart's first BPM definition when a chart is supplied.")] double? baseBpm = default,
            [Description("Optional audio duration in milliseconds; when omitted the audio file is measured.")] double? audioDurationMs = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.create_proj";
            McpOperationLogHelper.LogRequest(operationName, new { audioPath, fumenPath, baseBpm, audioDurationMs, requireConfirmation, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Create a project from audio '{audioPath}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
                return Failure(operationName, "AUDIO_NOT_FOUND", $"Audio file not found: {audioPath}");

            if (!string.IsNullOrWhiteSpace(fumenPath) && !File.Exists(fumenPath))
                return Failure(operationName, "FILE_NOT_FOUND", $"Fumen file not found: {fumenPath}");

            EditorProjectDataModel projModel;
            try
            {
                projModel = await RuntimeUiDispatcher.RunAsync(
                    async () => await BuildProjectModelAsync(audioPath, fumenPath, baseBpm, audioDurationMs),
                    cancellationToken);
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
