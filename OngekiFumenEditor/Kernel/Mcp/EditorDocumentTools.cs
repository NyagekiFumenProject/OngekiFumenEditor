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

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(EditorDocumentTools))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorDocumentTools
    {
        private static readonly string[] FastOpenExtensions = { ".ogkr", ".nyageki" };

        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;

        [ImportingConstructor]
        public EditorDocumentTools(IEditorDocumentManager editorDocumentManager, IMcpToolAuthorizationService mcpToolAuthorizationService)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
        }

        [McpServerTool(Name = "editor.open_proj", Title = "Open Project", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Open a .nyagekiProj project file. The editor shows its loading dialog while the project loads; the tool returns once the new editor reports Ready.")]
        public async Task<object> OpenProject(string projectPath, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.open_proj";
            McpOperationLogHelper.LogRequest(operationName, new { projectPath, requireConfirmation, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Open project '{projectPath}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
                return Failure(operationName, "FILE_NOT_FOUND", $"Project file not found: {projectPath}");

            if (!string.Equals(Path.GetExtension(projectPath), FumenVisualEditorProvider.FILE_EXTENSION_NAME, StringComparison.OrdinalIgnoreCase))
                return Failure(operationName, "UNSUPPORTED_FILE_TYPE", $"editor.open_proj expects a {FumenVisualEditorProvider.FILE_EXTENSION_NAME} file; use editor.open_fast for .ogkr/.nyageki charts.");

            bool opened;
            try
            {
                opened = await RuntimeUiDispatcher.RunAsync(async () => await DocumentOpenHelper.TryOpenAsDocument(projectPath), cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "OPEN_FAILED", ex.Message);
            }

            return ReportOpen(operationName, opened, projectPath, default, default);
        }

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

        private static object Failure(string operationName, string errorCode, string errorMessage)
        {
            var result = new { success = false, errorCode, errorMessage };
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
