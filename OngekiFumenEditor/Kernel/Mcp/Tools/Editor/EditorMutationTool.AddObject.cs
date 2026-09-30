using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
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
        [Description("Add a chart object and return its runtime object id. Supported objectType values: tap, flick, comment, bpm, bullet, bell, meter, clickse, enemy, lane, hold, soflan, lanenext, beam, beamnext, curvecontrol, isfarea, laneblock. Bullets require bulletPalleteStrId; holds take an optional endTGridUnit/endTGridGrid (add the end later with editor.create_hold_end); duration/interpolatable soflans require endTGridUnit/endTGridGrid, while a keyframe soflan is a single point and must not take them. tap and hold optionally dock to a lane via referenceLaneRecordId (+ snapXToLane to put the object exactly on that lane). lanenext/beamnext extend an existing lane/beam start: they need parentRecordId (the start's recordId) and must NOT take endTGrid*. curvecontrol bends a lane segment and needs referenceObjectId (the segment's object id from objectType='lanenext'). isfarea and laneblock are ranges and require endTGridUnit/endTGridGrid. Inside an action scope the object is queued until editor.end_action applies it.")]
        public async Task<object> AddObject(
            [Description("Object family: tap, flick, comment, bpm, bullet, bell, meter, clickse, enemy, lane, hold, soflan, lanenext, beam, beamnext, curvecontrol, isfarea or laneblock.")] string objectType,
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
            [Description("Lane family: center (default), left, right, colorful, enemy, wallLeft, wallRight or autoplayFader.")] string laneType = default,
            [Description("Soflan family: duration (default), interpolatable or keyframe.")] string soflanType = default,
            [Description("End position for hold (optional) and for the range families soflan, isfarea and laneblock: TGrid unit. Required for duration/interpolatable soflans, isfarea and laneblock; forbidden for keyframe soflans and for the single-point families lanenext/beamnext/curvecontrol.")] float? endTGridUnit = default,
            [Description("End position: TGrid grid (same rules as endTGridUnit).")] int? endTGridGrid = default,
            [Description("Soflan family: speed multiplier (default 1).")] double? speed = default,
            [Description("Soflan family / isfarea: soflan group (default 0).")] int? soflanGroup = default,
            [Description("Soflan family: whether the speed applies in design mode (default false).")] bool? applySpeedInDesignMode = default,
            [Description("tap/hold only: RecordId of the lane to dock to (see editor.query_object objectType='lane'). Omit, or pass a negative value, to leave the object floating.")] int? referenceLaneRecordId = default,
            [Description("tap/hold only: after docking, overwrite XGrid (and, for holds with an end, the HoldEnd XGrid) with the position the lane computes at that TGrid. Requires referenceLaneRecordId; fails when the lane has no path at that TGrid. Default false.")] bool? snapXToLane = default,
            [Description("lanenext / beamnext only: RecordId of the owning lane or beam start (from editor.query_object objectType='lane' or 'beam'). Required: an extension segment must hang off an existing start.")] int? parentRecordId = default,
            [Description("curvecontrol only: object id of the lane segment to bend (from editor.query_object objectType='lanenext'). Required.")] int? referenceObjectId = default,
            [Description("beam / beamnext only: beam width id, 1 to 5 (default 1).")] int? widthId = default,
            [Description("beam / beamnext only: XGrid unit of the oblique beam source offset. Giving either oblique component turns the beam oblique (OBS).")] float? obliqueSourceXGridUnit = default,
            [Description("beam / beamnext only: XGrid grid of the oblique beam source offset.")] int? obliqueSourceXGridGrid = default,
            [Description("lane/colorful or lanenext on a colorful lane: colour name (e.g. Akari, Yuzu, Rio) or numeric id, from ColorIdConst.")] string colorId = default,
            [Description("lane/colorful or lanenext on a colorful lane: brightness (default 3).")] int? brightness = default,
            [Description("isfarea only: XGrid unit of the area's far edge; that edge minus xGridUnit* is the area width. Defaults to xGridUnit* (zero width).")] float? endXGridUnit = default,
            [Description("isfarea only: XGrid grid of the area's far edge.")] int? endXGridGrid = default,
            [Description("laneblock only: left (default) or right.")] string blockDirection = default,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.add_object";
            var family = NormalizeFamily(objectType);
            McpOperationLogHelper.LogRequest(operationName, new { family, tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPalleteStrId, meterBunShi, meterBunbo, enemyWave, laneType, soflanType, endTGridUnit, endTGridGrid, speed, soflanGroup, applySpeedInDesignMode, referenceLaneRecordId, snapXToLane, parentRecordId, referenceObjectId, widthId, obliqueSourceXGridUnit, obliqueSourceXGridGrid, colorId, brightness, endXGridUnit, endXGridGrid, blockDirection, editorId, expectedEditorId, requestedBy, clientId });

            if (!CreatableFamilies.Contains(family))
                return Failure(operationName, "UNSUPPORTED_OBJECT_TYPE", $"editor.add_object supports {string.Join(", ", CreatableFamilies)}; '{objectType}' is not supported yet.");

            // §58：负数（UI 的 -1 哨兵）与不传同义 —— 不绑定 lane。
            var laneRecordId = referenceLaneRecordId is { } requestedLaneRecordId && requestedLaneRecordId >= 0 ? requestedLaneRecordId : (int?)null;
            if ((laneRecordId is not null || snapXToLane is not null) && family is not ("tap" or "hold"))
                return Failure(operationName, "INVALID_ARGUMENT", $"referenceLaneRecordId and snapXToLane only apply to tap and hold; '{family}' cannot dock to a lane.");
            if (snapXToLane == true && laneRecordId is null)
                return Failure(operationName, "INVALID_ARGUMENT", $"snapXToLane requires a non-negative referenceLaneRecordId: there is no lane to snap to.");

            if (parentRecordId is not null && family is not ("lanenext" or "beamnext"))
                return Failure(operationName, "INVALID_ARGUMENT", $"parentRecordId only applies to lanenext and beamnext; '{family}' has no owning start.");
            if (family is "lanenext" or "beamnext" && parentRecordId is null)
                return Failure(operationName, "INVALID_ARGUMENT", $"The {family} family requires parentRecordId: the segment must hang off an existing {(family == "lanenext" ? "lane" : "beam")} start.");

            if (referenceObjectId is not null && family != "curvecontrol")
                return Failure(operationName, "INVALID_ARGUMENT", $"referenceObjectId only applies to curvecontrol; '{family}' does not attach to a lane segment.");
            if (family == "curvecontrol" && referenceObjectId is null)
                return Failure(operationName, "INVALID_ARGUMENT", "The curvecontrol family requires referenceObjectId: the object id of the lane segment it bends.");

            // 家族专属参数不允许串用：静默忽略参数会让调用方以为生效了。
            if ((widthId is not null || obliqueSourceXGridUnit is not null || obliqueSourceXGridGrid is not null) && family is not ("beam" or "beamnext"))
                return Failure(operationName, "INVALID_ARGUMENT", $"widthId and obliqueSourceXGridUnit/obliqueSourceXGridGrid only apply to beam and beamnext; '{family}' has no beam width.");
            if ((colorId is not null || brightness is not null) && family is not ("lane" or "lanenext"))
                return Failure(operationName, "INVALID_ARGUMENT", $"colorId and brightness only apply to lane and lanenext; '{family}' is not a lane.");
            if ((endXGridUnit is not null || endXGridGrid is not null) && family != "isfarea")
                return Failure(operationName, "INVALID_ARGUMENT", $"endXGridUnit/endXGridGrid only apply to isfarea; '{family}' has no second X position.");
            if (blockDirection is not null && family != "laneblock")
                return Failure(operationName, "INVALID_ARGUMENT", $"blockDirection only applies to laneblock; '{family}' is not a lane block.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Add a {family} object at T[{tGridUnit},{tGridGrid}].", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            LaneStartBase referenceLane = default;
            if (laneRecordId is { } laneToResolve)
            {
                referenceLane = editor.Fumen.Lanes.FirstOrDefault(x => x.RecordId == laneToResolve);
                if (referenceLane is null)
                    return Failure(operationName, "LANE_NOT_FOUND", $"No lane with RecordId {laneToResolve} in editor '{resolvedEditorId}'. List lanes with editor.query_object objectType='lane'.");
            }

            // 延伸段挂靠的起点 / 曲线控制点要弯曲的那一段。
            ConnectableStartObject parentStart = default;
            ConnectableChildObjectBase curveTarget = default;
            try
            {
                if (family == "lanenext")
                    parentStart = ResolveLaneStartByRecordId(editor.Fumen, parentRecordId!.Value);
                else if (family == "beamnext")
                    parentStart = ResolveBeamStartByRecordId(editor.Fumen, parentRecordId!.Value);
                else if (family == "curvecontrol")
                    curveTarget = ResolveLaneSegmentByObjectId(editor.Fumen, referenceObjectId!.Value);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

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

            ObjectCreateSpec spec;
            try
            {
                spec = new ObjectCreateSpec
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
                    ReferenceLane = referenceLane,
                    SnapXToLane = snapXToLane == true,
                    ParentStart = parentStart,
                    CurveTarget = curveTarget,
                    WidthId = widthId,
                    ObliqueSourceXGrid = obliqueSourceXGridUnit is null && obliqueSourceXGridGrid is null
                        ? null
                        : new XGrid(obliqueSourceXGridUnit ?? 0, obliqueSourceXGridGrid ?? 0),
                    ColorId = colorId is null ? null : ParseColorId(colorId),
                    Brightness = brightness,
                    EndXGrid = endXGridUnit is null && endXGridGrid is null
                        ? null
                        : new XGrid(endXGridUnit ?? xGridUnit, endXGridGrid ?? xGridGrid),
                    BlockDirection = blockDirection,
                };
                if (endTGridUnit is not null || endTGridGrid is not null)
                    spec.EndTGrid = new TGrid(endTGridUnit ?? tGridUnit, endTGridGrid ?? 0);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

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
            // 曲线控制点的增删不走 fumen（OngekiFumen.AddObject 不认识它），必须挂到具体延伸段上；
            // 而 RemoveControlObject 会清掉 RefCurveObject，所以 owner 要在建动作时就捕获。
            var applyObject = BuildAddObject(fumen, obj, curveTarget);
            var revertObject = BuildRemoveObject(fumen, obj);
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
                        applyObject();
                        if (attachedHoldEnd is not null)
                            fumen.AddObject(attachedHoldEnd);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(revertObject);
                    }
                },
                () =>
                {
                    TrySilently(revertObject);
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
