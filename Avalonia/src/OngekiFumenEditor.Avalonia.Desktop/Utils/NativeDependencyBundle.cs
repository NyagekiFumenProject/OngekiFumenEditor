using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OngekiFumenEditor.Avalonia.Desktop.Utils;

/// <summary>
/// Extracts the published NativeAOT dependencies without relocating the application or its settings.
/// </summary>
internal static class NativeDependencyBundle
{
    private const string ResourcePrefix = "OngekiFumenEditor.NativeDependencies";
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private static readonly object initializationLock = new();
    // Keep the registration for the process lifetime: libraries are loaded lazily by their consumers.
    private static nint directoryCookie;

    internal static void Initialize()
    {
        lock (initializationLock)
        {
            if (directoryCookie != 0)
                return;
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("The bundled native dependencies require Windows.");

            var assembly = typeof(NativeDependencyBundle).Assembly;
            using var fingerprintStream = assembly.GetManifestResourceStream(ResourcePrefix + ".sha256")
                ?? throw new InvalidDataException("The native dependency bundle fingerprint is missing.");
            using var fingerprintReader = new StreamReader(fingerprintStream);
            var fingerprint = fingerprintReader.ReadToEnd().Trim();
            if (fingerprint.Length != 64 || fingerprint.Any(character => !char.IsAsciiHexDigit(character)))
                throw new InvalidDataException("The native dependency bundle fingerprint is not a SHA-256 hash.");
            fingerprint = fingerprint.ToLowerInvariant();

            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApplicationData))
                throw new IOException("A per-user local application data directory is required to extract native dependencies.");
            var cacheRoot = Path.Combine(localApplicationData, "OngekiFumenEditor", "NativeDependencies");
            var cacheDirectory = Path.Combine(cacheRoot, fingerprint);

            using var bundleStream = assembly.GetManifestResourceStream(ResourcePrefix + ".zip")
                ?? throw new InvalidDataException("The native dependency bundle is missing.");
            using var archive = new ZipArchive(bundleStream, ZipArchiveMode.Read, leaveOpen: true);
            ValidateEntries(archive);
            EnsureExtracted(archive, bundleStream, fingerprint, cacheRoot, cacheDirectory);

            // Application directory + explicitly registered directories + System32, never the CWD/PATH.
            if (!SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not configure native DLL search directories.");
            var cookie = AddDllDirectory(cacheDirectory);
            if (cookie == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register native dependency directory '{cacheDirectory}'.");
            directoryCookie = cookie;
        }
    }

    private static void ValidateEntries(ZipArchive archive)
    {
        if (archive.Entries.Count == 0)
            throw new InvalidDataException("The native dependency bundle is empty.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            if (name.Length <= 4 || !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '.' && character != '_' && character != '-')
                || IsDeviceName(name) || !names.Add(name) || entry.Length == 0)
                throw new InvalidDataException($"Invalid or duplicate native dependency entry '{name}'. Only flat DLL filenames are allowed.");
        }
    }

    private static bool IsDeviceName(string name)
    {
        var stem = name.AsSpan(0, name.IndexOf('.'));
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (stem.Length == 4 && stem[3] is >= '1' and <= '9'
                && (stem[..3].Equals("COM", StringComparison.OrdinalIgnoreCase)
                    || stem[..3].Equals("LPT", StringComparison.OrdinalIgnoreCase)));
    }

    private static void EnsureExtracted(
        ZipArchive archive,
        Stream bundleStream,
        string fingerprint,
        string cacheRoot,
        string cacheDirectory)
    {
        if (Directory.Exists(cacheDirectory))
        {
            ValidateCache(archive, cacheDirectory);
            return;
        }

        // Hash the embedded payload only on a cold start, not every time the application opens.
        bundleStream.Position = 0;
        var actualFingerprint = Convert.ToHexStringLower(SHA256.HashData(bundleStream));
        if (!string.Equals(actualFingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("The embedded native dependency bundle failed its SHA-256 integrity check.");

        Directory.CreateDirectory(cacheRoot);
        var stagingDirectory = Path.Combine(cacheRoot, $".{fingerprint}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            archive.ExtractToDirectory(stagingDirectory);
            ValidateCache(archive, stagingDirectory);
            try
            {
                // The final directory becomes visible only after every DLL has been fully written.
                Directory.Move(stagingDirectory, cacheDirectory);
            }
            catch (IOException) when (Directory.Exists(cacheDirectory))
            {
                // A concurrent first launch completed the same immutable bundle first.
                ValidateCache(archive, cacheDirectory);
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    private static void ValidateCache(ZipArchive archive, string cacheDirectory)
    {
        if ((File.GetAttributes(cacheDirectory) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"The native dependency cache must not be a directory link: '{cacheDirectory}'.");

        foreach (var entry in archive.Entries)
        {
            var file = new FileInfo(Path.Combine(cacheDirectory, entry.FullName));
            if (!file.Exists || file.Length != entry.Length || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"The native dependency cache is incomplete or invalid: '{file.FullName}'. Remove the cache directory and restart the application.");
        }
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern nint AddDllDirectory(string newDirectory);
}
