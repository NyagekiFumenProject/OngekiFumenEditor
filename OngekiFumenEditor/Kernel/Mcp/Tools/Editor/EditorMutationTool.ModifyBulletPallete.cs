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
        [McpServerTool(Name = "editor.modify_bullet_pallete", Title = "Modify Bullet Pallete", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Modify one property of a bullet pallete addressed by its StrID. Supported properties: editorName, shooter, target, size, type, speed, placeOffset, randomOffsetRange (StrID itself and the derived IsEnableSoflan are not editable). One undoable editor action; inside an action scope the change is queued until editor.end_action.")]
        public async Task<object> ModifyBulletPallete(
            string strId,
            [Description("editorName, shooter, target, size, type, speed, placeOffset or randomOffsetRange.")] string propertyName,
            [Description("New value as text; parsed according to propertyName.")] string newValue,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.modify_bullet_pallete";
            var property = NormalizePropertyName(propertyName);
            McpOperationLogHelper.LogRequest(operationName, new { strId, propertyName = property, newValue, editorId, expectedEditorId, requestedBy, clientId });

            if (!SupportedPalleteProperties.Contains(property))
                return Failure(operationName, "UNSUPPORTED_PROPERTY", $"editor.modify_bullet_pallete supports {string.Join(", ", SupportedPalleteProperties)}; '{propertyName}' is not supported (StrID is immutable and IsEnableSoflan is derived from target).");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Set {property} of bullet pallete '{strId}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            var pallete = fumen.BulletPalleteList[strId?.Trim()];
            if (pallete is null)
                return Failure(operationName, "PALLETE_NOT_FOUND", $"No bullet pallete '{strId}' in editor '{resolvedEditorId}'.");

            string oldValue;
            try
            {
                oldValue = ReadPalleteProperty(pallete, property);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "UNSUPPORTED_PROPERTY", ex.Message);
            }

            var outcome = new EditorActionOutcome
            {
                Operation = "modify_bullet_pallete",
                ObjectType = BulletPalleteObjectType,
                ObjectId = BulletPalleteList.ConvertIdToInt(pallete.StrID),
                ObjectStrId = pallete.StrID,
            };
            var action = LambdaUndoAction.Create(
                $"Modify bullet pallete '{pallete.StrID}' {property}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        WritePalleteProperty(pallete, property, newValue);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => WritePalleteProperty(pallete, property, oldValue));
                    }
                },
                () => TrySilently(() => WritePalleteProperty(pallete, property, oldValue)));

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
                propertyName = property,
                oldValue,
                newValue,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
