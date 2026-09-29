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
        [McpServerTool(Name = "editor.add_object", Title = "Add Object", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Add a chart object and return its runtime object id. Supported objectType values: tap, flick, comment, bpm, bullet, bell, meter, clickse, enemy, lane, hold, soflan. Bullets require bulletPalleteStrId; holds take an optional endTGridUnit/endTGridGrid (add the end later with editor.create_hold_end); duration/interpolatable soflans require endTGridUnit/endTGridGrid, while a keyframe soflan is a single point and must not take them. Inside an action scope the object is queued until editor.end_action applies it.")]
        public async Task<object> AddObject(
            [Description("Object family: tap, flick, comment, bpm, bullet, bell, meter, clickse, enemy, lane, hold or soflan.")] string objectType,
            float tGridUnit = 0,
            int tGridGrid = 0,
            float xGridUnit = 0,
            int xGridGrid = 0,
            [Description("Applies to tap, flick and hold.")] bool? isCritical = default,
            [Description("Flick direction: left or right.")] string direction = default,
            [Description("Comment text.")] string content = default,
            [Description("BPM value for the bpm family.")] double? bpm = default,
            [Description("Bullet pallete StrID (see editor.query_bullet_pallete). Required for bullet; optional for bell, where \"--\" means the Ongeki default bell.")] string bulletPalleteStrId = default,
            [Description("Meter family: numerator (default 4).")] int? meterBunShi = default,
            [Description("Meter family: denominator (default 4).")] int? meterBunbo = default,
            [Description("Enemy family: Wave1, Wave2 or Boss (default Boss).")] string enemyWave = default,
            [Description("Lane family: center (default), left, right, colorful, enemy, wallLeft or wallRight.")] string laneType = default,
            [Description("Soflan family: duration (default), interpolatable or keyframe.")] string soflanType = default,
            [Description("End position for hold (optional) and soflan: TGrid unit. Required for duration/interpolatable soflans; forbidden for keyframe soflans (a keyframe is a single point, use tGrid*).")] float? endTGridUnit = default,
            [Description("End position for hold (optional) and soflan: TGrid grid (same rules as endTGridUnit).")] int? endTGridGrid = default,
            [Description("Soflan family: speed multiplier (default 1).")] double? speed = default,
            [Description("Soflan family: soflan group (default 0).")] int? soflanGroup = default,
            [Description("Soflan family: whether the speed applies in design mode (default false).")] bool? applySpeedInDesignMode = default,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.add_object";
            var family = NormalizeFamily(objectType);
            McpOperationLogHelper.LogRequest(operationName, new { family, tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPalleteStrId, meterBunShi, meterBunbo, enemyWave, laneType, soflanType, endTGridUnit, endTGridGrid, speed, soflanGroup, applySpeedInDesignMode, editorId, expectedEditorId, requestedBy, clientId });

            if (!CreatableFamilies.Contains(family))
                return Failure(operationName, "UNSUPPORTED_OBJECT_TYPE", $"editor.add_object supports {string.Join(", ", CreatableFamilies)}; '{objectType}' is not supported yet.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Add a {family} object at T[{tGridUnit},{tGridGrid}].", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            BulletPallete pallete = default;
            if (family is "bullet" or "bell")
            {
                var requestedPalleteId = bulletPalleteStrId?.Trim();
                if (string.IsNullOrEmpty(requestedPalleteId))
                {
                    if (family == "bullet")
                        return Failure(operationName, "MISSING_BULLET_PALLETE", "editor.add_object requires bulletPalleteStrId when objectType is 'bullet'.");
                }
                else if (requestedPalleteId == Bell.OngekiDefaultBellPaletteName)
                {
                    if (family == "bullet")
                        return Failure(operationName, "INVALID_ARGUMENT", $"'{Bell.OngekiDefaultBellPaletteName}' marks the default bell and cannot be used for a bullet.");
                }
                else
                {
                    pallete = editor.Fumen.BulletPalleteList[requestedPalleteId];
                    if (pallete is null)
                        return Failure(operationName, "PALLETE_NOT_FOUND", $"No bullet pallete '{requestedPalleteId}' in editor '{resolvedEditorId}'.");
                }
            }

            var spec = new ObjectCreateSpec
            {
                Family = family,
                TGrid = new TGrid(tGridUnit, tGridGrid),
                XGrid = new XGrid(xGridUnit, xGridGrid),
                IsCritical = isCritical,
                Direction = direction,
                Content = content,
                Bpm = bpm,
                Pallete = pallete,
                MeterBunShi = meterBunShi,
                MeterBunbo = meterBunbo,
                EnemyWave = enemyWave,
                LaneType = laneType,
                SoflanType = soflanType,
                Speed = speed,
                SoflanGroup = soflanGroup,
                ApplySpeedInDesignMode = applySpeedInDesignMode,
            };
            if (endTGridUnit is not null || endTGridGrid is not null)
                spec.EndTGrid = new TGrid(endTGridUnit ?? tGridUnit, endTGridGrid ?? 0);

            OngekiObjectBase obj;
            try
            {
                obj = CreateObject(spec);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

            var outcome = new EditorActionOutcome { Operation = "add_object", ObjectType = family, ObjectId = obj.Id };
            var fumen = editor.Fumen;
            // 尾端实例必须在建动作时捕获：撤销走 RemoveObject(hold) 会清掉 RefHold 链接，
            // 若在执行时才读 hold.HoldEnd，重做会丢尾端、撤销会留下孤儿尾端。
            var attachedHoldEnd = obj is Hold holdWithEnd ? holdWithEnd.HoldEnd : default;
            var action = LambdaUndoAction.Create(
                $"Add {family} #{obj.Id}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        if (attachedHoldEnd is not null && obj is Hold holdToLink && !ReferenceEquals(holdToLink.HoldEnd, attachedHoldEnd))
                            holdToLink.SetHoldEnd(attachedHoldEnd);
                        fumen.AddObject(obj);
                        if (attachedHoldEnd is not null)
                            fumen.AddObject(attachedHoldEnd);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.RemoveObject(obj));
                    }
                },
                () =>
                {
                    TrySilently(() => fumen.RemoveObject(obj));
                    if (attachedHoldEnd is not null)
                        TrySilently(() => fumen.RemoveObject(attachedHoldEnd));
                });

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
                objectId = obj.Id,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }
    }
}
