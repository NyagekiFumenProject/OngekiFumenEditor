using ModelContextProtocol.Server;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorDocumentTool
    {
        [McpServerTool(Name = "editor.close", Title = "Close Editor", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Close an opened editor tab (see editor.list_opened). A dirty editor is refused with EDITOR_DIRTY so the caller can save or undo first; pass force=true to discard the unsaved changes and close anyway. Closing never saves, is not undoable, and shows no dialogs. Omit editorId to close the active editor.")]
        public async Task<object> Close(
            [Description("Editor id from editor.list_opened; omit to target the active editor.")] string editorId = default,
            [Description("Fail with EDITOR_CHANGED when the resolved editor is not this id.")] string expectedEditorId = default,
            [Description("Discard unsaved changes instead of refusing with EDITOR_DIRTY. Default false.")] bool force = false,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.close";
            McpOperationLogHelper.LogRequest(operationName, new { editorId, expectedEditorId, force, requireConfirmation, requestedBy, clientId });

            var preview = $"Close editor '{editorId ?? "(active)"}'{(force ? " and discard unsaved changes" : string.Empty)}.";
            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, preview, requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            (bool closed, bool refusedDirty, bool wasDirty) outcome;
            try
            {
                // dirty 判定与关闭必须同处 UI 线程：两次派发之间用户/自动保存都可能改掉 IsDirty。
                outcome = await RuntimeUiDispatcher.RunAsync(async () =>
                {
                    var dirty = editor.IsDirty;
                    if (dirty && !force)
                        return (closed: false, refusedDirty: true, wasDirty: true);

                    // 关闭本身走正常流程（TryCloseAsync，与用户点标签页的 X 同一条路），
                    // 但先声明「dirty 策略已定」，避开 Caliburn CloseStrategy 里那次「是否保存」模态框。
                    editor.RequestCloseWithoutPrompt();
                    await editor.TryCloseAsync(null);

                    var stillOpen = editorDocumentManager.GetEditorSnapshot()
                        .Any(x => RuntimeAutomationEditorId.Generate(x) == resolvedEditorId);
                    return (closed: !stillOpen, refusedDirty: false, wasDirty: dirty);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "CLOSE_FAILED", ex.Message);
            }

            if (outcome.refusedDirty)
                return Failure(operationName, "EDITOR_DIRTY", $"Editor '{resolvedEditorId}' has unsaved changes. Save it in the app, drop them with editor.undo, or call editor.close with force=true to discard them.");

            if (!outcome.closed)
                return Failure(operationName, "CLOSE_FAILED", $"Editor '{resolvedEditorId}' is still open after the close request.");

            var remaining = editorDocumentManager.GetEditorSnapshot()
                .Select(RuntimeAutomationEditorId.Generate)
                .ToArray();
            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                closed = true,
                wasDirty = outcome.wasDirty,
                discardedUnsavedChanges = outcome.wasDirty && force,
                remainingEditorCount = remaining.Length,
                remainingEditorIds = remaining,
                activeEditorId = RuntimeAutomationEditorId.Generate(editorDocumentManager.CurrentActivatedEditor),
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
