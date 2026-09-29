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
        [McpServerTool(Name = "editor.remove_hold_end", Title = "Remove Hold End", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Remove the hold end of a hold addressed by its runtime object id (the hold becomes zero-length again). One undoable editor action; inside an action scope it is queued until editor.end_action.")]
        public async Task<object> RemoveHoldEnd(
            int holdObjectId,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.remove_hold_end";
            McpOperationLogHelper.LogRequest(operationName, new { holdObjectId, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Remove the hold end of object #{holdObjectId}.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            if (!TryFindObject(fumen, holdObjectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {holdObjectId} was found in editor '{resolvedEditorId}'.");
            if (obj is not Hold hold)
                return Failure(operationName, "NOT_A_HOLD", $"Object #{holdObjectId} is a {family}, not a hold.");
            if (hold.HoldEnd is not { } holdEnd)
                return Failure(operationName, "HOLD_END_NOT_FOUND", $"Hold #{holdObjectId} has no end to remove.");

            var outcome = new EditorActionOutcome { Operation = "remove_hold_end", ObjectType = family, ObjectId = hold.Id };
            var action = LambdaUndoAction.Create(
                $"Remove hold end of #{hold.Id}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        fumen.RemoveObject(holdEnd);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.AddObject(holdEnd));
                    }
                },
                () => TrySilently(() =>
                {
                    hold.SetHoldEnd(holdEnd);
                    fumen.AddObject(holdEnd);
                }));

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
                holdObjectId = hold.Id,
                removedHoldEndObjectId = holdEnd.Id,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
