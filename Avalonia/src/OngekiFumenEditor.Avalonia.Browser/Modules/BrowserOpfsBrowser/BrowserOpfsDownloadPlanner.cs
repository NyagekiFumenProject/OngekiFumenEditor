#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OngekiFumenEditor.Avalonia.Browser.Modules.BrowserOpfsBrowser;

public sealed record BrowserOpfsDownloadPlan(
    IReadOnlyList<BrowserOpfsEntrySnapshot> SelectedEntries,
    bool UseZip,
    string SuggestedFileName);

public static class BrowserOpfsDownloadPlanner
{
    public static BrowserOpfsDownloadPlan Create(
        IReadOnlyList<BrowserOpfsEntrySnapshot> selectedEntries,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(selectedEntries);

        var normalized = selectedEntries
            .Where(x => x.StagingState == BrowserOpfsStagingState.None)
            .DistinctBy(x => NormalizePath(x.RelativePath), StringComparer.Ordinal)
            .OrderBy(x => GetPathDepth(x.RelativePath))
            .ThenBy(x => x.RelativePath, NaturalStringComparer.Instance)
            .ToArray();

        var deduplicated = new List<BrowserOpfsEntrySnapshot>(normalized.Length);
        foreach (var entry in normalized)
        {
            string path = NormalizePath(entry.RelativePath);
            if (deduplicated.Any(parent =>
                    parent.Kind == BrowserOpfsEntryKind.Folder &&
                    IsDescendantPath(path, NormalizePath(parent.RelativePath))))
                continue;

            deduplicated.Add(entry with { RelativePath = path });
        }

        if (deduplicated.Count == 0)
            throw new InvalidOperationException("At least one selectable OPFS entry is required for download.");

        bool useZip = deduplicated.Count != 1 || deduplicated[0].Kind == BrowserOpfsEntryKind.Folder;
        string suggestedFileName = useZip
            ? deduplicated.Count == 1
                ? $"{deduplicated[0].Name}.zip"
                : $"opfs-export-{now.ToLocalTime():yyyyMMdd-HHmmss}.zip"
            : deduplicated[0].Name;

        return new BrowserOpfsDownloadPlan(
            deduplicated,
            useZip,
            SanitizeFileName(suggestedFileName));
    }

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "download";

        char[] invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*'])
            .Distinct()
            .ToArray();
        var result = fileName.Select(character =>
                invalidCharacters.Contains(character) || char.IsControl(character) ? '_' : character)
            .ToArray();
        string sanitized = new string(result).TrimEnd(' ', '.');
        return string.IsNullOrWhiteSpace(sanitized) ? "download" : sanitized;
    }

    private static int GetPathDepth(string path) =>
        NormalizePath(path).Count(character => character == '/');

    private static string NormalizePath(string path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/');

    private static bool IsDescendantPath(string path, string parentPath) =>
        parentPath.Length == 0
            ? path.Length > 0
            : path.Length > parentPath.Length &&
              path.StartsWith(parentPath, StringComparison.Ordinal) &&
              path[parentPath.Length] == '/';
}

internal sealed class NaturalStringComparer : IComparer<string>
{
    public static NaturalStringComparer Instance { get; } = new();

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;

        int leftIndex = 0;
        int rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
            {
                int leftZeroStart = leftIndex;
                int rightZeroStart = rightIndex;
                while (leftZeroStart < left.Length && left[leftZeroStart] == '0')
                    leftZeroStart++;
                while (rightZeroStart < right.Length && right[rightZeroStart] == '0')
                    rightZeroStart++;

                int leftEnd = leftZeroStart;
                int rightEnd = rightZeroStart;
                while (leftEnd < left.Length && char.IsDigit(left[leftEnd]))
                    leftEnd++;
                while (rightEnd < right.Length && char.IsDigit(right[rightEnd]))
                    rightEnd++;

                int significantLengthComparison = (leftEnd - leftZeroStart).CompareTo(rightEnd - rightZeroStart);
                if (significantLengthComparison != 0)
                    return significantLengthComparison;

                int numericComparison = string.Compare(
                    left,
                    leftZeroStart,
                    right,
                    rightZeroStart,
                    leftEnd - leftZeroStart,
                    StringComparison.Ordinal);
                if (numericComparison != 0)
                    return numericComparison;

                int leftDigitEnd = leftEnd;
                int rightDigitEnd = rightEnd;
                int zeroCountComparison = (leftZeroStart - leftIndex).CompareTo(rightZeroStart - rightIndex);
                if (zeroCountComparison != 0)
                    return zeroCountComparison;

                leftIndex = leftDigitEnd;
                rightIndex = rightDigitEnd;
                continue;
            }

            int characterComparison = char.ToUpperInvariant(left[leftIndex])
                .CompareTo(char.ToUpperInvariant(right[rightIndex]));
            if (characterComparison != 0)
                return characterComparison;

            leftIndex++;
            rightIndex++;
        }

        return left.Length.CompareTo(right.Length);
    }
}
