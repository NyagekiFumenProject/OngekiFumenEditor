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
        [McpServerTool(Name = "editor.modify_object", Title = "Modify Object", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Modify one property of a chart object addressed by its runtime object id. Supported properties: tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPallete (bullet/bell only, value is a bullet pallete StrID; \"\" or \"--\" clears it for bells), bunShi/bunbo (meter), enemyWave (enemy), endTGridUnit/endTGridGrid (hold with an end, or any soflan), speed/soflanGroup/applySpeedInDesignMode (soflan). Inside an action scope the change is queued until editor.end_action applies it.")]
        public async Task<object> ModifyObject(
            int objectId,
            [Description("tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPallete, bunShi, bunbo, enemyWave, endTGridUnit, endTGridGrid, speed, soflanGroup or applySpeedInDesignMode.")] string propertyName,
            [Description("New value as text; parsed according to propertyName.")] string newValue,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.modify_object";
            var property = NormalizePropertyName(propertyName);
            McpOperationLogHelper.LogRequest(operationName, new { objectId, propertyName = property, newValue, editorId, expectedEditorId, requestedBy, clientId });

            if (!SupportedModifyProperties.Contains(property))
                return Failure(operationName, "UNSUPPORTED_PROPERTY", $"editor.modify_object supports {string.Join(", ", SupportedModifyProperties)}; '{propertyName}' is not supported.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Set {property} of object #{objectId}.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (!TryFindObject(editor.Fumen, objectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {objectId} was found in editor '{resolvedEditorId}'.");

            string oldValue;
            try
            {
                oldValue = ReadProperty(obj, property);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "UNSUPPORTED_PROPERTY", ex.Message);
            }

            var fumen = editor.Fumen;
            var outcome = new EditorActionOutcome { Operation = "modify_object", ObjectType = family, ObjectId = objectId };
            var action = LambdaUndoAction.Create(
                $"Modify {family} #{objectId} {property}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        WriteProperty(obj, property, newValue, fumen);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => WriteProperty(obj, property, oldValue, fumen));
                    }
                },
                () => TrySilently(() => WriteProperty(obj, property, oldValue, fumen)));

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
