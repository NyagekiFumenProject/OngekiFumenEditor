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
        [McpServerTool(Name = "editor.create_bullet_pallete", Title = "Create Bullet Pallete", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Create a bullet pallete (BPL) and return its StrID. One undoable editor action (visible in the history); inside an action scope it is queued until editor.end_action. The StrID is allocated up-front so it is returned even before the change is applied; if that scope is discarded the id stays unused forever.")]
        public async Task<object> CreateBulletPallete(
            [Description("Optional explicit StrID ([0-9A-Za-z]+, unique). Omit to let the editor allocate the next one.")] string strId = default,
            [Description("Optional display name shown in the pallete list.")] string editorName = default,
            [Description("Optional shooter enum value; defaults to Center.")] string shooter = default,
            [Description("Optional target enum value; defaults to FixField.")] string target = default,
            [Description("Optional size enum value; defaults to Normal.")] string size = default,
            [Description("Optional type enum value; defaults to Circle.")] string type = default,
            [Description("Optional speed multiplier; defaults to 1.")] double? speed = default,
            [Description("Optional place offset in TGrid units.")] int? placeOffset = default,
            [Description("Optional random offset range.")] int? randomOffsetRange = default,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.create_bullet_pallete";
            McpOperationLogHelper.LogRequest(operationName, new { strId, editorName, shooter, target, size, type, speed, placeOffset, randomOffsetRange, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Create bullet pallete '{strId ?? "(auto)"}'.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            string resolvedStrId;
            if (!string.IsNullOrWhiteSpace(strId))
            {
                var requestedId = strId.Trim();
                if (!IsValidPalleteStrId(requestedId))
                    return Failure(operationName, "INVALID_ARGUMENT", $"StrID '{requestedId}' is invalid; use letters and digits only (the pallete id alphabet).");
                if (fumen.BulletPalleteList[requestedId] is not null)
                    return Failure(operationName, "DUPLICATE_STR_ID", $"Bullet pallete '{requestedId}' already exists in editor '{resolvedEditorId}'.");
                resolvedStrId = requestedId.ToUpperInvariant();
            }
            else
            {
                resolvedStrId = fumen.BulletPalleteList.AllocateStrID();
            }

            BulletPallete pallete;
            try
            {
                pallete = new BulletPallete { StrID = resolvedStrId };
                if (!string.IsNullOrWhiteSpace(editorName))
                    pallete.EditorName = editorName;
                if (!string.IsNullOrWhiteSpace(shooter))
                    pallete.ShooterValue = ParsePalleteEnum<Shooter>(shooter, "shooter");
                if (!string.IsNullOrWhiteSpace(target))
                    pallete.TargetValue = ParsePalleteEnum<Target>(target, "target");
                if (!string.IsNullOrWhiteSpace(size))
                    pallete.SizeValue = ParsePalleteEnum<BulletSize>(size, "size");
                if (!string.IsNullOrWhiteSpace(type))
                    pallete.TypeValue = ParsePalleteEnum<BulletType>(type, "type");
                if (speed is { } speedValue)
                {
                    if (double.IsNaN(speedValue) || double.IsInfinity(speedValue))
                        throw new ArgumentException("'speed' must be a finite number.");
                    pallete.Speed = (float)speedValue;
                }
                if (placeOffset is { } placeOffsetValue)
                    pallete.PlaceOffset = placeOffsetValue;
                if (randomOffsetRange is { } randomOffsetRangeValue)
                    pallete.RandomOffsetRange = randomOffsetRangeValue;
            }
            catch (ArgumentException ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

            var outcome = new EditorActionOutcome
            {
                Operation = "create_bullet_pallete",
                ObjectType = BulletPalleteObjectType,
                ObjectId = BulletPalleteList.ConvertIdToInt(resolvedStrId),
                ObjectStrId = resolvedStrId,
            };
            var action = LambdaUndoAction.Create(
                $"Create bullet pallete '{resolvedStrId}'",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        fumen.AddObject(pallete);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.RemoveObject(pallete));
                    }
                },
                () => TrySilently(() => fumen.RemoveObject(pallete)));

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
                strId = resolvedStrId,
                editorName = pallete.EditorName,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
