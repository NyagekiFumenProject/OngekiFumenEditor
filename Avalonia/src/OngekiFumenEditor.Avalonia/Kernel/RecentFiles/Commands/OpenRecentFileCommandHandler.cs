using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Utils;
using System.IO;

namespace OngekiFumenEditor.Avalonia.Kernel.RecentFiles.Commands;

[RegisterSingleton<ICommandHandler>]
public partial class OpenRecentFileCommandHandler : CommandListHandlerBase<OpenRecentFileCommandListDefinition>
{
    private IEditorRecentFilesManager RecentOpenedManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IEditorRecentFilesManager>();
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();
    private IEnumerable<IEditorProvider> EditorProviders => OngekiFumenEditor.Avalonia.Avalonia.IoC.GetAll<IEditorProvider>();

    public override void Populate(Command command, List<Command> commands)
    {
        var recentOpened = RecentOpenedManager.RecentRecordInfos;

        var i = 0;
        foreach (var item in recentOpened)
        {
            i++;
            commands.Add(new Command(command.CommandDefinition)
            {
                Text = $"_{i} {item.DisplayName} ({item.FileName})",
                Tag = item,
                Enabled = File.Exists(item.FileName)
            });
        }
    }

    public override async Task Run(Command command)
    {
        if (command.Tag is not RecentRecordInfo info)
            return;

        if (info.Type == RecentOpenType.CommandOpen)
        {
            _ = await DocumentOpenHelper.TryOpenAsDocument(info.FileName);
            return;
        }

        var provider = PickEditorProvider(info.FileName);
        if (provider is null)
            return;

        var doc = provider.Create();
        var shouldShow = await provider.TryOpen(doc);
        if (shouldShow)
            await Shell.OpenDocumentAsync(doc);
    }

    private IEditorProvider PickEditorProvider(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return EditorProviders.FirstOrDefault(x => x.FileTypes.Any(t =>
            (t.Patterns ?? []).Any(p => p.EndsWith(ext, StringComparison.OrdinalIgnoreCase))));
    }
}
