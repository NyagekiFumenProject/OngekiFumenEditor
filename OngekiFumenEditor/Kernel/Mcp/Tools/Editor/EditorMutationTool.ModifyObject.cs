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
        [Description("Modify one property of a chart object addressed by its runtime object id. Supported properties: tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, tag (free text, \"\" clears it), bpm, bulletPallete (bullet/bell only, value is a bullet pallete StrID; \"\" clears it — a bullet then falls back to custom parameters — while \"--\" selects the Ongeki default bell and is bell-only), shooter/target/placeOffset/randomOffsetRange/speed (bullet/bell custom projectile parameters, writable only while no pallete is set), size/type/bulletDamageType (bullet-only custom projectile parameters), bunShi/bunbo (meter), enemyWave (enemy), endTGridUnit/endTGridGrid (hold with an end, soflan, isfarea or laneblock), speed/soflanGroup/applySpeedInDesignMode (soflan; soflanGroup also applies to isfarea), widthId (beam, 1-5), obliqueSourceXGridUnit/obliqueSourceXGridGrid (beam; \"\" clears the oblique source), colorId/brightness (colorful lane), isTransparent (lane starts, true/false), endXGridUnit/endXGridGrid (isfarea), blockDirection (laneblock, left/right), referenceLaneRecordId (tap/hold only, value is a lane RecordId; \"\" or -1 clears the lane binding and leaves the object floating). Inside an action scope the change is queued until editor.end_action applies it.")]
        public async Task<object> ModifyObject(
            int objectId,
            [Description("tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, tag, bpm, bulletPallete, shooter, target, size, type, bulletDamageType, placeOffset, randomOffsetRange, bunShi, bunbo, enemyWave, endTGridUnit, endTGridGrid, speed, soflanGroup, applySpeedInDesignMode, widthId, obliqueSourceXGridUnit, obliqueSourceXGridGrid, colorId, brightness, isTransparent, endXGridUnit, endXGridGrid, blockDirection or referenceLaneRecordId.")] string propertyName,
            [Description("New value as text; parsed according to propertyName.")] string newValue,
            [Description("tap/hold only: after the write, snap XGrid (and the HoldEnd XGrid, when present) back onto the docked lane. Only valid with referenceLaneRecordId, tGridUnit or tGridGrid, and only when the object is bound to a lane. Default false (never snaps).")] bool? snapXToLane = default,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.modify_object";
            var property = NormalizePropertyName(propertyName);
            var snap = snapXToLane == true;
            McpOperationLogHelper.LogRequest(operationName, new { objectId, propertyName = property, newValue, snapXToLane, editorId, expectedEditorId, requestedBy, clientId });

            if (!SupportedModifyProperties.Contains(property))
                return Failure(operationName, "UNSUPPORTED_PROPERTY", $"editor.modify_object supports {string.Join(", ", SupportedModifyProperties)}; '{propertyName}' is not supported.");

            if (snap)
            {
                if (property is not (ReferenceLaneRecordIdProperty or "tGridUnit" or "tGridGrid"))
                    return Failure(operationName, "INVALID_ARGUMENT", $"snapXToLane only applies to {ReferenceLaneRecordIdProperty}, tGridUnit or tGridGrid; '{property}' was requested.");

                if (property == ReferenceLaneRecordIdProperty && !BindsLane(operationName, newValue, out var laneParseError))
                    return laneParseError;
            }
            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Set {property} of object #{objectId}.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (!TryFindObject(editor.Fumen, objectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {objectId} was found in editor '{resolvedEditorId}'.");

            // §42：palette 已提供这些值；custom 参数在 palette 非空时按属性浏览器规则只读，必须先清掉 bulletPallete。
            if (ProjectileCustomProperties.Contains(property) && obj is IBulletPalleteReferencable { ReferenceBulletPallete: not null })
                return Failure(operationName, "INVALID_ARGUMENT", $"Object #{objectId} has a bullet pallete; custom projectile parameters are read-only while it is set. Clear 'bulletPallete' first.");

            if (snap && obj is not ILaneDockable)
                return Failure(operationName, "UNSUPPORTED_PROPERTY", $"Object #{objectId} ({family}) cannot dock to a lane; snapXToLane only applies to tap and hold.");

            if (snap && property != ReferenceLaneRecordIdProperty && obj is ILaneDockable unboundDockable && unboundDockable.ReferenceLaneStart is null)
                return Failure(operationName, "INVALID_ARGUMENT", $"Object #{objectId} is not bound to a lane; set {ReferenceLaneRecordIdProperty} before using snapXToLane.");

            string oldValue;
            try
            {
                oldValue = ReadProperty(obj, property);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "UNSUPPORTED_PROPERTY", ex.Message);
            }

            // 吸附改的是 XGrid，不属于被改的那个属性本身 —— undo / 回滚要单独把这几个 XGrid 还原。
            var xGridSnapshot = snap ? DockableXGridSnapshot.Capture(obj) : default;
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
                        if (snap)
                            SnapDockableXGridToBoundLane(obj);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => RollbackModify(obj, property, oldValue, fumen, xGridSnapshot));
                    }
                },
                () => TrySilently(() => RollbackModify(obj, property, oldValue, fumen, xGridSnapshot)));

            await RuntimeUiDispatcher.RunAsync(() =>
            {
                editor.UndoRedoManager.ExecuteAction(action);
                return true;
            }, cancellationToken);

            var queued = actionScopeManager.TryTrack(resolvedEditorId, McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId), outcome);
            // 属性写入失败（取值非法、越界等）必须带 errorCode 回去，否则调用方只能靠解析 errorMessage 才能分支。
            var failed = !queued && !outcome.Success;
            var response = new
            {
                success = queued || outcome.Success,
                editorId = resolvedEditorId,
                objectType = family,
                objectId,
                propertyName = property,
                oldValue,
                newValue,
                snapped = snap,
                applied = !queued,
                queued,
                errorCode = failed ? "INVALID_ARGUMENT" : default,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        /// <summary>
        /// 撤销 / 回滚一次 modify：先把属性本身写回旧值，再还原吸附动过的 XGrid。
        /// 顺序不能反 —— 写回 <c>referenceLaneRecordId</c> 会触发 HoldEnd.RedockXGrid()，
        /// 得让它先跑完再用快照把位置盖回原样。
        /// </summary>
        private static void RollbackModify(OngekiObjectBase obj, string property, string oldValue, OngekiFumen fumen, DockableXGridSnapshot xGridSnapshot)
        {
            WriteProperty(obj, property, oldValue, fumen);
            xGridSnapshot?.Restore(obj);
        }

        /// <summary>
        /// 校验「写入某个 lane 引用」的取值是否真的绑上 lane（而不是清空）。解析失败直接把错误结果交回去。
        /// </summary>
        private static bool BindsLane(string operationName, string newValue, out object failure)
        {
            failure = default;
            try
            {
                if (TryParseLaneRecordId(newValue, out _))
                    return true;

                failure = Failure(operationName, "INVALID_ARGUMENT", "snapXToLane cannot be combined with clearing the lane binding; pass a lane RecordId or drop snapXToLane.");
                return false;
            }
            catch (Exception ex)
            {
                failure = Failure(operationName, "INVALID_ARGUMENT", ex.Message);
                return false;
            }
        }
    }
}
