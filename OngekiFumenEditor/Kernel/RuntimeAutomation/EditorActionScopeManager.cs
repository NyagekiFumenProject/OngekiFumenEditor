using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IEditorActionScopeManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class EditorActionScopeManager : IEditorActionScopeManager
    {
        private sealed class Scope
        {
            public string EditorId { get; init; }
            public string IdentityKey { get; init; }
            public List<EditorActionOutcome> Outcomes { get; } = new();
        }

        private readonly object sync = new();
        private readonly Dictionary<string, Scope> scopes = new();

        public bool TryBegin(string editorId, string identityKey, out string errorCode, out string errorMessage)
        {
            errorCode = default;
            errorMessage = default;

            if (string.IsNullOrWhiteSpace(editorId))
            {
                errorCode = "EDITOR_NOT_FOUND";
                errorMessage = "An editorId is required to open an action scope.";
                return false;
            }

            lock (sync)
            {
                if (scopes.TryGetValue(editorId, out var existing))
                {
                    errorCode = "SCOPE_ALREADY_OPEN";
                    errorMessage = $"An action scope is already open for editor '{editorId}' (owner '{existing.IdentityKey}'). Call editor.end_action first.";
                    return false;
                }

                scopes[editorId] = new Scope
                {
                    EditorId = editorId,
                    IdentityKey = identityKey,
                };
                return true;
            }
        }

        public bool IsOpenFor(string editorId)
        {
            lock (sync)
                return !string.IsNullOrWhiteSpace(editorId) && scopes.ContainsKey(editorId);
        }

        public bool TryTrack(string editorId, string identityKey, EditorActionOutcome outcome)
        {
            lock (sync)
            {
                if (!scopes.TryGetValue(editorId, out var scope) || scope.IdentityKey != identityKey)
                    return false;

                scope.Outcomes.Add(outcome);
                return true;
            }
        }

        public bool TryTake(string editorId, string identityKey, out EditorActionScopeState state, out string errorCode, out string errorMessage)
        {
            state = default;
            errorCode = default;
            errorMessage = default;

            lock (sync)
            {
                if (!scopes.TryGetValue(editorId, out var scope))
                {
                    errorCode = "NO_OPEN_SCOPE";
                    errorMessage = $"No action scope is open for editor '{editorId}'. Call editor.begin_action first.";
                    return false;
                }

                if (scope.IdentityKey != identityKey)
                {
                    errorCode = "SCOPE_OWNER_MISMATCH";
                    errorMessage = $"The open action scope for editor '{editorId}' belongs to '{scope.IdentityKey}'.";
                    return false;
                }

                scopes.Remove(editorId);
                state = new EditorActionScopeState
                {
                    EditorId = editorId,
                    IdentityKey = identityKey,
                    Outcomes = scope.Outcomes.ToArray(),
                };
                return true;
            }
        }
    }
}
