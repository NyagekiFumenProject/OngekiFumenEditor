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
    }
}
