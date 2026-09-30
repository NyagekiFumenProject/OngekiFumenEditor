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
        [Description("Query chart objects of one family inside a TGrid range, ordered by TGrid then id. Returns runtime object ids usable with editor.modify_object/editor.remove_object. Pass nextCursor back as cursor to page; totals are reported in the editor's internal TGrid scale (see tGrid.totalGrid).")]
        public async Task<object> QueryObject(
            [Description("Object family: tap, flick, hold, bell, bullet, comment, bpm, meter, clickse, enemy, lane (lane starts), lanenext (lane segments), curvecontrol (lane curve control points), beam, beamnext, isfarea, laneblock or soflan.")] string objectType,
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
    }
}
