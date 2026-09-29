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
        // ---------------- hold end tools ----------------

        [McpServerTool(Name = "editor.create_hold_end", Title = "Create Hold End", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Attach a hold end (HoldEnd) to a hold addressed by its runtime object id, giving the hold its length. One undoable editor action (visible in the history); inside an action scope it is queued until editor.end_action.")]
        public async Task<object> CreateHoldEnd(
            int holdObjectId,
            float tGridUnit,
            int tGridGrid = 0,
            float xGridUnit = 0,
            int xGridGrid = 0,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.create_hold_end";
            McpOperationLogHelper.LogRequest(operationName, new { holdObjectId, tGridUnit, tGridGrid, xGridUnit, xGridGrid, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Create the hold end of object #{holdObjectId} at T[{tGridUnit},{tGridGrid}].", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            if (!TryFindObject(fumen, holdObjectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {holdObjectId} was found in editor '{resolvedEditorId}'.");
            if (obj is not Hold hold)
                return Failure(operationName, "NOT_A_HOLD", $"Object #{holdObjectId} is a {family}, not a hold.");
            if (hold.HoldEnd is not null)
                return Failure(operationName, "HOLD_END_ALREADY_EXISTS", $"Hold #{holdObjectId} already has an end; adjust it with editor.modify_object (endTGridUnit/endTGridGrid) or drop it with editor.remove_hold_end.");

            var endTGrid = new TGrid(tGridUnit, tGridGrid);
            if (endTGrid <= hold.TGrid)
                return Failure(operationName, "INVALID_ARGUMENT", $"The hold end must be after the hold start (end {endTGrid} <= start {hold.TGrid}).");

            var holdEnd = new HoldEnd
            {
                TGrid = endTGrid,
                XGrid = new XGrid(xGridUnit, xGridGrid),
            };
            var outcome = new EditorActionOutcome { Operation = "create_hold_end", ObjectType = family, ObjectId = hold.Id };
            var action = LambdaUndoAction.Create(
                $"Create hold end of #{hold.Id}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        hold.SetHoldEnd(holdEnd);
                        fumen.AddObject(holdEnd);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.RemoveObject(holdEnd));
                    }
                },
                () => TrySilently(() => fumen.RemoveObject(holdEnd)));

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
                holdEndObjectId = holdEnd.Id,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
