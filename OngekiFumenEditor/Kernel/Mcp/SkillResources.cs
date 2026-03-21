using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.ObjectModel;

namespace OngekiFumenEditor.Kernel.Mcp
{
    [Export(typeof(SkillResources))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class SkillResources
    {
        private const string SkillScheme = "skill://";

        private static readonly string SkillsRootDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Resources", "skills"));
        private IReadOnlyList<McpServerResource> directResources;

        public string BuildServerInstructions()
        {
            var skillDirectories = EnumerateSkillDirectories().ToArray();
            if (skillDirectories.Length == 0)
                return "This MCP server can expose built-in repo guidance as read-only resources under skill:// when packaged skills are available.";

            var builder = new StringBuilder();
            builder.Append("This MCP server ships built-in repo guidance as read-only resources under skill://. ");
            builder.Append("Read skill://index first to discover available skills, then use resources/list to inspect the packaged skill entries if needed. ");
            builder.Append("When working in a specific skill area, read that skill's index resource and then its SKILL.md before using tools. ");
            builder.Append("Use tools for runtime inspection or mutation; use skill:// resources for implementation guidance.");
            return builder.ToString();
        }

        public IReadOnlyList<McpServerResource> BuildDirectResources()
        {
            if (directResources is not null)
                return directResources;

            var resources = new List<McpServerResource>();

            foreach (var skillName in EnumerateSkillDirectories().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                resources.Add(CreateDirectResource(
                    () => GetSkillIndex(skillName),
                    $"skills.{skillName}.index",
                    $"{skillName} Skill Manifest",
                    $"List the main files and entry URIs for the packaged skill '{skillName}'.",
                    "text/markdown",
                    BuildSkillIndexUri(skillName)));

                resources.Add(CreateDirectResource(
                    () => ReadSkillDocument(skillName),
                    $"skills.{skillName}.skill_md",
                    $"{skillName} SKILL.md",
                    $"Read the SKILL.md file for the packaged skill '{skillName}'.",
                    "text/markdown",
                    BuildSkillDocumentUri(skillName)));

                var skillDirectory = GetSkillDirectory(skillName);

                var agentDirectory = Path.Combine(skillDirectory, "agents");
                if (Directory.Exists(agentDirectory))
                {
                    foreach (var agentFile in Directory.EnumerateFiles(agentDirectory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileName(agentFile);
                        resources.Add(CreateDirectResource(
                            () => ReadAgentFile(skillName, fileName),
                            $"skills.{skillName}.agents.{ToResourceSafeName(fileName)}",
                            $"{skillName} Agent File {fileName}",
                            $"Read the agent metadata file '{fileName}' for the packaged skill '{skillName}'.",
                            GuessMimeType(fileName),
                            BuildAgentUri(skillName, fileName)));
                    }
                }

                var referencesDirectory = Path.Combine(skillDirectory, "references");
                if (Directory.Exists(referencesDirectory))
                {
                    foreach (var referenceFile in Directory.EnumerateFiles(referencesDirectory, "*.md").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileName(referenceFile);
                        resources.Add(CreateDirectResource(
                            () => ReadReferenceFile(skillName, fileName),
                            $"skills.{skillName}.references.{ToResourceSafeName(fileName)}",
                            $"{skillName} Reference {fileName}",
                            $"Read the reference file '{fileName}' for the packaged skill '{skillName}'.",
                            "text/markdown",
                            BuildReferenceUri(skillName, fileName)));
                    }
                }
            }

            directResources = new ReadOnlyCollection<McpServerResource>(resources);
            return directResources;
        }

        [McpServerResource(Name = "skills.index", Title = "Built-in Skill Index", MimeType = "text/markdown", UriTemplate = "skill://index")]
        [Description("List all built-in skills packaged with this MCP server and the main resource URIs to read first.")]
        public TextResourceContents GetIndex(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var skillDirectories = EnumerateSkillDirectories().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var builder = new StringBuilder();
            builder.AppendLine("# Built-in Skills");
            builder.AppendLine();

            if (skillDirectories.Length == 0)
            {
                builder.AppendLine("No packaged skills were found under `Resources/skills`.");
            }
            else
            {
                builder.AppendLine("Read a skill's `index` resource first, then read its `SKILL.md`.");
                builder.AppendLine();

                foreach (var skillName in skillDirectories)
                {
                    builder.Append("* `");
                    builder.Append(skillName);
                    builder.Append("`: `");
                    builder.Append(BuildSkillIndexUri(skillName));
                    builder.Append("`, `");
                    builder.Append(BuildSkillDocumentUri(skillName));
                    builder.AppendLine("`");
                }
            }

            return CreateTextResource("skill://index", "text/markdown", builder.ToString());
        }

        [McpServerResource(Name = "skills.skill_index", Title = "Built-in Skill Manifest", MimeType = "text/markdown", UriTemplate = "skill://{skillName}/index")]
        [Description("List the main files and resource URIs for one packaged skill.")]
        public TextResourceContents GetSkillIndex(string skillName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            skillName = NormalizePathSegment(skillName, nameof(skillName));
            var skillDirectory = GetSkillDirectory(skillName);
            var builder = new StringBuilder();
            builder.Append("# ");
            builder.Append(skillName);
            builder.AppendLine();
            builder.AppendLine();
            builder.Append("* `SKILL.md`: `");
            builder.Append(BuildSkillDocumentUri(skillName));
            builder.AppendLine("`");

            var agentDirectory = Path.Combine(skillDirectory, "agents");
            if (Directory.Exists(agentDirectory))
            {
                foreach (var agentFile in Directory.EnumerateFiles(agentDirectory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(agentFile);
                    builder.Append("* `agents/");
                    builder.Append(fileName);
                    builder.Append("`: `");
                    builder.Append(BuildAgentUri(skillName, fileName));
                    builder.AppendLine("`");
                }
            }

            var referencesDirectory = Path.Combine(skillDirectory, "references");
            if (Directory.Exists(referencesDirectory))
            {
                foreach (var referenceFile in Directory.EnumerateFiles(referencesDirectory, "*.md").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(referenceFile);
                    builder.Append("* `references/");
                    builder.Append(fileName);
                    builder.Append("`: `");
                    builder.Append(BuildReferenceUri(skillName, fileName));
                    builder.AppendLine("`");
                }
            }

            return CreateTextResource(BuildSkillIndexUri(skillName), "text/markdown", builder.ToString());
        }

        [McpServerResource(Name = "skills.skill_document", Title = "Read Skill Document", MimeType = "text/markdown", UriTemplate = "skill://{skillName}/SKILL.md")]
        [Description("Read the SKILL.md file for one packaged skill.")]
        public TextResourceContents ReadSkillDocument(string skillName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            skillName = NormalizePathSegment(skillName, nameof(skillName));
            return ReadTextFile(BuildSkillDocumentUri(skillName), Path.Combine(GetSkillDirectory(skillName), "SKILL.md"), "text/markdown");
        }

        [McpServerResource(Name = "skills.agent_file", Title = "Read Skill Agent Metadata", MimeType = "application/yaml", UriTemplate = "skill://{skillName}/agents/{fileName}")]
        [Description("Read a file from the agents directory for one packaged skill.")]
        public TextResourceContents ReadAgentFile(string skillName, string fileName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            skillName = NormalizePathSegment(skillName, nameof(skillName));
            fileName = NormalizeFileName(fileName, nameof(fileName));
            return ReadTextFile(BuildAgentUri(skillName, fileName), Path.Combine(GetSkillDirectory(skillName), "agents", fileName), GuessMimeType(fileName));
        }

        [McpServerResource(Name = "skills.reference_file", Title = "Read Skill Reference", MimeType = "text/markdown", UriTemplate = "skill://{skillName}/references/{fileName}")]
        [Description("Read a markdown reference file from one packaged skill.")]
        public TextResourceContents ReadReferenceFile(string skillName, string fileName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            skillName = NormalizePathSegment(skillName, nameof(skillName));
            fileName = NormalizeFileName(fileName, nameof(fileName));
            return ReadTextFile(BuildReferenceUri(skillName, fileName), Path.Combine(GetSkillDirectory(skillName), "references", fileName), GuessMimeType(fileName));
        }

        private static IEnumerable<string> EnumerateSkillDirectories()
        {
            if (!Directory.Exists(SkillsRootDirectory))
                yield break;

            foreach (var directory in Directory.EnumerateDirectories(SkillsRootDirectory))
            {
                var skillName = Path.GetFileName(directory);
                if (!string.IsNullOrWhiteSpace(skillName))
                    yield return skillName;
            }
        }

        private static string GetSkillDirectory(string skillName)
        {
            var directory = Path.Combine(SkillsRootDirectory, skillName);
            if (!Directory.Exists(directory))
                throw new FileNotFoundException($"Skill '{skillName}' was not found under '{SkillsRootDirectory}'.");

            return directory;
        }

        private static TextResourceContents ReadTextFile(string uri, string filePath, string mimeType)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Skill resource file was not found: {filePath}");

            return CreateTextResource(uri, mimeType, File.ReadAllText(filePath));
        }

        private static TextResourceContents CreateTextResource(string uri, string mimeType, string text)
        {
            return new TextResourceContents
            {
                Uri = uri,
                MimeType = mimeType,
                Text = text ?? string.Empty,
            };
        }

        private static McpServerResource CreateDirectResource(Func<TextResourceContents> reader, string name, string title, string description, string mimeType, string uri)
        {
            return McpServerResource.Create(reader, new McpServerResourceCreateOptions
            {
                Name = name,
                Title = title,
                Description = description,
                MimeType = mimeType,
                UriTemplate = uri,
            });
        }

        private static string NormalizePathSegment(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A non-empty path segment is required.", parameterName);

            value = value.Trim();
            if (value.Contains("..", StringComparison.Ordinal) || value.IndexOfAny(['/', '\\']) >= 0)
                throw new ArgumentException("Path traversal is not allowed.", parameterName);

            return value;
        }

        private static string NormalizeFileName(string value, string parameterName)
        {
            value = NormalizePathSegment(value, parameterName);
            if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("Invalid file name.", parameterName);

            return value;
        }

        private static string GuessMimeType(string fileName)
        {
            return Path.GetExtension(fileName)?.ToLowerInvariant() switch
            {
                ".md" => "text/markdown",
                ".yaml" => "application/yaml",
                ".yml" => "application/yaml",
                _ => "text/plain",
            };
        }

        private static string ToResourceSafeName(string value)
        {
            return string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_'));
        }

        private static string BuildSkillIndexUri(string skillName) => $"{SkillScheme}{skillName}/index";

        private static string BuildSkillDocumentUri(string skillName) => $"{SkillScheme}{skillName}/SKILL.md";

        private static string BuildAgentUri(string skillName, string fileName) => $"{SkillScheme}{skillName}/agents/{fileName}";

        private static string BuildReferenceUri(string skillName, string fileName) => $"{SkillScheme}{skillName}/references/{fileName}";
    }
}
