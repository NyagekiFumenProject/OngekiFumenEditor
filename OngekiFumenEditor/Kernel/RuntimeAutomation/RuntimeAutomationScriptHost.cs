using Microsoft.CodeAnalysis;
using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Scripts;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IRuntimeAutomationScriptHost))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class RuntimeAutomationScriptHost : IRuntimeAutomationScriptHost
    {
        private readonly IRuntimeEditorContextProvider editorContextProvider;
        private readonly IScriptSecurityPolicy scriptSecurityPolicy;
        private readonly IEditorScriptExecutor editorScriptExecutor;
        private readonly IEditorDocumentManager editorDocumentManager;
        private ScriptRunResult lastResult;

        private sealed class InternalBuildResult
        {
            public BuildResult RawBuildResult { get; set; }
            public ScriptBuildResult PublicBuildResult { get; set; }
        }

        [ImportingConstructor]
        public RuntimeAutomationScriptHost(
            IRuntimeEditorContextProvider editorContextProvider,
            IScriptSecurityPolicy scriptSecurityPolicy,
            IEditorScriptExecutor editorScriptExecutor,
            IEditorDocumentManager editorDocumentManager)
        {
            this.editorContextProvider = editorContextProvider;
            this.scriptSecurityPolicy = scriptSecurityPolicy;
            this.editorScriptExecutor = editorScriptExecutor;
            this.editorDocumentManager = editorDocumentManager;
        }

        public ScriptRunResult GetLastResult()
        {
            return lastResult;
        }

        public async Task<ScriptBuildResult> BuildAsync(ScriptBuildRequest request, CancellationToken cancellationToken = default)
        {
            var result = await BuildInternalAsync(request, cancellationToken);
            return result.PublicBuildResult;
        }

        public async Task<ScriptRunResult> RunOnCurrentEditorAsync(ScriptRunRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var editor = editorDocumentManager.CurrentActivatedEditor;
            if (editor is null)
                return CacheResult(CreateRunFailure("NO_ACTIVE_EDITOR", "No active editor is available."));

            var editorId = editorContextProvider.GetCurrentEditor()?.EditorId ?? RuntimeAutomationEditorId.Generate(editor);
            if (!string.IsNullOrWhiteSpace(request?.ExpectedEditorId) && !string.Equals(request.ExpectedEditorId, editorId, StringComparison.Ordinal))
                return CacheResult(CreateRunFailure("EDITOR_CHANGED", $"The active editor changed. Expected '{request.ExpectedEditorId}', actual '{editorId}'.", editorId, GetTransactionName(request)));

            return await RunOnEditorCoreAsync(editor, editorId, request, true, cancellationToken);
        }

        public async Task<ScriptRunResult> RunOnEditorAsync(string editorId, ScriptRunRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(editorId))
                return CacheResult(CreateRunFailure("EDITOR_NOT_FOUND", "Editor id is required."));

            if (!string.IsNullOrWhiteSpace(request?.ExpectedEditorId) && !string.Equals(request.ExpectedEditorId, editorId, StringComparison.Ordinal))
                return CacheResult(CreateRunFailure("EDITOR_CHANGED", $"The requested editor id '{editorId}' does not match expected '{request.ExpectedEditorId}'.", editorId, GetTransactionName(request)));

            var editor = editorDocumentManager.GetCurrentEditors().FirstOrDefault(x => RuntimeAutomationEditorId.Generate(x) == editorId);
            if (editor is null)
                return CacheResult(CreateRunFailure("EDITOR_NOT_FOUND", $"Editor '{editorId}' was not found.", editorId, GetTransactionName(request)));

            return await RunOnEditorCoreAsync(editor, editorId, request, false, cancellationToken);
        }

        private async Task<ScriptRunResult> RunOnEditorCoreAsync(FumenVisualEditorViewModel editor, string editorId, ScriptRunRequest request, bool requireActiveEditor, CancellationToken cancellationToken)
        {
            request ??= new ScriptRunRequest();

            if (request.RequireConfirmation && !await ConfirmExecutionAsync(editor, request, cancellationToken))
                return CacheResult(CreateRunFailure("USER_CONFIRMATION_REQUIRED", "Script execution was cancelled by user confirmation.", editorId, GetTransactionName(request)));

            var buildResult = await BuildInternalAsync(new ScriptBuildRequest
            {
                ScriptText = request.ScriptText,
                EnableSecurityCheck = true,
            }, cancellationToken);

            if (!buildResult.PublicBuildResult.Success)
            {
                var errorCode = buildResult.PublicBuildResult.SecurityIssues.Count > 0 ? "SECURITY_CHECK_FAILED" : "SCRIPT_BUILD_FAILED";
                return CacheResult(new ScriptRunResult
                {
                    Success = false,
                    EditorId = editorId,
                    TransactionName = GetTransactionName(request),
                    Diagnostics = buildResult.PublicBuildResult.Diagnostics,
                    Logs = Array.Empty<string>(),
                    ErrorCode = errorCode,
                    ErrorMessage = errorCode == "SECURITY_CHECK_FAILED" ? "Script blocked by security policy." : "Script build failed.",
                });
            }

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher is null)
                return CacheResult(CreateRunFailure("SCRIPT_RUNTIME_ERROR", "No dispatcher is available for script execution.", editorId, GetTransactionName(request)));

            if (dispatcher.CheckAccess())
                return CacheResult(await ExecuteBuiltScriptAsync(editor, editorId, request, buildResult.RawBuildResult, requireActiveEditor, cancellationToken));

            return CacheResult(await dispatcher.InvokeAsync(() =>
                ExecuteBuiltScriptAsync(editor, editorId, request, buildResult.RawBuildResult, requireActiveEditor, cancellationToken)).Task.Unwrap());
        }

        private async Task<ScriptRunResult> ExecuteBuiltScriptAsync(FumenVisualEditorViewModel editor, string editorId, ScriptRunRequest request, BuildResult buildResult, bool requireActiveEditor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!editorDocumentManager.GetCurrentEditors().Contains(editor))
                return CreateRunFailure("EDITOR_NOT_FOUND", $"Editor '{editorId}' is no longer opened.", editorId, GetTransactionName(request));

            if (requireActiveEditor && editorDocumentManager.CurrentActivatedEditor != editor)
                return CreateRunFailure("EDITOR_CHANGED", $"Editor '{editorId}' is no longer the active editor.", editorId, GetTransactionName(request));

            var logs = new List<string>();
            var combinedUndoAction = default(IUndoableAction);
            var hasOpenedUndoCombine = request.WrapUndoTransaction && editor.UndoRedoManager is not null;

            try
            {
                if (hasOpenedUndoCombine)
                    editor.UndoRedoManager.BeginCombineAction();

                var executeResult = await editorScriptExecutor.Execute(buildResult, editor);
                if (!executeResult.Success)
                {
                    if (hasOpenedUndoCombine)
                        _ = editor.UndoRedoManager.EndCombineAction(GetTransactionName(request));

                    return new ScriptRunResult
                    {
                        Success = false,
                        EditorId = editorId,
                        TransactionName = GetTransactionName(request),
                        Logs = logs,
                        Diagnostics = Array.Empty<ScriptDiagnostic>(),
                        ErrorCode = "SCRIPT_RUNTIME_ERROR",
                        ErrorMessage = executeResult.ErrorMessage,
                    };
                }

                if (hasOpenedUndoCombine)
                {
                    combinedUndoAction = editor.UndoRedoManager.EndCombineAction(GetTransactionName(request));
                    if (!IsEmptyCompositeAction(combinedUndoAction))
                        editor.UndoRedoManager.ExecuteAction(combinedUndoAction);
                }

                logs.Add("Script executed.");

                return new ScriptRunResult
                {
                    Success = true,
                    EditorId = editorId,
                    TransactionName = GetTransactionName(request),
                    ReturnValueJson = SerializeReturnValue(executeResult.Result, logs),
                    Logs = logs,
                    Diagnostics = Array.Empty<ScriptDiagnostic>(),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (hasOpenedUndoCombine)
                    TryDiscardCombinedAction(editor.UndoRedoManager, request);

                Log.LogError($"Runtime automation script execution failed: {ex}");
                return CreateRunFailure("SCRIPT_RUNTIME_ERROR", ex.Message, editorId, GetTransactionName(request), logs);
            }
            finally
            {
                if (buildResult.Assembly is not null)
                    ScriptArgsGlobalStore.Clear(buildResult.Assembly);
            }
        }

        private async Task<InternalBuildResult> BuildInternalAsync(ScriptBuildRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request ??= new ScriptBuildRequest();

            if (string.IsNullOrWhiteSpace(request.ScriptText))
            {
                return new InternalBuildResult
                {
                    PublicBuildResult = new ScriptBuildResult
                    {
                        Success = false,
                        Diagnostics =
                        [
                            new ScriptDiagnostic
                            {
                                Severity = "Error",
                                Message = "Script text is empty.",
                            }
                        ]
                    }
                };
            }

            if (request.EnableSecurityCheck)
            {
                var securityResult = scriptSecurityPolicy.Check(request.ScriptText);
                if (!securityResult.Success)
                {
                    return new InternalBuildResult
                    {
                        PublicBuildResult = new ScriptBuildResult
                        {
                            Success = false,
                            SecurityIssues = securityResult.Issues,
                        }
                    };
                }
            }

            var rawBuildResult = await editorScriptExecutor.Build(new BuildParam
            {
                Script = request.ScriptText,
                DisplayFileName = "McpRuntimeScript.cs",
            });

            return new InternalBuildResult
            {
                RawBuildResult = rawBuildResult,
                PublicBuildResult = new ScriptBuildResult
                {
                    Success = rawBuildResult.IsSuccess,
                    Diagnostics = ConvertDiagnostics(rawBuildResult.Errors, rawBuildResult.Warnings),
                    SecurityIssues = Array.Empty<string>(),
                }
            };
        }

        private static IReadOnlyList<ScriptDiagnostic> ConvertDiagnostics(IEnumerable<Diagnostic> errors, IEnumerable<Diagnostic> warnings)
        {
            errors ??= Array.Empty<Diagnostic>();
            warnings ??= Array.Empty<Diagnostic>();

            return errors.Concat(warnings)
                .Select(ConvertDiagnostic)
                .ToArray();
        }

        private static ScriptDiagnostic ConvertDiagnostic(Diagnostic diagnostic)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var hasLineInfo = lineSpan.IsValid;

            return new ScriptDiagnostic
            {
                Severity = diagnostic.Severity.ToString(),
                Message = diagnostic.ToString(),
                Line = hasLineInfo ? lineSpan.StartLinePosition.Line + 1 : default(int?),
                Column = hasLineInfo ? lineSpan.StartLinePosition.Character + 1 : default(int?),
            };
        }

        private static ScriptRunResult CreateRunFailure(string errorCode, string errorMessage, string editorId = default, string transactionName = default, IReadOnlyList<string> logs = default)
        {
            return new ScriptRunResult
            {
                Success = false,
                EditorId = editorId,
                TransactionName = transactionName,
                Logs = logs ?? Array.Empty<string>(),
                Diagnostics = Array.Empty<ScriptDiagnostic>(),
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
            };
        }

        private static string GetTransactionName(ScriptRunRequest request)
        {
            return string.IsNullOrWhiteSpace(request?.TransactionName) ? "MCP Script" : request.TransactionName;
        }

        private static string SerializeReturnValue(object result, ICollection<string> logs)
        {
            if (result is null)
                return default;

            try
            {
                return JsonSerializer.Serialize(result);
            }
            catch (Exception ex)
            {
                logs?.Add($"Return value serialization fell back to string: {ex.Message}");
                return JsonSerializer.Serialize(result.ToString());
            }
        }

        private static bool IsEmptyCompositeAction(IUndoableAction action)
        {
            return action is CompositeUndoAction compositeAction && !compositeAction.CombinedActions.Any();
        }

        private static void TryDiscardCombinedAction(IUndoRedoManager undoRedoManager, ScriptRunRequest request)
        {
            try
            {
                _ = undoRedoManager.EndCombineAction(GetTransactionName(request));
            }
            catch
            {
            }
        }

        private async Task<bool> ConfirmExecutionAsync(FumenVisualEditorViewModel editor, ScriptRunRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher.CheckAccess())
                return ConfirmExecutionCore(editor, request);

            return await dispatcher.InvokeAsync(() => ConfirmExecutionCore(editor, request)).Task;
        }

        private static bool ConfirmExecutionCore(FumenVisualEditorViewModel editor, ScriptRunRequest request)
        {
            var preview = request?.ScriptText ?? string.Empty;
            preview = preview.Replace("\r\n", "\n");
            if (preview.Length > 400)
                preview = preview[..400] + Environment.NewLine + "...";

            var requestedBy = string.IsNullOrWhiteSpace(request?.RequestedBy) ? default : request.RequestedBy.Trim();

            var message = $"About to execute a runtime script on '{editor?.DisplayName ?? "Unknown"}'." +
                          $"{Environment.NewLine}{Environment.NewLine}Transaction: {GetTransactionName(request)}" +
                          $"{(string.IsNullOrWhiteSpace(requestedBy) ? string.Empty : $"{Environment.NewLine}Requested by: {requestedBy}")}" +
                          $"{Environment.NewLine}{Environment.NewLine}Script preview:{Environment.NewLine}{preview}" +
                          $"{Environment.NewLine}{Environment.NewLine}Continue?";

            return MessageBox.Show(message, "Confirm MCP Script Execution", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        }

        private ScriptRunResult CacheResult(ScriptRunResult result)
        {
            lastResult = result;
            return result;
        }
    }
}
