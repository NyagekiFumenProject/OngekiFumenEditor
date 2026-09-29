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

        private static string NormalizeFamily(string objectType) => objectType?.Trim().ToLowerInvariant() ?? string.Empty;

        private static string NormalizePropertyName(string propertyName) => propertyName?.Trim() ?? string.Empty;

        private static bool IsEmptyCompositeAction(IUndoableAction action)
            => action is CompositeUndoAction composite && !composite.CombinedActions.Any();

        private static void TryRollback(IUndoableAction composite, EditorActionScopeSummary summary)
        {
            try
            {
                composite.Undo();
                summary.RolledBack = true;
            }
            catch (Exception ex)
            {
                summary.ErrorMessage = $"{summary.ErrorMessage} (rollback failed: {ex.Message})";
            }
        }

        private static void TrySilently(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // 单条动作的补偿失败不再向上冒泡：结果由 EditorActionOutcome 汇报，end_action 汇总时统一回滚。
            }
        }

        private static bool TryParseCursor(string cursor, out int? totalGrid, out int objectId)
        {
            totalGrid = default;
            objectId = default;

            if (string.IsNullOrWhiteSpace(cursor))
                return true;

            var parts = cursor.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalGrid)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedObjectId))
                return false;

            totalGrid = parsedTotalGrid;
            objectId = parsedObjectId;
            return true;
        }
    }
}
