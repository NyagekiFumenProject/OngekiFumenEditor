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
    }
}
