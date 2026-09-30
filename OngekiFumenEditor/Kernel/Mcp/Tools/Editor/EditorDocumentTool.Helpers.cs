using Caliburn.Micro;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
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
        private static async Task<EditorProjectDataModel> BuildFumenModelAsync(string fumenPath, string audioPath, double? baseBpm)
        {
            var proj = new EditorProjectDataModel
            {
                FumenFilePath = fumenPath,
                AudioFilePath = audioPath,
            };

            using (var fs = File.OpenRead(fumenPath))
            {
                var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(fumenPath).DeserializeAsync(fs);
                proj.Fumen = fumen;
                proj.BaseBPM = baseBpm ?? fumen.MetaInfo.BpmDefinition.First;
            }

            using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(audioPath);
            proj.AudioDuration = audio.Duration;
            return proj;
        }

        private static async Task<EditorProjectDataModel> BuildProjectModelAsync(string audioPath, string fumenPath, double? baseBpm, double? audioDurationMs)
        {
            var proj = new EditorProjectDataModel
            {
                AudioFilePath = audioPath,
            };

            if (audioDurationMs is { } durationMs)
            {
                proj.AudioDuration = TimeSpan.FromMilliseconds(durationMs);
            }
            else
            {
                using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(audioPath);
                proj.AudioDuration = audio.Duration;
            }

            if (string.IsNullOrWhiteSpace(fumenPath))
            {
                if (baseBpm is { } bpm)
                    proj.BaseBPM = bpm;

                return proj;
            }

            using (var fs = File.OpenRead(fumenPath))
            {
                var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(fumenPath).DeserializeAsync(fs);
                proj.Fumen = fumen;
                proj.FumenFilePath = fumenPath;
                proj.BaseBPM = baseBpm ?? fumen.MetaInfo.BpmDefinition.First;
            }

            return proj;
        }

        private async Task<object> TryAuthorizeAsync(string operationName, string requestedBy, string clientId, string preview, bool allowInteractivePrompt, CancellationToken cancellationToken)
        {
            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync(operationName, requestedBy, clientId, preview, allowInteractivePrompt, cancellationToken);
            if (authorizationResult.IsAuthorized)
                return null;

            var deniedResult = new
            {
                success = false,
                errorCode = authorizationResult.ErrorCode,
                errorMessage = authorizationResult.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, deniedResult);
            return deniedResult;
        }

        private object ReportOpen(string operationName, bool opened, string fumenPath, string audioPath, string projectPath)
        {
            var editor = editorDocumentManager.CurrentActivatedEditor;

            // open_proj only knows the project file; the chart and audio it points at are
            // discovered while the editor loads. Fall back to what the editor has resolved so
            // the response is useful whenever it is already available.
            fumenPath = string.IsNullOrWhiteSpace(fumenPath) ? editor?.EditorProjectData?.FumenFilePath : fumenPath;
            audioPath = string.IsNullOrWhiteSpace(audioPath) ? editor?.EditorProjectData?.AudioFilePath : audioPath;

            var response = new
            {
                success = opened,
                opened,
                fumenPath = string.IsNullOrWhiteSpace(fumenPath) ? default : fumenPath,
                audioPath = string.IsNullOrWhiteSpace(audioPath) ? default : audioPath,
                projectPath = string.IsNullOrWhiteSpace(projectPath) ? default : projectPath,
                editorId = RuntimeAutomationEditorId.Generate(editor),
                displayName = editor?.DisplayName,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        private bool TryResolveEditor(string editorId, string expectedEditorId, out FumenVisualEditorViewModel editor, out string resolvedEditorId, out object errorResult)
        {
            editor = string.IsNullOrWhiteSpace(editorId)
                ? editorDocumentManager.CurrentActivatedEditor
                : (editorDocumentManager.TryGetEditorById(editorId, out var found) ? found : null);
            resolvedEditorId = RuntimeAutomationEditorId.Generate(editor);

            if (editor is null)
            {
                errorResult = new
                {
                    success = false,
                    errorCode = string.IsNullOrWhiteSpace(editorId) ? "NO_ACTIVE_EDITOR" : "EDITOR_NOT_FOUND",
                    errorMessage = string.IsNullOrWhiteSpace(editorId) ? "No active editor is available." : $"Editor '{editorId}' was not found.",
                };
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedEditorId) && !string.Equals(expectedEditorId, resolvedEditorId, StringComparison.Ordinal))
            {
                errorResult = new
                {
                    success = false,
                    errorCode = "EDITOR_CHANGED",
                    errorMessage = $"Expected editor '{expectedEditorId}' but resolved '{resolvedEditorId}'.",
                };
                return false;
            }

            errorResult = null;
            return true;
        }

        private static object Failure(string operationName, string errorCode, string errorMessage)
        {
            var result = new { success = false, errorCode, errorMessage };
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
