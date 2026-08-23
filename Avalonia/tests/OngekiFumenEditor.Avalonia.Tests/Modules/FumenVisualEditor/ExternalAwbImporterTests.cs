using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Setup;
using OngekiFumenEditor.Avalonia.Tests.Utils;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class ExternalAwbImporterTests
{
    static ExternalAwbImporterTests()
    {
        // Plain unit tests run without the application service provider, so give the static
        // Log singleton a silent instance before importer code paths touch it.
        OngekiFumenEditor.Avalonia.Utils.Log.Initialize(new OngekiFumenEditor.Avalonia.Utils.Log([]));
    }

    private static readonly byte[] GoodAwb = "GOOD-AWB-CONTENT"u8.ToArray();
    private static readonly byte[] BrokenAwb = "BROKEN-AWB-CONTENT"u8.ToArray();

    [Fact]
    public async Task MissingProjectAwb_PicksVerifiesAndCommitsNewFile()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        using var picked = new StubAwbFile("song.awb", GoodAwb);
        bool pickerCalled = false;
        var provider = new InMemoryTemporaryFolderProvider();

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(
                name =>
                {
                    pickerCalled = true;
                    return picked;
                }),
            VerifyGoodOnly,
            provider);

        Assert.True(pickerCalled);
        Assert.NotNull(result);
        Assert.Equal(AwbImportAction.CommitNew, result.Action);
        var committed = Assert.IsType<StubAwbFile>(result.BoundAwbFile);
        Assert.Equal(GoodAwb, await committed.ReadAllBytes());
        Assert.Empty(provider.Root.ChildFiles);
    }

    [Fact]
    public async Task CancelledPick_AbortsWithoutModifyingProject()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(_ => null),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider());

        Assert.Null(result);
        Assert.DoesNotContain(directory.ChildFiles, file => file.FileName.Equals("song.awb"));
    }

    [Fact]
    public async Task DecodableExistingAwb_ReusedWithoutPicker()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        var existing = directory.Add("song.awb", GoodAwb);
        bool pickerCalled = false;

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(_ =>
            {
                pickerCalled = true;
                return null;
            }),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider());

        Assert.False(pickerCalled);
        Assert.NotNull(result);
        Assert.Equal(AwbImportAction.BindExisting, result.Action);
        Assert.Same(existing, result.BoundAwbFile);
    }

    [Fact]
    public async Task UndecodableExistingAwb_ConfirmedReplaceCommitsStagedContent()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        var existing = directory.Add("song.awb", BrokenAwb);
        using var picked = new StubAwbFile("external/song.awb", GoodAwb);
        AwbReplaceCandidate? candidate = null;

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(
                _ => picked,
                passed =>
                {
                    candidate = passed;
                    return true;
                }),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider());

        Assert.NotNull(result);
        Assert.Equal(AwbImportAction.ReplaceExisting, result.Action);
        Assert.Same(existing, result.BoundAwbFile);
        Assert.NotNull(candidate);
        Assert.Equal(GoodAwb, await existing.ReadAllBytes());
    }

    [Fact]
    public async Task DeclinedReplace_KeepsProjectAwbUntouched()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        var existing = directory.Add("song.awb", BrokenAwb);
        using var picked = new StubAwbFile("song.awb", GoodAwb);

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(
                _ => picked,
                _ => false),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider());

        Assert.Null(result);
        Assert.Equal(BrokenAwb, await existing.ReadAllBytes());
    }

    [Fact]
    public async Task UndecodablePickedAwb_FailsAndLeavesProjectUntouched()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        var existing = directory.Add("song.awb", BrokenAwb);
        using var picked = new StubAwbFile("song.awb", BrokenAwb);

        await Assert.ThrowsAsync<InvalidDataException>(() => ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(_ => picked),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider()));

        Assert.Equal(BrokenAwb, await existing.ReadAllBytes());
    }

    [Fact]
    public async Task MultipleSiblingCandidates_Throw()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        directory.Add("song.awb", GoodAwb);
        directory.Add("SONG.AWB", GoodAwb);

        await Assert.ThrowsAsync<InvalidDataException>(() => ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(_ => null),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider()));
    }

    [Fact]
    public async Task TransientVerificationFailureWithIdenticalPick_ReusesProjectAwb()
    {
        var directory = new StubAwbDirectory("project");
        using var acb = directory.Add("song.acb", []);
        var existing = directory.Add("song.awb", GoodAwb);
        using var picked = new StubAwbFile("song.awb", (byte[])GoodAwb.Clone());
        uint calls = 0;

        Task FlakyVerify(Stream _, Stream awb, CancellationToken __)
        {
            if (Interlocked.Increment(ref calls) == 1)
                throw new InvalidDataException("transient decode failure");
            return Task.CompletedTask;
        }

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            null,
            Callbacks(_ => picked),
            FlakyVerify,
            new InMemoryTemporaryFolderProvider());

        Assert.NotNull(result);
        Assert.Equal(AwbImportAction.BindExisting, result.Action);
        Assert.Same(existing, result.BoundAwbFile);
    }

    [Fact]
    public async Task AcbWithoutParent_UsesFallbackDirectoryForCommit()
    {
        var fallback = new StubAwbDirectory("fallback");
        using var acb = new StubAwbFile("elsewhere/song.acb", []);
        using var picked = new StubAwbFile("song.awb", GoodAwb);

        var result = await ExternalAwbImporter.ImportAsync(
            acb,
            "song.awb",
            fallback,
            Callbacks(_ => picked),
            VerifyGoodOnly,
            new InMemoryTemporaryFolderProvider());

        Assert.NotNull(result);
        Assert.Equal(AwbImportAction.CommitNew, result.Action);
        Assert.Contains(fallback.ChildFiles, file => file.FileName.Equals("song.awb"));
    }

    private static ExternalAwbImportCallbacks Callbacks(
        Func<string, ISimpleFile?> pickExternalAwb,
        Func<AwbReplaceCandidate, bool>? confirmReplace = null) =>
        new(
            (expectedName, _) => Task.FromResult(pickExternalAwb(expectedName)),
            (candidate, _) => Task.FromResult(confirmReplace?.Invoke(candidate) ?? true));

    private static async Task VerifyGoodOnly(Stream _, Stream awbStream, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        await awbStream.CopyToAsync(memory, cancellationToken);
        var text = System.Text.Encoding.ASCII.GetString(memory.ToArray());
        if (!text.StartsWith("GOOD", StringComparison.Ordinal))
            throw new InvalidDataException("The AWB content is not decodable.");
    }
}
