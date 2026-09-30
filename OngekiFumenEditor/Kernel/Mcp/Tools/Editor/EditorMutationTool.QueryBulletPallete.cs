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
                var single = LookupBulletPallete(fumen, strId.Trim());
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
    }
}
