using Caliburn.Micro;
using ModelContextProtocol.Server;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.RuntimeAutomation;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Modules.FumenCheckerListViewer.Base.DefaultNavigateBehaviorImpl;
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
        private const int DefaultCheckResultLimit = 200;
        private const int MaxCheckResultLimit = 1000;

        /// <summary>
        /// 检查规则只把 context 用于「用户点结果时跳转」，跑规则本身不会用到它。
        /// MCP 侧没有视图，传一个什么都不做的实现即可。
        /// </summary>
        private static readonly IFumenCheckContext NullCheckContext = new NullFumenCheckContext();

        private sealed class NullFumenCheckContext : IFumenCheckContext
        {
            public void ScrollTo(TGrid tGrid) { }
            public void ScrollTo(OngekiTimelineObjectBase ongekiObject) { }
            public void NotifyObjectClicked(OngekiTimelineObjectBase ongekiObject) { }
            public void ShowFumenMetaInfo() { }
        }

        [McpServerTool(Name = "editor.check", Title = "Run Fumen Check", ReadOnly = true, Destructive = false, OpenWorld = false)]
        [Description("Run every built-in fumen check rule (the same rules the FumenCheckerListViewer tool runs) against an editor's chart and return the results. Read-only: it does not modify the chart. Call it after any chart mutation to catch mistakes introduced by your own edits. Results are ordered by severity (error -> problem -> suggest). Each result carries ruleName, severity, description, location plus the structured target (objectType/objectId or tGrid) when the rule points at a specific spot. Use minSeverity to filter and limit to bound the response.")]
        public async Task<object> Check(
            [Description("Lowest severity to return: 'error' (errors only), 'problem' (problems + errors) or 'suggest' (everything). Default 'suggest'.")] string minSeverity = default,
            [Description("Maximum number of results to return (1-1000, default 200). Counts always cover every result, regardless of this limit.")] int? limit = default,
            string editorId = default,
            string expectedEditorId = default,
            string requestedBy = default,
            string clientId = default,
            CancellationToken cancellationToken = default)
        {
            const string operationName = "editor.check";
            McpOperationLogHelper.LogRequest(operationName, new { minSeverity, limit, editorId, expectedEditorId, requestedBy, clientId });

            if (!TryParseMinSeverity(minSeverity, out var severityFloor, out var severityError))
                return Failure(operationName, "INVALID_ARGUMENT", severityError);

            var take = Math.Clamp(limit ?? DefaultCheckResultLimit, 1, MaxCheckResultLimit);

            // 只读操作：不弹交互式授权框。
            if (await TryAuthorizeAsync(operationName, requestedBy, clientId, "Run all built-in fumen check rules and return the results.", false, cancellationToken) is { } denied)
                return denied;

            if (TryResolveEditor(editorId, expectedEditorId, out var editor, out var resolvedEditorId, out var resolveError) is false)
                return resolveError;

            if (editor.Fumen is null)
                return Failure(operationName, "NO_FUMEN", $"Editor '{resolvedEditorId}' has no chart loaded.");

            var fumen = editor.Fumen;

            // 规则会遍历谱面集合，必须在 UI 线程上跑，且单条规则抛异常不能拖垮整次检查。
            var scan = await RuntimeUiDispatcher.RunAsync(() =>
            {
                var rules = IoC.GetAll<IFumenCheckRule>().ToList();
                var hits = new List<ICheckResult>();
                var failures = new List<object>();

                foreach (var rule in rules)
                {
                    try
                    {
                        foreach (var hit in rule.CheckRule(fumen, NullCheckContext))
                        {
                            if (hit is not null)
                                hits.Add(hit);
                        }
                    }
                    catch (Exception ex)
                    {
                        failures.Add(new
                        {
                            rule = rule.GetType().Name,
                            errorCode = "RULE_FAILED",
                            errorMessage = ex.Message,
                        });
                    }
                }

                return new { RuleCount = rules.Count, Hits = hits, Failures = failures };
            }, cancellationToken);

            var all = scan.Hits
                .OrderByDescending(x => x.Severity)
                .ThenBy(x => GetResultTGrid(x)?.TotalGrid ?? int.MaxValue)
                .ThenBy(x => x.RuleName, StringComparer.Ordinal)
                .ToList();

            var errorCount = all.Count(x => x.Severity == RuleSeverity.Error);
            var problemCount = all.Count(x => x.Severity == RuleSeverity.Problem);
            var suggestCount = all.Count(x => x.Severity == RuleSeverity.Suggest);

            var selected = all.Where(x => x.Severity >= severityFloor).ToList();
            var results = selected.Take(take).Select(x => ToCheckResultDto(fumen, x)).ToList();

            var response = new
            {
                success = true,
                editorId = resolvedEditorId,
                ruleCount = scan.RuleCount,
                minSeverity = SeverityName(severityFloor),
                total = all.Count,
                errorCount,
                problemCount,
                suggestCount,
                matched = selected.Count,
                returned = results.Count,
                truncated = selected.Count > results.Count,
                results,
                ruleFailures = scan.Failures,
            };
            McpOperationLogHelper.LogResult(operationName, new { response.success, response.editorId, response.total, response.errorCount, response.problemCount, response.suggestCount, response.returned, response.truncated, failedRules = scan.Failures.Count });
            return response;
        }

        /// <summary>取出该检查结果指向的谱面位置；只有导航行为知道目标在哪。</summary>
        private static TGrid GetResultTGrid(ICheckResult result) => result.NavigateBehavior switch
        {
            NavigateToObjectBehavior behavior => (behavior.OngekiObject as ITimelineObject)?.TGrid,
            NavigateToTGridBehavior behavior => behavior.TargetTGrid,
            _ => null,
        };

        private static object ToCheckResultDto(OngekiFumen fumen, ICheckResult result)
        {
            string family = default;
            int? objectId = default;

            if (result.NavigateBehavior is NavigateToObjectBehavior behavior && behavior.OngekiObject is { } obj)
            {
                objectId = obj.Id;
                if (TryFindObject(fumen, obj.Id, out var resolvedFamily, out _))
                    family = resolvedFamily;
            }

            var tGrid = GetResultTGrid(result);

            return new
            {
                ruleName = result.RuleName,
                severity = SeverityName(result.Severity),
                description = result.Description,
                location = result.LocationDescription,
                objectType = family,
                objectId,
                tGrid = tGrid is null ? null : new { unit = tGrid.Unit, grid = tGrid.Grid, totalGrid = tGrid.TotalGrid },
            };
        }

        private static string SeverityName(RuleSeverity severity) => severity switch
        {
            RuleSeverity.Error => "error",
            RuleSeverity.Problem => "problem",
            RuleSeverity.Suggest => "suggest",
            _ => severity.ToString().ToLowerInvariant(),
        };

        /// <summary>minSeverity 是「最低级别」而非「恰好等于」：problem 会连同 error 一起返回。</summary>
        private static bool TryParseMinSeverity(string raw, out RuleSeverity severity, out string error)
        {
            error = default;
            if (string.IsNullOrWhiteSpace(raw))
            {
                severity = RuleSeverity.Suggest;
                return true;
            }

            switch (raw.Trim().ToLowerInvariant())
            {
                case "all":
                case "suggest":
                    severity = RuleSeverity.Suggest;
                    return true;
                case "problem":
                case "warning":
                    severity = RuleSeverity.Problem;
                    return true;
                case "error":
                    severity = RuleSeverity.Error;
                    return true;
                default:
                    severity = default;
                    error = $"minSeverity expects 'error', 'problem' or 'suggest' (got '{raw}').";
                    return false;
            }
        }
    }
}
