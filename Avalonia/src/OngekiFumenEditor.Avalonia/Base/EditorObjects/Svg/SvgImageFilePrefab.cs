#nullable enable

using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;

namespace OngekiFumenEditor.Avalonia.Base.EditorObjects.Svg;

public sealed class SvgImageFilePrefab : SvgPrefabBase
{
    public const string CommandName = "[SVG_IMG]";
    private ISimpleFile? svgFile;
    private SvgFileLease? svgFileLease;

    public override string IDShortName => CommandName;

    [ObjectPropertyBrowserSingleSelectedOnly]
    public ISimpleFile? SvgFile
    {
        get => svgFile;
        set
        {
            if (ReferenceEquals(svgFile, value))
                return;

            SetSvgFile(value is null ? null : new SvgFileLease(value));
        }
    }

    public override void Copy(OngekiObjectBase fromObj)
    {
        base.Copy(fromObj);
        if (fromObj is not SvgImageFilePrefab from)
            return;

        SetSvgFile(from.svgFileLease?.Share());
    }

    public void ReloadSvgFile()
    {
        CleanGeometry();
        if (SvgFile is null ||
            (!string.IsNullOrWhiteSpace(SvgFile.LocalPath) && !File.Exists(SvgFile.LocalPath)))
            return;

        using var stream = SvgFile.OpenRead().GetAwaiter().GetResult();
        ApplySvgContent(stream);
    }

    public override void Dispose()
    {
        svgFileLease?.Dispose();
        svgFileLease = null;
        svgFile = null;
        base.Dispose();
    }

    public override string ToString() => $"{base.ToString()} File[{SvgFile?.FileName}]";

    private void SetSvgFile(SvgFileLease? nextLease)
    {
        var nextFile = nextLease?.File;
        svgFileLease?.Dispose();
        svgFileLease = nextLease;
        if (SetProperty(ref svgFile, nextFile))
            ReloadSvgFile();
    }

    private sealed class SvgFileLease : IDisposable
    {
        private SharedState? state;

        public SvgFileLease(ISimpleFile file)
        {
            state = new SharedState(file);
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
            private ISimpleFile? file;
            private int referenceCount = 1;

            public SharedState(ISimpleFile file)
            {
                this.file = file ?? throw new ArgumentNullException(nameof(file));
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

                fileToDispose?.Dispose();
            }
        }
    }
}
