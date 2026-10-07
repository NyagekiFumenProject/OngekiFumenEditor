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
        [McpServerTool(Name = "editor.remove_object", Title = "Remove Object", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Remove a chart object addressed by its runtime object id. Inside an action scope the removal is queued until editor.end_action applies it.")]
        public async Task<object> RemoveObject(int objectId, string editorId = default, string expectedEditorId = default, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.remove_object";
            McpOperationLogHelper.LogRequest(operationName, new { objectId, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Remove object #{objectId}.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (!TryFindObject(editor.Fumen, objectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {objectId} was found in editor '{resolvedEditorId}'.");

            var outcome = new EditorActionOutcome { Operation = "remove_object", ObjectType = family, ObjectId = objectId };
            var fumen = editor.Fumen;
            // 曲线控制点的还原同样不能走 fumen.AddObject；而且必须在真正移除之前捕获 owner，
            // 因为 RemoveControlObject 会把 RefCurveObject 清空。
            var restoreObject = BuildAddObject(fumen, obj);
            var removeObject = BuildRemoveObject(fumen, obj);
            var action = LambdaUndoAction.Create(
                $"Remove {family} #{objectId}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        removeObject();
                        outcome.Success = true;
                        // §27：写操作显式标脏（remove 不触发被删对象的属性变更通知）。
                        editor.IsDirty = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(restoreObject);
                    }
                },
                () => TrySilently(restoreObject));

            await RuntimeUiDispatcher.RunAsync(() =>
            {
                editor.UndoRedoManager.ExecuteAction(action);
                return true;
            }, cancellationToken);

            var queued = actionScopeManager.TryTrack(resolvedEditorId, McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId), outcome);
            var response = new
            {
                success = queued || outcome.Success,
                editorId = resolvedEditorId,
                objectType = family,
                objectId,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
