using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles;
using OngekiFumenEditor.Base.OngekiObjects.Projectiles.Enums;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorMutationTool
    {
        [McpServerTool(Name = "editor.end_action", Title = "End Editor Action Scope", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Close the action scope opened by editor.begin_action and apply the queued mutations as one undo entry. Fails and rolls back the whole batch when any queued operation failed. Set discard=true to drop the queued mutations without applying them.")]
        public async Task<object> EndAction(string name = default, string editorId = default, bool discard = false, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.end_action";
            McpOperationLogHelper.LogRequest(operationName, new { name, editorId, discard, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Close the editor action scope as '{name}'.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, default, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var identityKey = McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId);
            if (!actionScopeManager.TryTake(resolvedEditorId, identityKey, out var scope, out var errorCode, out var errorMessage))
                return Failure(operationName, errorCode, errorMessage);

            var transactionName = string.IsNullOrWhiteSpace(name) ? "Editor action scope" : name.Trim();
            var summary = await RuntimeUiDispatcher.RunAsync(() =>
            {
                var composite = editor.UndoRedoManager.EndCombineAction(transactionName);
                var result = new EditorActionScopeSummary
                {
                    EditorId = resolvedEditorId,
                    TransactionName = transactionName,
                    OutcomeCount = scope.Outcomes.Count,
                    Outcomes = scope.Outcomes,
                };

                if (discard)
                {
                    result.Applied = false;
                    result.ErrorCode = default;
                    return result;
                }

                if (IsEmptyCompositeAction(composite))
                {
                    result.Applied = false;
                    return result;
                }

                try
                {
                    editor.UndoRedoManager.ExecuteAction(composite);
                    result.Applied = true;

                    var failed = scope.Outcomes.Where(x => x.Executed && !x.Success).ToArray();
                    if (failed.Length > 0)
                    {
                        result.FailedCount = failed.Length;
                        result.ErrorCode = "ACTION_FAILED";
                        result.ErrorMessage = string.Join("; ", failed.Select(x => $"{x.Operation}#{x.ObjectId}: {x.ErrorMessage}"));

                        // 整批回滚后净效果等于“没有应用”，不要对外声称 applied=true。
                        result.Applied = false;
                        TryRollback(composite, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Applied = false;
                    result.ErrorCode = "ACTION_FAILED";
                    result.ErrorMessage = ex.Message;
                    TryRollback(composite, result);
                }

                return result;
            }, cancellationToken);

            var response = new
            {
                success = string.IsNullOrEmpty(summary.ErrorCode),
                editorId = summary.EditorId,
                transactionName = summary.TransactionName,
                applied = summary.Applied,
                rolledBack = summary.RolledBack,
                outcomeCount = summary.OutcomeCount,
                failedCount = summary.FailedCount,
                errorCode = summary.ErrorCode,
                errorMessage = summary.ErrorMessage,
                outcomes = summary.Outcomes.Select(x => new
                {
                    operation = x.Operation,
                    objectType = x.ObjectType,
                    objectId = x.ObjectId,
                    objectStrId = x.ObjectStrId,
                    executed = x.Executed,
                    success = x.Success,
                    errorMessage = x.ErrorMessage,
                }).ToArray(),
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
