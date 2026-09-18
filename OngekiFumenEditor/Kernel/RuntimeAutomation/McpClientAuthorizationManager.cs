using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Kernel.RuntimeAutomation
{
    [Export(typeof(IMcpClientAuthorizationManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class McpClientAuthorizationManager : IMcpClientAuthorizationManager
    {
        private const string AnonymousIdentityKey = "anonymous";

        private sealed class Entry
        {
            public string IdentityKey { get; set; }
            public string RequestedBy { get; set; }
            public string ClientId { get; set; }
            public bool IsExecutionApproved { get; set; }
            public DateTimeOffset LastSeenUtc { get; set; }
        }

        private readonly object sync = new object();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

        public McpClientRegistrationInfo RegisterClientUsage(string requestedBy, string clientId)
        {
            requestedBy = NormalizeRequestedBy(requestedBy);
            clientId = NormalizeClientId(clientId);
            var identityKey = BuildClientIdentityKey(requestedBy, clientId);
            if (string.IsNullOrWhiteSpace(identityKey))
                return default;

            lock (sync)
            {
                if (!entries.TryGetValue(identityKey, out var entry))
                {
                    entry = new Entry
                    {
                        IdentityKey = identityKey,
                    };
                    entries.Add(identityKey, entry);
                }

                entry.RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? entry.RequestedBy : requestedBy;
                entry.ClientId = string.IsNullOrWhiteSpace(clientId) ? entry.ClientId : clientId;
                entry.LastSeenUtc = DateTimeOffset.UtcNow;

                return ToInfo(entry);
            }
        }

        public bool IsExecutionApprovalRemembered(string requestedBy, string clientId)
        {
            var identityKey = BuildClientIdentityKey(NormalizeRequestedBy(requestedBy), NormalizeClientId(clientId));
            if (string.IsNullOrWhiteSpace(identityKey))
                return false;

            lock (sync)
                return entries.TryGetValue(identityKey, out var entry) && entry.IsExecutionApproved;
        }

        public void RememberExecutionApproval(string requestedBy, string clientId)
        {
            requestedBy = NormalizeRequestedBy(requestedBy);
            clientId = NormalizeClientId(clientId);
            var identityKey = BuildClientIdentityKey(requestedBy, clientId);
            if (string.IsNullOrWhiteSpace(identityKey))
                return;

            lock (sync)
            {
                if (!entries.TryGetValue(identityKey, out var entry))
                {
                    entry = new Entry
                    {
                        IdentityKey = identityKey,
                    };
                    entries.Add(identityKey, entry);
                }

                entry.RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? entry.RequestedBy : requestedBy;
                entry.ClientId = string.IsNullOrWhiteSpace(clientId) ? entry.ClientId : clientId;
                entry.IsExecutionApproved = true;
                entry.LastSeenUtc = DateTimeOffset.UtcNow;
            }
        }

        public bool RevokeExecutionApproval(string identityKey)
        {
            identityKey = string.IsNullOrWhiteSpace(identityKey) ? default : identityKey.Trim();
            if (string.IsNullOrWhiteSpace(identityKey))
                return false;

            lock (sync)
            {
                if (!entries.TryGetValue(identityKey, out var entry) || !entry.IsExecutionApproved)
                    return false;

                entry.IsExecutionApproved = false;
                return true;
            }
        }

        public IReadOnlyList<McpClientRegistrationInfo> GetKnownClients()
        {
            lock (sync)
            {
                return entries.Values
                    .OrderByDescending(x => x.LastSeenUtc)
                    .Select(ToInfo)
                    .ToArray();
            }
        }

        private static McpClientRegistrationInfo ToInfo(Entry entry)
        {
            return new McpClientRegistrationInfo
            {
                IdentityKey = entry.IdentityKey,
                RequestedBy = entry.RequestedBy,
                ClientId = entry.ClientId,
                IsExecutionApproved = entry.IsExecutionApproved,
                LastSeenUtc = entry.LastSeenUtc,
            };
        }

        private static string NormalizeRequestedBy(string requestedBy)
        {
            return string.IsNullOrWhiteSpace(requestedBy) ? default : requestedBy.Trim();
        }

        private static string NormalizeClientId(string clientId)
        {
            return string.IsNullOrWhiteSpace(clientId) ? default : clientId.Trim();
        }

        private static string BuildClientIdentityKey(string requestedBy, string clientId)
        {
            if (!string.IsNullOrWhiteSpace(clientId))
                return $"clientId:{clientId}";

            if (!string.IsNullOrWhiteSpace(requestedBy))
                return $"requestedBy:{requestedBy}";

            return AnonymousIdentityKey;
        }
    }
}
