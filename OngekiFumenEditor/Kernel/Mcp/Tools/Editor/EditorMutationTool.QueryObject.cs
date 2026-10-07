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
        [McpServerTool(Name = "editor.query_object", Title = "Query Objects", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Query chart objects of one family — or several via objectTypes — inside a TGrid range. Single-family pages are ordered by TGrid (objects sharing a TGrid keep the chart's internal order, which is stable across calls but not sorted by id); multi-family pages merge every requested family and are ordered by TGrid then object id. selectedOnly=true narrows the result to the objects currently selected in the editor; includeAuxiliary=false drops auxiliary display objects (lane curve control points). Returns runtime object ids usable with editor.modify_object/editor.remove_object. Pass nextCursor back as cursor to page; paging assumes the filters and the selection stay unchanged between pages. Totals are reported in the editor's internal TGrid scale (see tGrid.totalGrid).")]
        public async Task<object> QueryObject(
            [Description("Object family (single form): tap, flick, hold, bell, bullet, comment, bpm, meter, clickse, enemy, lane (lane starts), lanenext (lane segments), curvecontrol (lane curve control points), beam, beamnext, isfarea, laneblock or soflan. Provide this or objectTypes; giving both merges them without duplicates.")] string objectType = default,
            [Description("Object families (multi form): any combination of the family names above. Multi-family pages are ordered by TGrid then object id.")] string[] objectTypes = default,
            [Description("Inclusive lower bound in the TGrid totalGrid scale; omit for no bound.")] int? minTotalGrid = default,
            [Description("Inclusive upper bound in the TGrid totalGrid scale; omit for no bound.")] int? maxTotalGrid = default,
            [Description("When true, only objects currently selected in the editor are returned. Default false (no selection filter).")] bool? selectedOnly = default,
            [Description("When false, auxiliary display objects (currently the lane curve control points) are omitted from the result. Default true (they are included).")] bool? includeAuxiliary = default,
            int limit = 200,
            [Description("Opaque cursor returned by a previous call.")] string cursor = default,
            string editorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.query_object";
            var requestedFamilies = new List<string>();
            if (!string.IsNullOrWhiteSpace(objectType))
                requestedFamilies.Add(NormalizeFamily(objectType));
            if (objectTypes is not null)
                requestedFamilies.AddRange(objectTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(NormalizeFamily));
            McpOperationLogHelper.LogRequest(operationName, new { objectType, objectTypes, minTotalGrid, maxTotalGrid, selectedOnly, includeAuxiliary, limit, cursor, editorId, requestedBy, clientId });

            if (requestedFamilies.Count == 0)
                return Failure(operationName, "INVALID_ARGUMENT", "editor.query_object needs objectType or objectTypes: pass at least one family to query.");

            var unknownFamily = requestedFamilies.FirstOrDefault(x => !QuerableFamilies.Contains(x));
            if (unknownFamily is not null)
                return Failure(operationName, "UNSUPPORTED_OBJECT_TYPE", $"editor.query_object supports {string.Join(", ", QuerableFamilies)}; '{unknownFamily}' is not supported.");

            var families = requestedFamilies.Distinct().ToArray();

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Query {string.Join(", ", families)} objects.", true, cancellationToken) is { } denied)
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

                IEnumerable<(string family, OngekiObjectBase obj)> source;
                if (families.Length == 1)
                {
                    var single = families[0];
                    source = EnumerateFamily(editor.Fumen, single, min, max).Select(x => (single, x));
                }
                else
                {
                    // 多族结果跨族合并，必须给出全序：按 (TGrid, Id) 排序（单族保持各族原有顺序，避免改变既有分页契约）。
                    source = families
                        .SelectMany(f => EnumerateFamily(editor.Fumen, f, min, max).Select(x => (family: f, obj: x)))
                        .OrderBy(x => x.obj is ITimelineObject timeline ? timeline.TGrid.TotalGrid : 0)
                        .ThenBy(x => x.obj.Id);
                }

                if (selectedOnly == true)
                    source = source.Where(x => x.obj is ISelectableObject { IsSelected: true });
                if (includeAuxiliary == false)
                    source = source.Where(x => !IsAuxiliaryObject(x.obj));

                foreach (var (itemFamily, obj) in source)
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
                    objects.Add(ToObjectDto(itemFamily, obj));
                }

                return new { objects, truncated, lastTotalGrid, lastObjectId };
            }, cancellationToken);

            var nextCursor = page.truncated && page.objects.Count > 0 ? $"{page.lastTotalGrid}:{page.lastObjectId}" : default;

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                objectType = families.Length == 1 ? families[0] : default,
                objectTypes = families,
                selectedOnly = selectedOnly == true,
                includeAuxiliary = includeAuxiliary != false,
                count = page.objects.Count,
                truncated = page.truncated,
                nextCursor,
                objects = page.objects,
            };
            McpOperationLogHelper.LogResult(operationName, new { success = true, families, count = response.count, truncated = response.truncated, nextCursor });
            return response;
        }
    }
}
