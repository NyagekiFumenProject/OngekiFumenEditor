using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorMutationTool
    {
        [McpServerTool(Name = "editor.get_object", Title = "Get Object", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Read one chart object by its runtime object id (ids come from editor.query_object or the add/modify responses): returns the object's full summary DTO plus every property editor.modify_object can address on it, each with its current canonical value and whether it is writable right now. Writable mirrors the per-property eligibility rules of editor.modify_object (a property that does not apply to the object is omitted; custom projectile parameters are read-only while a pallete is set) — whether a concrete value parses can only be discovered by writing it. Read-only: it does not modify the chart or the undo history.")]
        public async Task<object> GetObject(
            int objectId,
            string editorId = default,
            string expectedEditorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.get_object";
            McpOperationLogHelper.LogRequest(operationName, new { objectId, editorId, expectedEditorId, requestedBy, clientId });

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Read object #{objectId}.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (!TryFindObject(editor.Fumen, objectId, out var family, out var obj))
                return Failure(operationName, "OBJECT_NOT_FOUND", $"No object with id {objectId} was found in editor '{resolvedEditorId}'.");

            var (detail, properties) = await RuntimeUiDispatcher.RunAsync(() =>
            {
                var rows = new List<object>(SupportedModifyProperties.Length);
                foreach (var name in SupportedModifyProperties)
                {
                    string value;
                    try
                    {
                        value = ReadProperty(obj, name);
                    }
                    catch (ArgumentException)
                    {
                        // 与 modify_object 的校验共用同一套读取规则：读不出来的属性 = 该对象没有这个属性。
                        continue;
                    }

                    rows.Add(new
                    {
                        name,
                        value,
                        // 类型级可写性镜像 modify_object 的入口校验；取值能否解析只有真正写入时才知道。
                        writable = !(ProjectileCustomProperties.Contains(name) && obj is IBulletPalleteReferencable { ReferenceBulletPallete: not null }),
                    });
                }

                return (detail: ToObjectDto(family, obj), rows);
            }, cancellationToken);

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                objectType = family,
                objectId = obj.Id,
                @object = detail,
                properties,
            };
            McpOperationLogHelper.LogResult(operationName, new { success = true, objectType = family, objectId = obj.Id, propertyCount = properties.Count });
            return response;
        }
    }
}
