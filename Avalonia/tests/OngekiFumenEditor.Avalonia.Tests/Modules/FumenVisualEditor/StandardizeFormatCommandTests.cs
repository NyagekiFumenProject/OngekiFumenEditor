using Avalonia.Headless.XUnit;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Modules.Dialogs.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.StandardizeFormat;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Utils.SimpleFileSystem;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Modules.FumenVisualEditor;

public sealed class StandardizeFormatCommandTests
{
    [AvaloniaFact]
    public async Task PickerCancelled_DoesNotRunConversion()
    {
        var editor = CreateEditor();
        var convertService = new StubConvertService();
        var outputService = new StubOutputService();
        var dialogManager = new StubDialogManager();
        var handler = CreateHandler(editor, dialogManager, convertService, outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        Assert.Equal(1, outputService.PickCallCount);
        Assert.Equal(0, convertService.CallCount);
        Assert.Empty(dialogManager.Messages);
        Assert.Equal(0, dialogManager.ConfirmCallCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task PickerFailure_ShowsErrorWithoutLockingEditor()
    {
        var editor = CreateEditor();
        var convertService = new StubConvertService();
        var outputService = new StubOutputService
        {
            PickFailure = new IOException("picker failed")
        };
        var dialogManager = new StubDialogManager();
        var handler = CreateHandler(editor, dialogManager, convertService, outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Contains("picker failed", message.Content, StringComparison.Ordinal);
        Assert.Equal(DialogMessageType.Error, message.Type);
        Assert.Equal(0, convertService.CallCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task SuccessfulConversion_UsesStandardizedInMemoryOptionAndRevealsAfterConfirmation()
    {
        var editor = CreateEditor();
        var outputFile = new StubSimpleFile("standardized.ogkr");
        var convertService = new StubConvertService
        {
            LockProbe = () => editor.IsLocked,
            Result = new FumenConverterWrapper.GenerateResult(true)
        };
        var outputService = new StubOutputService
        {
            OutputFile = outputFile,
            CanReveal = true,
            RevealResult = true
        };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = CreateHandler(editor, dialogManager, convertService, outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        Assert.Equal(1, convertService.CallCount);
        Assert.True(convertService.WasLockedDuringCall);
        Assert.Same(editor.EditorContext.Fumen, convertService.LastFumen);
        Assert.True(convertService.LastOption!.IsStandarizeFumen);
        Assert.Same(outputFile, convertService.LastOption.OutputFumenFile);
        Assert.Null(convertService.LastOption.InputFumenFile);
        Assert.Equal(1, dialogManager.ConfirmCallCount);
        Assert.Equal(Lang.NewFumenFileSaveDone, dialogManager.LastConfirmContent);
        Assert.Equal(Lang.StandardizeFormat, dialogManager.LastConfirmTitle);
        Assert.Equal(1, outputService.RevealCallCount);
        Assert.Empty(dialogManager.Messages);
        Assert.True(outputFile.IsDisposed);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task FailedConversion_ShowsErrorWithoutRevealPrompt()
    {
        var editor = CreateEditor();
        var outputFile = new StubSimpleFile("standardized.ogkr");
        var convertService = new StubConvertService
        {
            Result = new FumenConverterWrapper.GenerateResult(false, "validation failed")
        };
        var outputService = new StubOutputService
        {
            OutputFile = outputFile,
            CanReveal = true
        };
        var dialogManager = new StubDialogManager();
        var handler = CreateHandler(editor, dialogManager, convertService, outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Equal("validation failed", message.Content);
        Assert.Equal(DialogMessageType.Error, message.Type);
        Assert.Equal(0, dialogManager.ConfirmCallCount);
        Assert.Equal(0, outputService.RevealCallCount);
        Assert.True(outputFile.IsDisposed);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task ConversionException_UnlocksEditorAndShowsError()
    {
        var editor = CreateEditor();
        var outputFile = new StubSimpleFile("standardized.ogkr");
        var convertService = new StubConvertService
        {
            LockProbe = () => editor.IsLocked,
            Failure = new IOException("write failed")
        };
        var outputService = new StubOutputService
        {
            OutputFile = outputFile,
            CanReveal = true
        };
        var dialogManager = new StubDialogManager();
        var handler = CreateHandler(editor, dialogManager, convertService, outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Contains("write failed", message.Content, StringComparison.Ordinal);
        Assert.Equal(DialogMessageType.Error, message.Type);
        Assert.True(convertService.WasLockedDuringCall);
        Assert.Equal(0, outputService.RevealCallCount);
        Assert.True(outputFile.IsDisposed);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task SuccessfulConversionWithoutRevealCapability_ShowsCompletionMessage()
    {
        var editor = CreateEditor();
        var outputService = new StubOutputService
        {
            OutputFile = new StubSimpleFile("standardized.ogkr"),
            CanReveal = false
        };
        var dialogManager = new StubDialogManager();
        var handler = CreateHandler(editor, dialogManager, new StubConvertService(), outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Equal(Lang.ConvertSuccess, message.Content);
        Assert.Equal(DialogMessageType.Info, message.Type);
        Assert.Equal(0, dialogManager.ConfirmCallCount);
        Assert.Equal(0, outputService.RevealCallCount);
    }

    [AvaloniaFact]
    public async Task RevealFailure_ShowsSavedButNotOpenedError()
    {
        var editor = CreateEditor();
        var outputService = new StubOutputService
        {
            OutputFile = new StubSimpleFile("standardized.ogkr"),
            CanReveal = true,
            RevealResult = false
        };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = CreateHandler(editor, dialogManager, new StubConvertService(), outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Equal(Lang.OpenOutputFolderFailed, message.Content);
        Assert.Equal(DialogMessageType.Error, message.Type);
        Assert.Equal(1, outputService.RevealCallCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task RevealException_ShowsSavedButNotOpenedError()
    {
        var editor = CreateEditor();
        var outputService = new StubOutputService
        {
            OutputFile = new StubSimpleFile("standardized.ogkr"),
            CanReveal = true,
            RevealFailure = new IOException("launcher failed")
        };
        var dialogManager = new StubDialogManager { ConfirmResult = true };
        var handler = CreateHandler(editor, dialogManager, new StubConvertService(), outputService);

        await handler.Run(new Command(new StandardizeFormatCommandDefinition()));

        var message = Assert.Single(dialogManager.Messages);
        Assert.Equal(Lang.OpenOutputFolderFailed, message.Content);
        Assert.Equal(DialogMessageType.Error, message.Type);
        Assert.Equal(1, outputService.RevealCallCount);
        Assert.False(editor.IsLocked);
    }

    [AvaloniaFact]
    public async Task Update_OnlyEnablesWithAnActiveFumen()
    {
        var editorManager = new StubEditorDocumentManager();
        var handler = new StandardizeFormatCommandHandler(
            editorManager,
            new StubDialogManager(),
            new StubConvertService(),
            new StubOutputService());
        var command = new Command(new StandardizeFormatCommandDefinition());

        await handler.Update(command);
        Assert.False(command.Enabled);

        editorManager.Current = CreateEditor();
        await handler.Update(command);
        Assert.True(command.Enabled);
    }

    [Theory]
    [InlineData("C:\\charts\\standardized.ogkr", "C:\\charts")]
    [InlineData(null, null)]
    public void TryGetOutputDirectory_UsesOnlyLocalPaths(string? localPath, string? expectedDirectory)
    {
        var outputFile = new StubSimpleFile("standardized.ogkr", localPath);

        var result = StandardizeFormatOutputService.TryGetOutputDirectory(outputFile, out var outputDirectory);

        Assert.Equal(expectedDirectory is not null, result);
        Assert.Equal(expectedDirectory, outputDirectory);
    }

    private static StandardizeFormatCommandHandler CreateHandler(
        FumenVisualEditorViewModel editor,
        StubDialogManager dialogManager,
        StubConvertService convertService,
        StubOutputService outputService) =>
        new(
            new StubEditorDocumentManager { Current = editor },
            dialogManager,
            convertService,
            outputService);

    private static FumenVisualEditorViewModel CreateEditor() =>
        new(Microsoft.Extensions.Logging.Abstractions.NullLogger<FumenVisualEditorViewModel>.Instance) { EditorContext = new EditorContext { Fumen = new OngekiFumen() } };

    private sealed class StubConvertService : IFumenConvertService
    {
        public FumenConverterWrapper.GenerateResult Result { get; init; } = new(true);

        public Exception? Failure { get; init; }

        public Func<bool>? LockProbe { get; init; }

        public int CallCount { get; private set; }

        public bool WasLockedDuringCall { get; private set; }

        public FumenConvertOption? LastOption { get; private set; }

        public OngekiFumen? LastFumen { get; private set; }

        public Task<FumenConverterWrapper.GenerateResult> GenerateAsync(
            FumenConvertOption option,
            OngekiFumen inMemoryFumen = null!,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastOption = option;
            LastFumen = inMemoryFumen;
            WasLockedDuringCall = LockProbe?.Invoke() ?? false;
            return Failure is null
                ? Task.FromResult(Result)
                : Task.FromException<FumenConverterWrapper.GenerateResult>(Failure);
        }
    }

    private sealed class StubOutputService : IStandardizeFormatOutputService
    {
        public ISimpleFile? OutputFile { get; init; }

        public Exception? PickFailure { get; init; }

        public bool CanReveal { get; init; }

        public bool RevealResult { get; init; } = true;

        public Exception? RevealFailure { get; init; }

        public int PickCallCount { get; private set; }

        public int RevealCallCount { get; private set; }

        public Task<ISimpleFile> PickOutputFileAsync()
        {
            PickCallCount++;
            return PickFailure is null
                ? Task.FromResult(OutputFile!)
                : Task.FromException<ISimpleFile>(PickFailure);
        }

        public bool CanRevealOutputDirectory(ISimpleFile outputFile) => CanReveal;

        public Task<bool> RevealOutputDirectoryAsync(ISimpleFile outputFile)
        {
            RevealCallCount++;
            return RevealFailure is null
                ? Task.FromResult(RevealResult)
                : Task.FromException<bool>(RevealFailure);
        }
    }

    private sealed class StubSimpleFile(string fileName, string? localPath = null) : ISimpleFile
    {
        public ISimpleDirectory? ParentDictionary => null;

        public string FullPath => localPath ?? fileName;

        public string? LocalPath => localPath;

        public string FileName => fileName;

        public long FileLength => 0;

        public bool IsDisposed { get; private set; }

        public ValueTask<string[]> ReadAllLines() => ValueTask.FromResult(Array.Empty<string>());

        public ValueTask<byte[]> ReadAllBytes() => ValueTask.FromResult(Array.Empty<byte>());

        public Task<Stream> OpenRead() => Task.FromResult<Stream>(new MemoryStream());

        public Task<Stream> OpenWrite() => Task.FromResult<Stream>(new MemoryStream());

        public void Dispose() => IsDisposed = true;
    }

    private sealed class StubEditorDocumentManager : IEditorDocumentManager
    {
        private FumenVisualEditorViewModel? current;

        public FumenVisualEditorViewModel Current
        {
            get => current!;
            set => current = value;
        }

        public FumenVisualEditorViewModel CurrentActivatedEditor => current!;

        public event IEditorDocumentManager.NotifyCreateFunc OnNotifyCreated
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.ActivateEditorChangedFunc OnActivateEditorChanged
        {
            add { }
            remove { }
        }

        public event IEditorDocumentManager.NotifyDestoryFunc OnNotifyDestoryed
        {
            add { }
            remove { }
        }

        public IEnumerable<FumenVisualEditorViewModel> GetCurrentEditors() =>
            current is null ? [] : [current];

        public void NotifyActivate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDeactivate(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }

        public void NotifyCreate(FumenVisualEditorViewModel editor) => current = editor;

        public void NotifyDestory(FumenVisualEditorViewModel editor)
        {
            if (ReferenceEquals(current, editor))
                current = null;
        }
    }

    private sealed class StubDialogManager : IDialogManager
    {
        public bool ConfirmResult { get; init; }

        public int ConfirmCallCount { get; private set; }

        public string? LastConfirmContent { get; private set; }

        public string? LastConfirmTitle { get; private set; }

        public List<(string Content, DialogMessageType Type)> Messages { get; } = [];

        public Task<T> ShowDialog<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : DialogViewModelBase => throw new NotSupportedException();

        public Task ShowDialog(DialogViewModelBase dialogViewModel) => throw new NotSupportedException();

        public Task ShowMessageDialog(string content, DialogMessageType messageType = DialogMessageType.Info)
        {
            Messages.Add((content, messageType));
            return Task.CompletedTask;
        }

        public Task<bool> ShowComfirmDialog(
            string content,
            string? title = null,
            string? yesButtonContent = null,
            string? noButtonContent = null)
        {
            ConfirmCallCount++;
            LastConfirmContent = content;
            LastConfirmTitle = title;
            return Task.FromResult(ConfirmResult);
        }
    }
}
