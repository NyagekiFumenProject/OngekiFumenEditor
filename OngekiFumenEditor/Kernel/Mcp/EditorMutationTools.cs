using Gemini.Modules.UndoRedo;
using Gemini.Modules.UndoRedo.UndoAction;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.Collections;
using OngekiFumenEditor.Base.Collections.Base;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects;
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

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(EditorMutationTools))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorMutationTools
    {
        private static readonly string[] QuerableFamilies =
        {
            "tap", "flick", "hold", "bell", "bullet", "comment", "bpm", "meter", "clickse", "enemy", "lane", "soflan",
        };

        private static readonly string[] CreatableFamilies = { "tap", "flick", "comment", "bpm", "bullet", "bell" };

        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IMcpToolAuthorizationService mcpToolAuthorizationService;
        private readonly IEditorActionScopeManager actionScopeManager;

        [ImportingConstructor]
        public EditorMutationTools(IEditorDocumentManager editorDocumentManager, IMcpToolAuthorizationService mcpToolAuthorizationService, IEditorActionScopeManager actionScopeManager)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.mcpToolAuthorizationService = mcpToolAuthorizationService;
            this.actionScopeManager = actionScopeManager;
        }

        [McpServerTool(Name = "editor.scroll_to", Title = "Scroll Editor To Time", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Scroll an editor viewport to a chart position. In design mode this also moves the playback position; in preview mode it only moves the preview scroll.")]
        public async Task<object> ScrollTo(
            [Description("TGrid unit (the first component of T[unit,grid]).")] float tGridUnit = 0,
            [Description("TGrid grid (the second component of T[unit,grid]).")] int tGridGrid = 0,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.scroll_to";
            McpOperationLogHelper.LogRequest(operationName, new { tGridUnit, tGridGrid, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Scroll editor to T[{tGridUnit},{tGridGrid}].", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var target = new TGrid(tGridUnit, tGridGrid);
            var result = await RuntimeUiDispatcher.RunAsync(() =>
            {
                editor.ScrollTo(target);
                return new
                {
                    success = true,
                    editorId = resolvedEditorId,
                    target = target.ToString(),
                    viewport = editor.GetViewportTGrid()?.ToString(),
                    isPreviewMode = editor.IsPreviewMode,
                };
            }, cancellationToken);

            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }

        [McpServerTool(Name = "editor.begin_action", Title = "Begin Editor Action Scope", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Open an undo/redo combine scope on an editor. Mutations issued until editor.end_action are queued and only applied when end_action runs; end_action reports their real outcome.")]
        public async Task<object> BeginAction(string editorId = default, string expectedEditorId = default, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.begin_action";
            McpOperationLogHelper.LogRequest(operationName, new { editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, "Open an editor action scope.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var identityKey = McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId);
            if (!actionScopeManager.TryBegin(resolvedEditorId, identityKey, out var errorCode, out var errorMessage))
                return Failure(operationName, errorCode, errorMessage);

            try
            {
                await RuntimeUiDispatcher.RunAsync(() =>
                {
                    editor.UndoRedoManager.BeginCombineAction();
                    return true;
                }, cancellationToken);
            }
            catch
            {
                actionScopeManager.TryTake(resolvedEditorId, identityKey, out _, out _, out _);
                throw;
            }

            var result = new { success = true, editorId = resolvedEditorId, scope = "open", identityKey };
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }

        [McpServerTool(Name = "editor.end_action", Title = "End Editor Action Scope", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Close the action scope opened by editor.begin_action and apply the queued mutations as one undo entry. Fails and rolls back the whole batch when any queued operation failed. Set discard=true to drop the queued mutations without applying them.")]
        public async Task<object> EndAction(string name = default, string editorId = default, bool discard = false, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.end_action";
            McpOperationLogHelper.LogRequest(operationName, new { name, editorId, discard, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Close the editor action scope as '{name}'.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, default, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var identityKey = McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId);
            if (!actionScopeManager.TryTake(resolvedEditorId, identityKey, out var scope, out var errorCode, out var errorMessage))
                return Failure(operationName, errorCode, errorMessage);

            var transactionName = string.IsNullOrWhiteSpace(name) ? "Editor action scope" : name.Trim();
            var summary = await RuntimeUiDispatcher.RunAsync(() =>
            {
                var composite = editor.UndoRedoManager.EndCombineAction(transactionName);
                var result = new EditorActionScopeSummary
                {
                    EditorId = resolvedEditorId,
                    TransactionName = transactionName,
                    OutcomeCount = scope.Outcomes.Count,
                    Outcomes = scope.Outcomes,
                };

                if (discard)
                {
                    result.Applied = false;
                    result.ErrorCode = default;
                    return result;
                }

                if (IsEmptyCompositeAction(composite))
                {
                    result.Applied = false;
                    return result;
                }

                try
                {
                    editor.UndoRedoManager.ExecuteAction(composite);
                    result.Applied = true;

                    var failed = scope.Outcomes.Where(x => x.Executed && !x.Success).ToArray();
                    if (failed.Length > 0)
                    {
                        result.FailedCount = failed.Length;
                        result.ErrorCode = "ACTION_FAILED";
                        result.ErrorMessage = string.Join("; ", failed.Select(x => $"{x.Operation}#{x.ObjectId}: {x.ErrorMessage}"));

                        // 整批回滚后净效果等于“没有应用”，不要对外声称 applied=true。
                        result.Applied = false;
                        TryRollback(composite, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Applied = false;
                    result.ErrorCode = "ACTION_FAILED";
                    result.ErrorMessage = ex.Message;
                    TryRollback(composite, result);
                }

                return result;
            }, cancellationToken);

            var response = new
            {
                success = string.IsNullOrEmpty(summary.ErrorCode),
                editorId = summary.EditorId,
                transactionName = summary.TransactionName,
                applied = summary.Applied,
                rolledBack = summary.RolledBack,
                outcomeCount = summary.OutcomeCount,
                failedCount = summary.FailedCount,
                errorCode = summary.ErrorCode,
                errorMessage = summary.ErrorMessage,
                outcomes = summary.Outcomes.Select(x => new
                {
                    operation = x.Operation,
                    objectType = x.ObjectType,
                    objectId = x.ObjectId,
                    objectStrId = x.ObjectStrId,
                    executed = x.Executed,
                    success = x.Success,
                    errorMessage = x.ErrorMessage,
                }).ToArray(),
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        [McpServerTool(Name = "editor.add_object", Title = "Add Object", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Add a chart object and return its runtime object id. Supported objectType values: tap, flick, comment, bpm, bullet, bell. Bullets require bulletPalleteStrId; bells may omit it (or pass \"--\") for the Ongeki default bell. Inside an action scope the object is queued until editor.end_action applies it.")]
        public async Task<object> AddObject(
            [Description("Object family: tap, flick, comment, bpm, bullet or bell.")] string objectType,
            float tGridUnit = 0,
            int tGridGrid = 0,
            float xGridUnit = 0,
            int xGridGrid = 0,
            [Description("Applies to tap and flick.")] bool? isCritical = default,
            [Description("Flick direction: left or right.")] string direction = default,
            [Description("Comment text.")] string content = default,
            [Description("BPM value for the bpm family.")] double? bpm = default,
            [Description("Bullet pallete StrID (see editor.query_bullet_pallete). Required for bullet; optional for bell, where \"--\" means the Ongeki default bell.")] string bulletPalleteStrId = default,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.add_object";
            var family = NormalizeFamily(objectType);
            McpOperationLogHelper.LogRequest(operationName, new { family, tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPalleteStrId, editorId, expectedEditorId, requestedBy, clientId });

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

            OngekiObjectBase obj;
            try
            {
                obj = CreateObject(family, new TGrid(tGridUnit, tGridGrid), new XGrid(xGridUnit, xGridGrid), isCritical, direction, content, bpm, pallete);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

            var outcome = new EditorActionOutcome { Operation = "add_object", ObjectType = family, ObjectId = obj.Id };
            var fumen = editor.Fumen;
            var action = LambdaUndoAction.Create(
                $"Add {family} #{obj.Id}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        fumen.AddObject(obj);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.RemoveObject(obj));
                    }
                },
                () => TrySilently(() => fumen.RemoveObject(obj)));

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
            var action = LambdaUndoAction.Create(
                $"Remove {family} #{objectId}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        fumen.RemoveObject(obj);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => fumen.AddObject(obj));
                    }
                },
                () => TrySilently(() => fumen.AddObject(obj)));

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

        [McpServerTool(Name = "editor.modify_object", Title = "Modify Object", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Modify one property of a chart object addressed by its runtime object id. Supported properties: tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm, bulletPallete (bullet/bell only, value is a bullet pallete StrID; \"\" or \"--\" clears it for bells). Inside an action scope the change is queued until editor.end_action applies it.")]
        public async Task<object> ModifyObject(
            int objectId,
            [Description("tGridUnit, tGridGrid, xGridUnit, xGridGrid, isCritical, direction, content, bpm or bulletPallete.")] string propertyName,
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

        [McpServerTool(Name = "editor.query_object", Title = "Query Objects", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Query chart objects of one family inside a TGrid range, ordered by TGrid then id. Returns runtime object ids usable with editor.modify_object/editor.remove_object. Pass nextCursor back as cursor to page; totals are reported in the editor's internal TGrid scale (see tGrid.totalGrid).")]
        public async Task<object> QueryObject(
            [Description("Object family: tap, flick, hold, bell, bullet, comment, bpm, meter, clickse, enemy, lane or soflan.")] string objectType,
            [Description("Inclusive lower bound in the TGrid totalGrid scale; omit for no bound.")] int? minTotalGrid = default,
            [Description("Inclusive upper bound in the TGrid totalGrid scale; omit for no bound.")] int? maxTotalGrid = default,
            int limit = 200,
            [Description("Opaque cursor returned by a previous call.")] string cursor = default,
            string editorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.query_object";
            var family = NormalizeFamily(objectType);
            McpOperationLogHelper.LogRequest(operationName, new { family, minTotalGrid, maxTotalGrid, limit, cursor, editorId, requestedBy, clientId });

            if (!QuerableFamilies.Contains(family))
                return Failure(operationName, "UNSUPPORTED_OBJECT_TYPE", $"editor.query_object supports {string.Join(", ", QuerableFamilies)}; '{objectType}' is not supported.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Query {family} objects.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, default, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (!TryParseCursor(cursor, out var cursorTotalGrid, out var cursorObjectId))
                return Failure(operationName, "INVALID_CURSOR", "The cursor is not a value returned by a previous editor.query_object call.");

            var take = Math.Clamp(limit, 1, 2000);
            var min = minTotalGrid is { } minValue ? TGrid.FromTotalGrid(minValue) : TGrid.MinValue;
            var max = maxTotalGrid is { } maxValue ? TGrid.FromTotalGrid(maxValue) : TGrid.MaxValue;

            var page = await RuntimeUiDispatcher.RunAsync(() =>
            {
                var objects = new List<object>(take);
                var truncated = false;
                var seenCursor = cursorTotalGrid is null;
                var lastTotalGrid = 0;
                var lastObjectId = 0;

                foreach (var obj in EnumerateFamily(editor.Fumen, family, min, max))
                {
                    if (!seenCursor)
                    {
                        if (obj is ITimelineObject cursorTimeline && cursorTimeline.TGrid.TotalGrid == cursorTotalGrid && obj.Id == cursorObjectId)
                            seenCursor = true;
                        continue;
                    }

                    if (objects.Count == take)
                    {
                        truncated = true;
                        break;
                    }

                    lastTotalGrid = (obj as ITimelineObject)?.TGrid.TotalGrid ?? 0;
                    lastObjectId = obj.Id;
                    objects.Add(ToObjectDto(family, obj));
                }

                return new { objects, truncated, lastTotalGrid, lastObjectId };
            }, cancellationToken);

            var nextCursor = page.truncated && page.objects.Count > 0 ? $"{page.lastTotalGrid}:{page.lastObjectId}" : default;

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                objectType = family,
                count = page.objects.Count,
                truncated = page.truncated,
                nextCursor,
                objects = page.objects,
            };
            McpOperationLogHelper.LogResult(operationName, new { success = true, family, count = response.count, truncated = response.truncated, nextCursor });
            return response;
        }

        private static readonly string[] SupportedModifyProperties = { "tGridUnit", "tGridGrid", "xGridUnit", "xGridGrid", "isCritical", "direction", "content", "bpm", "bulletPallete" };

        // ---------------- bullet pallete (BPL) tools ----------------

        private const string BulletPalleteObjectType = "bullet_pallete";

        /// <summary>
        /// editor.modify_bullet_pallete 支持的属性，与 BulletPallete 的可写字段一致。
        /// 不含 StrID（改 id 会牵动所有引用，并触发 BulletPalleteList 的同 id 静默替换）与派生的 IsEnableSoflan。
        /// </summary>
        private static readonly string[] SupportedPalleteProperties = { "editorName", "shooter", "target", "size", "type", "speed", "placeOffset", "randomOffsetRange" };

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

        [McpServerTool(Name = "editor.query_bullet_pallete", Title = "Query Bullet Pallete", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Query bullet palletes. Pass strId for one pallete's full detail (including the objects referencing it); otherwise page through all palletes ordered by id, optionally filtered by editorNameContains.")]
        public async Task<object> QueryBulletPallete(
            [Description("Return the full detail of this one pallete.")] string strId = default,
            [Description("Case-insensitive substring filter on the display name (list mode only).")] string editorNameContains = default,
            int limit = 200,
            [Description("Opaque cursor returned by a previous call (list mode only).")] string cursor = default,
            string editorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.query_bullet_pallete";
            McpOperationLogHelper.LogRequest(operationName, new { strId, editorNameContains, limit, cursor, editorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, strId is null ? "Query bullet palletes." : $"Query bullet pallete '{strId}'.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, default, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var fumen = editor.Fumen;
            if (!string.IsNullOrWhiteSpace(strId))
            {
                var single = fumen.BulletPalleteList[strId.Trim()];
                if (single is null)
                    return Failure(operationName, "PALLETE_NOT_FOUND", $"No bullet pallete '{strId}' in editor '{resolvedEditorId}'.");

                var singleResponse = new
                {
                    success = true,
                    editorId = resolvedEditorId,
                    pallete = BuildBulletPalleteDetail(fumen, single, includeReferencingObjects: true),
                };
                McpOperationLogHelper.LogResult(operationName, new { success = true, strId = single.StrID });
                return singleResponse;
            }

            var take = Math.Clamp(limit, 1, 2000);
            var afterId = -1;
            if (!string.IsNullOrWhiteSpace(cursor)
                && (!int.TryParse(cursor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out afterId) || afterId < 0))
                return Failure(operationName, "INVALID_CURSOR", "The cursor is not a value returned by a previous editor.query_bullet_pallete call.");

            var filter = string.IsNullOrWhiteSpace(editorNameContains) ? default : editorNameContains.Trim();
            var referenceCounts = CountPalleteReferences(fumen);
            var details = new List<object>();
            var truncated = false;
            var lastId = afterId;
            foreach (var pallete in fumen.BulletPalleteList)
            {
                var id = BulletPalleteList.ConvertIdToInt(pallete.StrID);
                if (id <= afterId)
                    continue;
                if (filter is not null && (pallete.EditorName ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (details.Count == take)
                {
                    truncated = true;
                    break;
                }

                details.Add(BuildBulletPalleteDetail(fumen, pallete, includeReferencingObjects: false, referenceCounts.GetValueOrDefault(pallete.StrID)));
                lastId = id;
            }

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                count = details.Count,
                truncated,
                nextCursor = truncated ? lastId.ToString(CultureInfo.InvariantCulture) : default,
                palletes = details,
            };
            McpOperationLogHelper.LogResult(operationName, new { success = true, count = details.Count, truncated });
            return response;
        }

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
            var pallete = fumen.BulletPalleteList[strId?.Trim()];
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

        // ---------------- undo / redo ----------------

        [McpServerTool(Name = "editor.undo", Title = "Undo", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Undo the newest entry of the editor's action history (one entry per action; an action scope applied via editor.end_action counts as a single entry). Returns the name of the undone entry plus the updated undo/redo counts.")]
        public async Task<object> Undo(string editorId = default, string expectedEditorId = default, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.undo";
            McpOperationLogHelper.LogRequest(operationName, new { editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, "Undo the last editor action.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var manager = editor.UndoRedoManager;
            string undoneName = default;
            var result = await RuntimeUiDispatcher.RunAsync(() =>
            {
                if (!manager.CanUndo)
                    return default;
                undoneName = manager.CurrentAction?.Name;
                manager.Undo(1);
                return new { undoCount = manager.UndoActionCount, redoCount = manager.RedoActionCount, topName = manager.CurrentAction?.Name };
            }, cancellationToken);

            if (result is null)
                return Failure(operationName, "NOTHING_TO_UNDO", $"Editor '{resolvedEditorId}' has no undoable action.");

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                undone = undoneName,
                undoCount = result.undoCount,
                redoCount = result.redoCount,
                nextUndoName = result.topName,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        [McpServerTool(Name = "editor.redo", Title = "Redo", ReadOnly = false, Destructive = true, OpenWorld = false)]
        [Description("Redo the most recently undone action of the editor. Returns the name of the redone entry plus the updated undo/redo counts.")]
        public async Task<object> Redo(string editorId = default, string expectedEditorId = default, bool requireConfirmation = true, string requestedBy = default, string clientId = default, CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.redo";
            McpOperationLogHelper.LogRequest(operationName, new { editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, "Redo the last undone editor action.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var manager = editor.UndoRedoManager;
            string redoneName = default;
            var result = await RuntimeUiDispatcher.RunAsync(() =>
            {
                if (!manager.CanRedo)
                    return default;
                var index = manager.UndoActionCount;
                redoneName = index < manager.ActionStack.Count ? manager.ActionStack[index]?.Name : default;
                manager.Redo(1);
                return new { undoCount = manager.UndoActionCount, redoCount = manager.RedoActionCount, topName = manager.CurrentAction?.Name };
            }, cancellationToken);

            if (result is null)
                return Failure(operationName, "NOTHING_TO_REDO", $"Editor '{resolvedEditorId}' has nothing to redo.");

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                redone = redoneName,
                undoCount = result.undoCount,
                redoCount = result.redoCount,
                nextUndoName = result.topName,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        private static object BuildBulletPalleteDetail(OngekiFumen fumen, BulletPallete pallete, bool includeReferencingObjects, int? referenceCount = default)
        {
            var referencing = includeReferencingObjects
                ? EnumeratePalleteReferences(fumen).Where(x => PalleteMatches(x.ReferenceBulletPallete, pallete)).ToArray()
                : default;

            return new
            {
                strId = pallete.StrID,
                runtimeId = BulletPalleteList.ConvertIdToInt(pallete.StrID),
                editorName = pallete.EditorName,
                shooter = pallete.ShooterValue.ToString(),
                target = pallete.TargetValue.ToString(),
                size = pallete.SizeValue.ToString(),
                type = pallete.TypeValue.ToString(),
                speed = pallete.Speed,
                placeOffset = pallete.PlaceOffset,
                randomOffsetRange = pallete.RandomOffsetRange,
                isEnableSoflan = pallete.IsEnableSoflan,
                referenceCount = referenceCount ?? referencing?.Length ?? 0,
                referencingObjectIds = referencing?.Select(x => (x as OngekiObjectBase)?.Id ?? 0).Take(50).ToArray(),
            };
        }

        private static IEnumerable<IBulletPalleteReferencable> EnumeratePalleteReferences(OngekiFumen fumen)
            => fumen.Bells.OfType<IBulletPalleteReferencable>().Concat(fumen.Bullets);

        private static bool PalleteMatches(BulletPallete candidate, BulletPallete pallete)
            => candidate is not null && string.Equals(candidate.StrID, pallete.StrID, StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, int> CountPalleteReferences(OngekiFumen fumen)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in EnumeratePalleteReferences(fumen))
            {
                var id = reference.ReferenceBulletPallete?.StrID;
                if (string.IsNullOrEmpty(id))
                    continue;
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
            return counts;
        }

        private static bool IsValidPalleteStrId(string strId)
            => strId.Length > 0 && strId.All(char.IsAsciiLetterOrDigit);

        private static IBulletPalleteReferencable RequirePalleteReferencable(OngekiObjectBase obj)
            => obj as IBulletPalleteReferencable ?? throw new ArgumentException($"Object #{obj.Id} ({obj.GetType().Name}) does not reference a bullet pallete.");

        private static TEnum ParsePalleteEnum<TEnum>(string raw, string propertyName) where TEnum : struct, Enum
        {
            if (Enum.TryParse<TEnum>(raw?.Trim(), true, out var value) && Enum.IsDefined(value))
                return value;
            throw new ArgumentException($"'{raw}' is not a valid {propertyName}; expected one of {string.Join(", ", Enum.GetNames<TEnum>())}.");
        }

        private static string ReadPalleteProperty(BulletPallete pallete, string propertyName)
        {
            switch (propertyName)
            {
                case "editorName":
                    return pallete.EditorName ?? string.Empty;
                case "shooter":
                    return pallete.ShooterValue.ToString();
                case "target":
                    return pallete.TargetValue.ToString();
                case "size":
                    return pallete.SizeValue.ToString();
                case "type":
                    return pallete.TypeValue.ToString();
                case "speed":
                    return pallete.Speed.ToString(CultureInfo.InvariantCulture);
                case "placeOffset":
                    return pallete.PlaceOffset.ToString(CultureInfo.InvariantCulture);
                case "randomOffsetRange":
                    return pallete.RandomOffsetRange.ToString(CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException($"Unsupported bullet pallete property '{propertyName}'.");
            }
        }

        private static void WritePalleteProperty(BulletPallete pallete, string propertyName, string rawValue)
        {
            switch (propertyName)
            {
                case "editorName":
                    pallete.EditorName = rawValue ?? string.Empty;
                    return;
                case "shooter":
                    pallete.ShooterValue = ParsePalleteEnum<Shooter>(rawValue, propertyName);
                    return;
                case "target":
                    pallete.TargetValue = ParsePalleteEnum<Target>(rawValue, propertyName);
                    return;
                case "size":
                    pallete.SizeValue = ParsePalleteEnum<BulletSize>(rawValue, propertyName);
                    return;
                case "type":
                    pallete.TypeValue = ParsePalleteEnum<BulletType>(rawValue, propertyName);
                    return;
                case "speed":
                    pallete.Speed = ParseFloat(rawValue);
                    return;
                case "placeOffset":
                    pallete.PlaceOffset = ParseInt(rawValue);
                    return;
                case "randomOffsetRange":
                    pallete.RandomOffsetRange = ParseInt(rawValue);
                    return;
                default:
                    throw new ArgumentException($"Unsupported bullet pallete property '{propertyName}'.");
            }
        }

        private async Task<object> TryAuthorizeAsync(string operationName, string requestedBy, string clientId, string preview, bool allowInteractivePrompt, CancellationToken cancellationToken)
        {
            var authorizationResult = await mcpToolAuthorizationService.EnsureAuthorizedAsync(operationName, requestedBy, clientId, preview, allowInteractivePrompt, cancellationToken);
            if (authorizationResult.IsAuthorized)
                return null;

            var deniedResult = new
            {
                success = false,
                errorCode = authorizationResult.ErrorCode,
                errorMessage = authorizationResult.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, deniedResult);
            return deniedResult;
        }

        private bool TryResolveEditor(string editorId, string expectedEditorId, out FumenVisualEditorViewModel editor, out string resolvedEditorId, out object errorResult)
        {
            editor = string.IsNullOrWhiteSpace(editorId)
                ? editorDocumentManager.CurrentActivatedEditor
                : (editorDocumentManager.TryGetEditorById(editorId, out var found) ? found : null);
            resolvedEditorId = RuntimeAutomationEditorId.Generate(editor);

            if (editor is null)
            {
                errorResult = new
                {
                    success = false,
                    errorCode = string.IsNullOrWhiteSpace(editorId) ? "NO_ACTIVE_EDITOR" : "EDITOR_NOT_FOUND",
                    errorMessage = string.IsNullOrWhiteSpace(editorId) ? "No active editor is available." : $"Editor '{editorId}' was not found.",
                };
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedEditorId) && !string.Equals(expectedEditorId, resolvedEditorId, StringComparison.Ordinal))
            {
                errorResult = new
                {
                    success = false,
                    errorCode = "EDITOR_CHANGED",
                    errorMessage = $"Expected editor '{expectedEditorId}' but resolved '{resolvedEditorId}'.",
                };
                return false;
            }

            errorResult = null;
            return true;
        }

        private static object Failure(string operationName, string errorCode, string errorMessage)
        {
            var result = new { success = false, errorCode, errorMessage };
            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }

        private static string NormalizeFamily(string objectType) => objectType?.Trim().ToLowerInvariant() ?? string.Empty;

        private static string NormalizePropertyName(string propertyName) => propertyName?.Trim() ?? string.Empty;

        private static bool IsEmptyCompositeAction(IUndoableAction action)
            => action is CompositeUndoAction composite && !composite.CombinedActions.Any();

        private static void TryRollback(IUndoableAction composite, EditorActionScopeSummary summary)
        {
            try
            {
                composite.Undo();
                summary.RolledBack = true;
            }
            catch (Exception ex)
            {
                summary.ErrorMessage = $"{summary.ErrorMessage} (rollback failed: {ex.Message})";
            }
        }

        private static void TrySilently(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // 单条动作的补偿失败不再向上冒泡：结果由 EditorActionOutcome 汇报，end_action 汇总时统一回滚。
            }
        }

        private static bool TryParseCursor(string cursor, out int? totalGrid, out int objectId)
        {
            totalGrid = default;
            objectId = default;

            if (string.IsNullOrWhiteSpace(cursor))
                return true;

            var parts = cursor.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotalGrid)
                || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedObjectId))
                return false;

            totalGrid = parsedTotalGrid;
            objectId = parsedObjectId;
            return true;
        }

        private static OngekiObjectBase CreateObject(string family, TGrid tGrid, XGrid xGrid, bool? isCritical, string direction, string content, double? bpm, BulletPallete pallete = default)
        {
            switch (family)
            {
                case "tap":
                    return new Tap
                    {
                        TGrid = tGrid,
                        XGrid = xGrid,
                        IsCritical = isCritical ?? false,
                    };

                case "flick":
                    return new Flick
                    {
                        TGrid = tGrid,
                        XGrid = xGrid,
                        Direction = ParseDirection(direction),
                        IsCritical = isCritical ?? false,
                    };

                case "comment":
                    return new Comment
                    {
                        TGrid = tGrid,
                        Content = content ?? string.Empty,
                    };

                case "bpm":
                    if (bpm is not { } bpmValue || bpmValue <= 0)
                        throw new ArgumentException("The bpm family requires a positive 'bpm' value.");

                    return new BPMChange
                    {
                        TGrid = tGrid,
                        BPM = bpmValue,
                    };

                case "bullet":
                    return new Bullet
                    {
                        TGrid = tGrid,
                        XGrid = xGrid,
                        ReferenceBulletPallete = pallete,
                    };

                case "bell":
                    return new Bell
                    {
                        TGrid = tGrid,
                        XGrid = xGrid,
                        ReferenceBulletPallete = pallete,
                    };

                default:
                    throw new ArgumentException($"Unsupported object family '{family}'.");
            }
        }

        private static Flick.FlickDirection ParseDirection(string direction)
        {
            return (direction ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "left" => Flick.FlickDirection.Left,
                "right" => Flick.FlickDirection.Right,
                _ => throw new ArgumentException("The flick family requires direction 'left' or 'right'."),
            };
        }

        private static string ReadProperty(OngekiObjectBase obj, string propertyName)
        {
            switch (propertyName)
            {
                case "tGridUnit":
                    return RequireTimeline(obj).TGrid.Unit.ToString(CultureInfo.InvariantCulture);
                case "tGridGrid":
                    return RequireTimeline(obj).TGrid.Grid.ToString(CultureInfo.InvariantCulture);
                case "xGridUnit":
                    return RequireMovable(obj).XGrid.Unit.ToString(CultureInfo.InvariantCulture);
                case "xGridGrid":
                    return RequireMovable(obj).XGrid.Grid.ToString(CultureInfo.InvariantCulture);
                case "isCritical":
                    return RequireCritical(obj).IsCritical.ToString(CultureInfo.InvariantCulture);
                case "direction":
                    return RequireFlick(obj).Direction.ToString();
                case "content":
                    return RequireComment(obj).Content ?? string.Empty;
                case "bpm":
                    return RequireBpm(obj).BPM.ToString(CultureInfo.InvariantCulture);
                case "bulletPallete":
                    return RequirePalleteReferencable(obj).ReferenceBulletPallete?.StrID ?? string.Empty;
                default:
                    throw new ArgumentException($"Unsupported property '{propertyName}'.");
            }
        }

        private static void WriteProperty(OngekiObjectBase obj, string propertyName, string rawValue, OngekiFumen fumen)
        {
            switch (propertyName)
            {
                case "tGridUnit":
                {
                    var timeline = RequireTimeline(obj);
                    timeline.TGrid = new TGrid(ParseFloat(rawValue), timeline.TGrid.Grid);
                    return;
                }
                case "tGridGrid":
                {
                    var timeline = RequireTimeline(obj);
                    timeline.TGrid = new TGrid(timeline.TGrid.Unit, ParseInt(rawValue));
                    return;
                }
                case "xGridUnit":
                {
                    var movable = RequireMovable(obj);
                    movable.XGrid = new XGrid(ParseFloat(rawValue), movable.XGrid.Grid);
                    return;
                }
                case "xGridGrid":
                {
                    var movable = RequireMovable(obj);
                    movable.XGrid = new XGrid(movable.XGrid.Unit, ParseInt(rawValue));
                    return;
                }
                case "isCritical":
                    RequireCritical(obj).IsCritical = ParseBool(rawValue);
                    return;
                case "direction":
                    RequireFlick(obj).Direction = ParseDirection(rawValue);
                    return;
                case "content":
                    RequireComment(obj).Content = rawValue ?? string.Empty;
                    return;
                case "bpm":
                    RequireBpm(obj).BPM = ParseDouble(rawValue);
                    return;
                case "bulletPallete":
                {
                    var referencable = RequirePalleteReferencable(obj);
                    var requested = rawValue?.Trim();
                    if (string.IsNullOrEmpty(requested) || requested == Bell.OngekiDefaultBellPaletteName)
                    {
                        if (obj is not Bell)
                            throw new ArgumentException("A bullet must reference a bullet pallete; only bells can fall back to the Ongeki default bell.");
                        referencable.ReferenceBulletPallete = default;
                        return;
                    }

                    referencable.ReferenceBulletPallete = fumen.BulletPalleteList[requested]
                        ?? throw new ArgumentException($"No bullet pallete '{requested}' in the editor.");
                    return;
                }
                default:
                    throw new ArgumentException($"Unsupported property '{propertyName}'.");
            }
        }

        private static float ParseFloat(string rawValue)
            => float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid number.");

        private static double ParseDouble(string rawValue)
            => double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid number.");

        private static int ParseInt(string rawValue)
            => int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid integer.");

        private static bool ParseBool(string rawValue)
            => bool.TryParse(rawValue, out var value)
                ? value
                : throw new ArgumentException($"'{rawValue}' is not a valid boolean.");

        private static OngekiTimelineObjectBase RequireTimeline(OngekiObjectBase obj)
            => obj as OngekiTimelineObjectBase ?? throw new ArgumentException($"Object #{obj.Id} has no TGrid.");

        private static OngekiMovableObjectBase RequireMovable(OngekiObjectBase obj)
            => obj as OngekiMovableObjectBase ?? throw new ArgumentException($"Object #{obj.Id} has no XGrid.");

        private static ICriticalableObject RequireCritical(OngekiObjectBase obj)
            => obj as ICriticalableObject ?? throw new ArgumentException($"Object #{obj.Id} has no isCritical property.");

        private static Flick RequireFlick(OngekiObjectBase obj)
            => obj as Flick ?? throw new ArgumentException($"Object #{obj.Id} is not a flick.");

        private static Comment RequireComment(OngekiObjectBase obj)
            => obj as Comment ?? throw new ArgumentException($"Object #{obj.Id} is not a comment.");

        private static BPMChange RequireBpm(OngekiObjectBase obj)
            => obj as BPMChange ?? throw new ArgumentException($"Object #{obj.Id} is not a bpm change.");

        private static bool TryFindObject(OngekiFumen fumen, int objectId, out string family, out OngekiObjectBase obj)
        {
            foreach (var name in QuerableFamilies)
            {
                var hit = EnumerateFamily(fumen, name, TGrid.MinValue, TGrid.MaxValue)?.FirstOrDefault(x => x.Id == objectId);
                if (hit is not null)
                {
                    family = name;
                    obj = hit;
                    return true;
                }
            }

            family = default;
            obj = default;
            return false;
        }

        private static IEnumerable<OngekiObjectBase> EnumerateFamily(OngekiFumen fumen, string family, TGrid min, TGrid max)
        {
            switch (family)
            {
                case "tap":
                    return RangeOf(fumen.Taps, min, max);
                case "flick":
                    return RangeOf(fumen.Flicks, min, max);
                case "bell":
                    return RangeOf(fumen.Bells, min, max);
                case "bullet":
                    return RangeOf(fumen.Bullets, min, max);
                case "comment":
                    return RangeOf(fumen.Comments, min, max);
                case "clickse":
                    return RangeOf(fumen.ClickSEs, min, max);
                case "enemy":
                    return RangeOf(fumen.EnemySets, min, max);
                case "bpm":
                    return RangeOf(fumen.BpmList, min, max);
                case "meter":
                    return RangeOf(fumen.MeterChanges, min, max);
                case "hold":
                    return fumen.Holds.Where(x => InRange(x, min, max));
                case "lane":
                    return fumen.Lanes.Where(x => InRange(x, min, max));
                case "soflan":
                    return fumen.SoflansMap.Values.SelectMany(x => x).OfType<OngekiObjectBase>().Where(x => InRange(x, min, max));
                default:
                    return default;
            }
        }

        private static IEnumerable<OngekiObjectBase> RangeOf<T>(IBinaryFindRangeEnumable<T, TGrid> list, TGrid min, TGrid max)
            where T : OngekiObjectBase, ITimelineObject
            => list.BinaryFindRange(min, max).Cast<OngekiObjectBase>();

        private static bool InRange(OngekiObjectBase obj, TGrid min, TGrid max)
            => obj is ITimelineObject timeline && min <= timeline.TGrid && timeline.TGrid <= max;

        private static object ToObjectDto(string family, OngekiObjectBase obj)
        {
            var tGrid = (obj as ITimelineObject)?.TGrid;
            var xGrid = (obj as OngekiMovableObjectBase)?.XGrid;

            return new
            {
                id = obj.Id,
                type = family,
                tGrid = tGrid is null ? null : new { unit = tGrid.Unit, grid = tGrid.Grid, totalGrid = tGrid.TotalGrid },
                xGrid = xGrid is null ? null : new { unit = xGrid.Unit, grid = xGrid.Grid, totalGrid = xGrid.TotalGrid },
                isCritical = obj is ICriticalableObject criticalable ? criticalable.IsCritical : (bool?)null,
                bulletPalleteStrId = obj is IBulletPalleteReferencable referencable ? referencable.ReferenceBulletPallete?.StrID : default,
            };
        }
    }
}
