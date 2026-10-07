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
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
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

        /// <summary>
        /// 按 StrID 精确查一个弹幕调色板。
        /// <para>
        /// 不能直接用 <c>fumen.BulletPalleteList[strId]</c>：它的索引器经
        /// <see cref="BulletPalleteList.ConvertIdToInt"/> 把未知字符一律折算成 0，
        /// 于是 "does-not-exist" 这种垃圾串可能撞上某个真实调色板的数值 id，
        /// 静默返回一个完全不相干的调色板。这里查回来后再核对真身，对不上就当没找到。
        /// </para>
        /// </summary>
        private static BulletPallete LookupBulletPallete(OngekiFumen fumen, string strId)
        {
            try
            {
                var found = fumen.BulletPalleteList[strId];
                return found is not null && string.Equals(found.StrID, strId, StringComparison.OrdinalIgnoreCase) ? found : null;
            }
            catch
            {
                return null;
            }
        }

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

        /// <summary>
        /// §27：写操作后把属性浏览器刷到一致状态（删除的对象从选中集消失、被改动的选中对象显示新值）。
        /// 只在该 editor 是当前激活编辑器时刷新 —— 属性浏览器展示的就是活动编辑器的选中集，
        /// 与 UI 的 <c>RemoveObjects</c> 守卫（<c>if (IsActive)</c>）一致。
        /// </summary>
        private static void RefreshPropertyBrowser(FumenVisualEditorViewModel editor)
        {
            if (editor is null || !editor.IsActive)
                return;

            // 全限定 IoC：本文件不引入 Caliburn.Micro，避免它的 Action 与 System.Action 撞名。
            TrySilently(() => Caliburn.Micro.IoC.Get<IFumenObjectPropertyBrowser>().RefreshSelected(editor));
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
    }
}
