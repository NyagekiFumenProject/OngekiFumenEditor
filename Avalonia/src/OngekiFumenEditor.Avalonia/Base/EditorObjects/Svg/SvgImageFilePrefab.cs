#nullable enable

using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public sealed class SvgImageFilePrefab : SvgPrefabBase
{
    public const string CommandName = "[SVG_IMG]";
    private ISimpleFile? svgFile;
    private SvgFileLease? svgFileLease;
    private string svgFilePath = string.Empty;

    public override string IDShortName => CommandName;

    [ObjectPropertyBrowserSingleSelectedOnly]
    public ISimpleFile? SvgFile
    {
        get => svgFile;
        set
        {
            if (ReferenceEquals(svgFile, value))
                return;

            SetSvgFilePath(value?.FullPath ?? string.Empty);
            SetSvgFile(value is null ? null : new SvgFileLease(value, ownsFile: true));
        }
    }

    [ObjectPropertyBrowserHide]
    public string SvgFilePath
    {
        get => svgFilePath;
        set
        {
            var locator = value ?? string.Empty;
            if (!SetSvgFilePath(locator))
                return;

            //todo:
            /*
            SetSvgFile(
                string.IsNullOrWhiteSpace(locator)
                    ? null
                    : new SvgFileLease(new SerializedSvgFileLocator(locator), ownsFile: true),
                reload: false);
            */
        }
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgImageFilePrefab from)
            return;

        SetSvgFilePath(from.SvgFilePath);
        SetSvgFile(from.svgFileLease?.Share());
    }

    public async Task BindProjectFileAsync(
        ISimpleFile file,
        string projectRelativeLocator,
        CancellationToken cancellationToken = default)
    {
#if ENABLE_SVG_PREFAB_OBJECTS
        var content = await file.ReadAllBytes();
        cancellationToken.ThrowIfCancellationRequested();
        var memoryFile = new ProjectResourceSimpleFile(file, projectRelativeLocator, content);
        try
        {
            await BindFileAsync(memoryFile, projectRelativeLocator, ownsFile: true, cancellationToken);
        }
        catch
        {
            memoryFile.Dispose();
            throw;
        }
#else
        await Task.CompletedTask;
        throw new NotSupportedException("SVG prefab project resources are temporarily disabled.");
#endif
    }

    public async Task BindOwnedFileAsync(
        ISimpleFile file,
        string locator,
        CancellationToken cancellationToken = default)
    {
#if ENABLE_SVG_PREFAB_OBJECTS
        await BindFileAsync(file, locator, ownsFile: true, cancellationToken);
#else
        await Task.CompletedTask;
        throw new NotSupportedException("SVG prefab project resources are temporarily disabled.");
#endif
    }

#if ENABLE_SVG_PREFAB_OBJECTS
    private async Task BindFileAsync(
        ISimpleFile file,
        string locator,
        bool ownsFile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);

        try
        {
            await using var stream = await file.OpenRead().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            ApplySvgContent(stream);
            SetSvgFilePath(locator);
            SetSvgFile(new SvgFileLease(file, ownsFile), reload: false);
        }
        catch
        {
            if (ownsFile)
                file.Dispose();
            throw;
        }
    }
#endif

    public void ReloadSvgFile()
    {
        CleanGeometry();
#if ENABLE_SVG_PREFAB_OBJECTS
        if (SvgFile is null ||
            (!string.IsNullOrWhiteSpace(SvgFile.LocalPath) && !File.Exists(SvgFile.LocalPath)))
            return;

        using var stream = SvgFile.OpenRead().GetAwaiter().GetResult();
        ApplySvgContent(stream);
#endif
    }

    public override void Dispose()
    {
        svgFileLease?.Dispose();
        svgFileLease = null;
        svgFile = null;
        base.Dispose();
    }

    public override string ToString() => $"{base.ToString()} File[{SvgFile?.FileName}]";

    private void SetSvgFile(SvgFileLease? nextLease, bool reload = true)
    {
        var nextFile = nextLease?.File;
        svgFileLease?.Dispose();
        svgFileLease = nextLease;
        if (SetProperty(ref svgFile, nextFile) && reload)
            ReloadSvgFile();
    }

    private bool SetSvgFilePath(string value) =>
        SetProperty(ref svgFilePath, value ?? string.Empty, nameof(SvgFilePath));

    private sealed class SvgFileLease : IDisposable
    {
        private SharedState? state;

        public SvgFileLease(ISimpleFile file, bool ownsFile)
        {
            state = new SharedState(file, ownsFile);
        }

        private SvgFileLease(SharedState state)
        {
            state.Retain();
            this.state = state;
        }

        public ISimpleFile File => GetState().File;

        public SvgFileLease Share() => new(GetState());

        public void Dispose()
        {
            Interlocked.Exchange(ref state, null)?.Release();
        }

        private SharedState GetState() =>
            state ?? throw new ObjectDisposedException(nameof(SvgFileLease));

        private sealed class SharedState
        {
            private readonly object syncRoot = new();
            private readonly bool ownsFile;
            private ISimpleFile? file;
            private int referenceCount = 1;

            public SharedState(ISimpleFile file, bool ownsFile)
            {
                this.file = file ?? throw new ArgumentNullException(nameof(file));
                this.ownsFile = ownsFile;
            }

            public ISimpleFile File
            {
                get
                {
                    lock (syncRoot)
                        return file ?? throw new ObjectDisposedException(nameof(SvgFileLease));
                }
            }

            public void Retain()
            {
                lock (syncRoot)
                {
                    ObjectDisposedException.ThrowIf(file is null, this);
                    referenceCount++;
                }
            }

            public void Release()
            {
                ISimpleFile? fileToDispose = null;
                lock (syncRoot)
                {
                    if (file is null)
                        return;

                    referenceCount--;
                    if (referenceCount == 0)
                    {
                        fileToDispose = file;
                        file = null;
                    }
                }

                if (ownsFile)
                    fileToDispose?.Dispose();
            }
        }
    }
}
