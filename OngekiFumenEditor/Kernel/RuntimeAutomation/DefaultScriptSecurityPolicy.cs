using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IScriptSecurityPolicy))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class DefaultScriptSecurityPolicy : IScriptSecurityPolicy
    {
        private static readonly CSharpParseOptions ParserOptions = CSharpParseOptions.Default
            .WithKind(Microsoft.CodeAnalysis.SourceCodeKind.Script)
            .WithLanguageVersion(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview);

        private static readonly string[] BlockedTokens =
        [
            "#r",
            "System.Reflection",
            "Activator.CreateInstance",
            "Assembly.Load",
            "Type.GetType",
            "MethodInfo",
            "PropertyInfo",
            "FieldInfo",
            "System.Diagnostics.Process",
            "System.IO.File",
            "System.IO.Directory",
            "System.Net",
            "Caliburn.Micro.IoC",
            "IoC.Get",
            "ScriptArgsGlobalStore",
            "IEditorDocumentManager",
            "FumenVisualEditorViewModel",
        ];

        public ScriptSecurityCheckResult Check(string scriptText)
        {
            scriptText ??= string.Empty;

            var issues = BlockedTokens
                .Where(x => scriptText.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(x => $"Blocked token detected: {x}")
                .ToList();

            AnalyzeUndoableScriptPattern(scriptText, issues);

            return new ScriptSecurityCheckResult
            {
                Success = issues.Count <= 0,
                Issues = issues.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            };
        }

        private static void AnalyzeUndoableScriptPattern(string scriptText, List<string> issues)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(scriptText, ParserOptions);
            var root = syntaxTree.GetRoot();

            var allowedTargetEditorAccessSpans = new List<TextSpan>();
            var executeActionInvocations = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(IsExecuteActionInvocation)
                .ToArray();

            if (executeActionInvocations.Length <= 0)
            {
                issues.Add("MCP runtime scripts must modify chart state via UndoRedoManager.ExecuteAction with explicit redo and undo lambdas.");
            }

            foreach (var invocation in executeActionInvocations)
            {
                if (!TryCollectAllowedTargetEditorAccessSpans(invocation, allowedTargetEditorAccessSpans, out var issue))
                    issues.Add(issue);
            }

            foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(IsScriptArgsTargetEditorAccess))
            {
                if (!allowedTargetEditorAccessSpans.Any(span => Contains(span, access.Span)))
                {
                    issues.Add($"ScriptArgs.TargetEditor is only allowed inside UndoRedoManager.ExecuteAction redo/undo lambdas: {TrimSnippet(access.ToString())}");
                }
            }
        }

        private static bool TryCollectAllowedTargetEditorAccessSpans(InvocationExpressionSyntax executeActionInvocation, ICollection<TextSpan> allowedSpans, out string issue)
        {
            issue = default;

            if (executeActionInvocation.ArgumentList?.Arguments.Count != 1)
            {
                issue = "UndoRedoManager.ExecuteAction must receive exactly one inline undo action argument.";
                return false;
            }

            if (!TryGetUndoActionParts(executeActionInvocation.ArgumentList.Arguments[0].Expression, out var redoLambda, out var undoLambda))
            {
                issue = "UndoRedoManager.ExecuteAction must use LambdaUndoAction.Create(name, redo, undo) or new LambdaUndoAction(name, redo, undo).";
                return false;
            }

            allowedSpans.Add(executeActionInvocation.Expression.Span);
            allowedSpans.Add(redoLambda.Span);
            allowedSpans.Add(undoLambda.Span);
            return true;
        }

        private static bool TryGetUndoActionParts(ExpressionSyntax expression, out AnonymousFunctionExpressionSyntax redoLambda, out AnonymousFunctionExpressionSyntax undoLambda)
        {
            redoLambda = default;
            undoLambda = default;

            SeparatedSyntaxList<ArgumentSyntax> arguments;
            if (expression is InvocationExpressionSyntax invocation && IsLambdaUndoActionCreateInvocation(invocation))
            {
                arguments = invocation.ArgumentList.Arguments;
            }
            else if (expression is ObjectCreationExpressionSyntax objectCreation && IsLambdaUndoActionConstructor(objectCreation))
            {
                arguments = objectCreation.ArgumentList?.Arguments ?? default;
            }
            else
            {
                return false;
            }

            if (arguments.Count != 3)
                return false;

            redoLambda = arguments[1].Expression as AnonymousFunctionExpressionSyntax;
            undoLambda = arguments[2].Expression as AnonymousFunctionExpressionSyntax;
            return redoLambda is not null && undoLambda is not null;
        }

        private static bool IsExecuteActionInvocation(InvocationExpressionSyntax invocation)
        {
            return invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                   string.Equals(memberAccess.Name.Identifier.ValueText, "ExecuteAction", StringComparison.Ordinal);
        }

        private static bool IsLambdaUndoActionCreateInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            return string.Equals(memberAccess.Name.Identifier.ValueText, "Create", StringComparison.Ordinal) &&
                   EndsWithIdentifier(memberAccess.Expression, "LambdaUndoAction");
        }

        private static bool IsLambdaUndoActionConstructor(ObjectCreationExpressionSyntax objectCreation)
        {
            return EndsWithIdentifier(objectCreation.Type, "LambdaUndoAction");
        }

        private static bool IsScriptArgsTargetEditorAccess(MemberAccessExpressionSyntax memberAccess)
        {
            return string.Equals(memberAccess.Name.Identifier.ValueText, "TargetEditor", StringComparison.Ordinal) &&
                   EndsWithIdentifier(memberAccess.Expression, "ScriptArgs");
        }

        private static bool EndsWithIdentifier(SyntaxNode node, string identifier)
        {
            return node switch
            {
                IdentifierNameSyntax id => string.Equals(id.Identifier.ValueText, identifier, StringComparison.Ordinal),
                GenericNameSyntax generic => string.Equals(generic.Identifier.ValueText, identifier, StringComparison.Ordinal),
                MemberAccessExpressionSyntax memberAccess => string.Equals(memberAccess.Name.Identifier.ValueText, identifier, StringComparison.Ordinal),
                QualifiedNameSyntax qualifiedName => string.Equals(qualifiedName.Right.Identifier.ValueText, identifier, StringComparison.Ordinal),
                AliasQualifiedNameSyntax aliasQualifiedName => string.Equals(aliasQualifiedName.Name.Identifier.ValueText, identifier, StringComparison.Ordinal),
                _ => false,
            };
        }

        private static bool Contains(TextSpan outer, TextSpan inner)
        {
            return inner.Start >= outer.Start && inner.End <= outer.End;
        }

        private static string TrimSnippet(string snippet)
        {
            if (string.IsNullOrWhiteSpace(snippet))
                return "<empty>";

            snippet = snippet.Replace("\r", " ").Replace("\n", " ").Trim();
            return snippet.Length > 120 ? snippet[..120] + "..." : snippet;
        }
    }
}
