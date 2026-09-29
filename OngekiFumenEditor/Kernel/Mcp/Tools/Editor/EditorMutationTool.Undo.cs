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
    }
}
