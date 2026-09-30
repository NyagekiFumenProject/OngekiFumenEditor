using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Mcp.Tools.Editor
{
    internal sealed partial class EditorMutationTool
    {
        /// <summary>
        /// 一个可写的谱面元信息字段。三段式是为了让「取值解析」发生在动 undo 栈之前 ——
        /// 非法输入要能在 <c>editor.set_metainfo</c> 的参数校验阶段返回 INVALID_ARGUMENT，
        /// 而不是等到 action 执行时才以「无 errorCode」的形式失败。
        /// <c>Read</c> 与 <c>Parse</c> 都产出**规范字符串**，所以 undo 直接 <c>Write(oldValue)</c> 即可。
        /// </summary>
        private sealed class MetaInfoField
        {
            public MetaInfoField(string name, string valueType, Func<FumenMetaInfo, string> read, Func<string, string> parse, Action<OngekiFumen, FumenMetaInfo, string> write)
            {
                Name = name;
                ValueType = valueType;
                Read = read;
                Parse = parse;
                Write = write;
            }

            public string Name { get; }
            public string ValueType { get; }
            public Func<FumenMetaInfo, string> Read { get; }
            public Func<string, string> Parse { get; }
            public Action<OngekiFumen, FumenMetaInfo, string> Write { get; }
        }

        private static MetaInfoField Text(string name, Func<FumenMetaInfo, string> get, Action<OngekiFumen, FumenMetaInfo, string> set)
            => new(name, "string",
                m => get(m) ?? string.Empty,
                v => v,
                set);

        private static MetaInfoField Number(string name, Func<FumenMetaInfo, double> get, Action<OngekiFumen, FumenMetaInfo, double> set)
            => new(name, "number",
                m => FormatDouble(get(m)),
                v => FormatDouble(ParseDouble(v, name)),
                (f, m, c) => set(f, m, double.Parse(c, CultureInfo.InvariantCulture)));

        private static MetaInfoField Integer(string name, Func<FumenMetaInfo, int> get, Action<OngekiFumen, FumenMetaInfo, int> set)
            => new(name, "integer",
                m => FormatInt(get(m)),
                v => FormatInt(ParseInt(v, name)),
                (f, m, c) => set(f, m, int.Parse(c, CultureInfo.InvariantCulture)));

        private static MetaInfoField Boolean(string name, Func<FumenMetaInfo, bool> get, Action<OngekiFumen, FumenMetaInfo, bool> set)
            => new(name, "boolean",
                m => FormatBool(get(m)),
                v => FormatBool(ParseBool(v, name)),
                (f, m, c) => set(f, m, bool.Parse(c)));

        private static MetaInfoField VersionField(Func<FumenMetaInfo, Version> get, Action<OngekiFumen, FumenMetaInfo, Version> set)
            => new("version", "string",
                m => (get(m) ?? new Version(1, 0, 0)).ToString(),
                v => Version.Parse(v.Trim()).ToString(),
                (f, m, c) => set(f, m, Version.Parse(c)));

        // 注意：静态字段按文本顺序初始化，MetaInfoFieldsByName 依赖 MetaInfoFields，必须排在后面。
        private static readonly MetaInfoField[] MetaInfoFields =
        {
            Text("creator", m => m.Creator, (f, m, v) => m.Creator = v),
            VersionField(m => m.Version, (f, m, v) => m.Version = v),

            // 首个 BPM 同时写进 BpmList.FirstBpm：只改 meta 字段不够，谱面本身的首个 BPM 也要跟着变。
            Number("bpmFirst", m => m.BpmDefinition.First, (f, m, v) => { m.BpmDefinition.First = v; f.BpmList.FirstBpm = v; }),
            Number("bpmCommon", m => m.BpmDefinition.Common, (f, m, v) => m.BpmDefinition.Common = v),
            Number("bpmMinimum", m => m.BpmDefinition.Minimum, (f, m, v) => m.BpmDefinition.Minimum = v),
            Number("bpmMaximum", m => m.BpmDefinition.Maximum, (f, m, v) => m.BpmDefinition.Maximum = v),

            // 同上：MetaInfo.MeterDefinition 是普通自动属性、不发通知，框架里那条同步链实际上是断的，这里显式补上。
            Integer("meterBunshi", m => m.MeterDefinition.Bunshi, (f, m, v) => { m.MeterDefinition.Bunshi = v; f.MeterChanges.FirstMeter.BunShi = v; }),
            Integer("meterBunbo", m => m.MeterDefinition.Bunbo, (f, m, v) => { m.MeterDefinition.Bunbo = v; f.MeterChanges.FirstMeter.Bunbo = v; }),

            Integer("tResolution", m => m.TRESOLUTION, (f, m, v) => m.TRESOLUTION = v),
            Integer("xResolution", m => m.XRESOLUTION, (f, m, v) => m.XRESOLUTION = v),
            Integer("clickDefinition", m => m.ClickDefinition, (f, m, v) => m.ClickDefinition = v),

            Boolean("tutorial", m => m.Tutorial, (f, m, v) => m.Tutorial = v),

            Number("bulletDamage", m => m.BulletDamage, (f, m, v) => m.BulletDamage = v),
            Number("hardBulletDamage", m => m.HardBulletDamage, (f, m, v) => m.HardBulletDamage = v),
            Number("dangerBulletDamage", m => m.DangerBulletDamage, (f, m, v) => m.DangerBulletDamage = v),
            Number("beamDamage", m => m.BeamDamage, (f, m, v) => m.BeamDamage = v),

            Number("progJudgeBpm", m => m.ProgJudgeBpm, (f, m, v) => m.ProgJudgeBpm = (float)v),
        };

        private static readonly Dictionary<string, MetaInfoField> MetaInfoFieldsByName =
            MetaInfoFields.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        [McpServerTool(Name = "editor.set_metainfo", Title = "Set Fumen Meta Info", ReadOnly = false, Destructive = false, OpenWorld = false)]
        [Description("Set one field of the editor's chart meta info (the values shown in the FumenMetaInfoBrowser tool). One undoable editor action, so it participates in undo/redo and in an action scope. Supported fields: creator, version (\"1.0.0\"), bpmFirst/bpmCommon/bpmMinimum/bpmMaximum, meterBunshi/meterBunbo, tResolution, xResolution, clickDefinition, tutorial (true/false), bulletDamage, hardBulletDamage, dangerBulletDamage, beamDamage, progJudgeBpm. bpmFirst and meterBunshi/meterBunbo also update the chart's first BPM / first meter so the change actually takes effect. Inside an action scope the change is queued until editor.end_action applies it.")]
        public async Task<object> SetMetaInfo(
            [Description("Field to change: creator, version, bpmFirst, bpmCommon, bpmMinimum, bpmMaximum, meterBunshi, meterBunbo, tResolution, xResolution, clickDefinition, tutorial, bulletDamage, hardBulletDamage, dangerBulletDamage, beamDamage or progJudgeBpm.")] string metainfoName,
            [Description("New value as text; parsed according to the field's type.")] string newValue,
            string editorId = default,
            string expectedEditorId = default,
            bool requireConfirmation = true,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.set_metainfo";
            var name = NormalizePropertyName(metainfoName);
            McpOperationLogHelper.LogRequest(operationName, new { metainfoName = name, newValue, editorId, expectedEditorId, requestedBy, clientId });

            if (!MetaInfoFieldsByName.TryGetValue(name, out var field))
                return Failure(operationName, "UNSUPPORTED_METAINFO", $"editor.set_metainfo supports {string.Join(", ", MetaInfoFields.Select(x => x.Name))}; '{metainfoName}' is not supported.");

            if (newValue is null)
                return Failure(operationName, "INVALID_ARGUMENT", $"editor.set_metainfo requires newValue for '{field.Name}'.");

            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, $"Set fumen meta info {field.Name}.", requireConfirmation, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            var metaInfo = editor.Fumen?.MetaInfo;
            if (metaInfo is null)
                return Failure(operationName, "NO_METAINFO", $"Editor '{resolvedEditorId}' has no chart meta info to set.");
            if (metaInfo.BpmDefinition is null || metaInfo.MeterDefinition is null)
                return Failure(operationName, "NO_METAINFO", $"Editor '{resolvedEditorId}' has incomplete meta info (BpmDefinition or MeterDefinition is null).");

            string oldValue;
            try
            {
                oldValue = field.Read(metaInfo);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "UNSUPPORTED_METAINFO", $"Cannot read '{field.Name}' from the current meta info: {ex.Message}");
            }

            // 先把输入解析成规范形式：非法输入在这里就变成带 errorCode 的失败，不会先污染 undo 栈。
            string canonicalNewValue;
            try
            {
                canonicalNewValue = field.Parse(newValue);
            }
            catch (Exception ex)
            {
                return Failure(operationName, "INVALID_ARGUMENT", ex.Message);
            }

            var fumen = editor.Fumen;
            var outcome = new EditorActionOutcome { Operation = "set_metainfo", ObjectType = "metainfo" };
            var action = LambdaUndoAction.Create(
                $"Set metainfo {field.Name}",
                () =>
                {
                    outcome.Executed = true;
                    try
                    {
                        field.Write(fumen, metaInfo, canonicalNewValue);
                        outcome.Success = true;
                    }
                    catch (Exception ex)
                    {
                        outcome.Success = false;
                        outcome.ErrorMessage = ex.Message;
                        TrySilently(() => field.Write(fumen, metaInfo, oldValue));
                    }
                },
                () => TrySilently(() => field.Write(fumen, metaInfo, oldValue)));

            await RuntimeUiDispatcher.RunAsync(() =>
            {
                editor.UndoRedoManager.ExecuteAction(action);
                return true;
            }, cancellationToken);

            var queued = actionScopeManager.TryTrack(resolvedEditorId, McpClientAuthorizationManager.BuildClientIdentityKey(requestedBy, clientId), outcome);
            var response = new
            {
                success = queued || outcome.Success,
                editorId = resolvedEditorId,
                metainfoName = field.Name,
                valueType = field.ValueType,
                oldValue,
                newValue = canonicalNewValue,
                applied = !queued,
                queued,
                errorMessage = queued ? default : outcome.ErrorMessage,
            };
            McpOperationLogHelper.LogResult(operationName, response);
            return response;
        }

        // 全部用 InvariantCulture + 往返格式，保证 Read()/Parse() 的输出能被 Write() 原样读回（undo 依赖这一点）。
        private static string FormatDouble(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string FormatInt(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string FormatBool(bool value) => value ? "true" : "false";

        private static double ParseDouble(string raw, string fieldName)
            => double.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"{fieldName} expects a number (got '{raw}').");

        private static int ParseInt(string raw, string fieldName)
            => int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"{fieldName} expects an integer (got '{raw}').");

        private static bool ParseBool(string raw, string fieldName) => raw?.Trim().ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new ArgumentException($"{fieldName} expects true or false (got '{raw}')."),
        };
    }
}
