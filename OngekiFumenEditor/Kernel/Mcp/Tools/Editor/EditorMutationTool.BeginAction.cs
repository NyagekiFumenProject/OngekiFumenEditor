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
    }
}
