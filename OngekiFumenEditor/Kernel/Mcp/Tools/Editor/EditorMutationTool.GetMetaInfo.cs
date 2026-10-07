using ModelContextProtocol.Server;
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
        [McpServerTool(Name = "editor.get_metainfo", Title = "Get Fumen Meta Info", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Read the editor's chart meta info (the same fields editor.set_metainfo writes and the FumenMetaInfoBrowser tool shows). Pass metainfoName for one field, or omit it to read every supported field in one call. Read-only: it never modifies the chart and never touches the undo history.")]
        public async Task<object> GetMetaInfo(
            [Description("Field to read: creator, version, bpmFirst, bpmCommon, bpmMinimum, bpmMaximum, meterBunshi, meterBunbo, tResolution, xResolution, clickDefinition, tutorial, bulletDamage, hardBulletDamage, dangerBulletDamage, beamDamage or progJudgeBpm. Omit to read every field.")] string metainfoName = default,
            string editorId = default,
            string expectedEditorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.get_metainfo";
            var name = string.IsNullOrWhiteSpace(metainfoName) ? default : NormalizePropertyName(metainfoName);
            McpOperationLogHelper.LogRequest(operationName, new { metainfoName = name, editorId, expectedEditorId, requestedBy, clientId });

            if (name is not null && !MetaInfoFieldsByName.ContainsKey(name))
                return Failure(operationName, "UNSUPPORTED_METAINFO", $"editor.get_metainfo supports {string.Join(", ", MetaInfoFields.Select(x => x.Name))}; '{metainfoName}' is not supported.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, name is null ? "Read all fumen meta info fields." : $"Read fumen meta info {name}.", true, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var metaInfo = editor.Fumen?.MetaInfo;
            if (metaInfo is null)
                return Failure(operationName, "NO_METAINFO", $"Editor '{resolvedEditorId}' has no chart meta info to read.");
            // 与 editor.set_metainfo 同一前置条件：Bpm/Meter 定义缺失时 bpm*/meter* 字段本来就读不出来，统一报 NO_METAINFO。
            if (metaInfo.BpmDefinition is null || metaInfo.MeterDefinition is null)
                return Failure(operationName, "NO_METAINFO", $"Editor '{resolvedEditorId}' has incomplete meta info (BpmDefinition or MeterDefinition is null).");

            object result;
            if (name is not null)
            {
                var field = MetaInfoFieldsByName[name];
                string value;
                try
                {
                    value = field.Read(metaInfo);
                }
                catch (Exception ex)
                {
                    return Failure(operationName, "UNSUPPORTED_METAINFO", $"Cannot read '{field.Name}' from the current meta info: {ex.Message}");
                }

                result = new
                {
                    success = true,
                    editorId = resolvedEditorId,
                    metainfoName = field.Name,
                    valueType = field.ValueType,
                    value,
                };
            }
            else
            {
                var fields = new List<object>(MetaInfoFields.Length);
                foreach (var field in MetaInfoFields)
                {
                    string value;
                    try
                    {
                        value = field.Read(metaInfo);
                    }
                    catch (Exception ex)
                    {
                        return Failure(operationName, "UNSUPPORTED_METAINFO", $"Cannot read '{field.Name}' from the current meta info: {ex.Message}");
                    }

                    fields.Add(new { name = field.Name, valueType = field.ValueType, value });
                }

                result = new
                {
                    success = true,
                    editorId = resolvedEditorId,
                    fields,
                };
            }

            McpOperationLogHelper.LogResult(operationName, result);
            return result;
        }
    }
}
