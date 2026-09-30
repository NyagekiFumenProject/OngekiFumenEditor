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
        [McpServerTool(Name = "editor.remove_bullet_pallete", Title = "Remove Bullet Pallete", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Remove a bullet pallete addressed by its StrID. Refused while any bullet or bell still references it (errorCode PALLETE_IN_USE plus a sample of the referencing object ids); bullets have no default pallete to fall back to. One undoable editor action; inside an action scope the removal is queued until editor.end_action.")]
        public async Task<object> RemoveBulletPallete(
            string strId,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.remove_bullet_pallete";
            McpOperationLogHelper.LogRequest(operationName, new { strId, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Remove bullet pallete '{strId}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            var pallete = LookupBulletPallete(fumen, strId?.Trim());
            if (pallete is null)
                return Failure(operationName, "PALLETE_NOT_FOUND", $"No bullet pallete '{strId}' in editor '{resolvedEditorId}'.");

            var referencing = EnumeratePalleteReferences(fumen)
                .Where(x => PalleteMatches(x.ReferenceBulletPallete, pallete))
                .ToArray();
            if (referencing.Length > 0)
            {
                var sample = string.Join(", ", referencing.OfType<OngekiObjectBase>().Select(x => $"#{x.Id}").Take(10));
                return Failure(operationName, "PALLETE_IN_USE", $"Bullet pallete '{pallete.StrID}' is referenced by {referencing.Length} object(s) ({sample}); reassign or remove them first.");
            }

            var outcome = new EditorActionOutcome
            {
                Operation = "remove_bullet_pallete",
                ObjectType = BulletPalleteObjectType,
                ObjectId = BulletPalleteList.ConvertIdToInt(pallete.StrID),
                ObjectStrId = pallete.StrID,
            };
            var action = LambdaUndoAction.Create(
                $"Remove bullet pallete '{pallete.StrID}'",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        fumen.RemoveObject(pallete);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.AddObject(pallete));
                    }
                },
                () => TrySilently(() => fumen.AddObject(pallete)));

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
                strId = pallete.StrID,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
