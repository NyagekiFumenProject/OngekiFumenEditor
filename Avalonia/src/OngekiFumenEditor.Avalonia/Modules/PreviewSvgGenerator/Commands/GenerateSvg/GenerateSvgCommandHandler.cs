using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Modules.Shell;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Kernel;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator.Commands.GenerateSvg;

[RegisterSingleton<ICommandHandler>]
public partial class GenerateSvgCommandHandler : CommandHandlerBase<GenerateSvgCommandDefinition>
{
    private IShell Shell => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IShell>();
    private IPreviewSvgGenerator PreviewSvgGenerator => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IPreviewSvgGenerator>();
    private IDialogManager DialogManager => OngekiFumenEditor.Avalonia.Avalonia.IoC.Get<IDialogManager>();

    public override void Update(Command command)
    {
        base.Update(command);
        command.Enabled = TryGetActiveFumen(Shell.ActiveDocument, out _, out _);
    }

    public override async Task Run(Command command)
    {
        if (!TryGetActiveFumen(Shell.ActiveDocument, out var fumen, out var duration))
        {
            await DialogManager.ShowMessageDialog("No active fumen document found.", DialogMessageType.Warning);
            return;
        }

        try
        {
            var opt = new SvgGenerateOption
            {
                Duration = duration,
                OutputFilePath = Path.GetTempFileName() + ".svg"
            };

            await PreviewSvgGenerator.GenerateSvgAsync(fumen, opt);
            await DialogManager.ShowMessageDialog($"SVG generated: {opt.OutputFilePath}");
        }
        catch (Exception e)
        {
            await DialogManager.ShowMessageDialog($"Generate SVG failed: {e.Message}", DialogMessageType.Error);
        }
    }

    private static bool TryGetActiveFumen(IDocumentViewModel document, out OngekiFumen fumen, out TimeSpan duration)
    {
        fumen = null;
        duration = TimeSpan.Zero;

        if (document is null)
            return false;

        var docType = document.GetType();
        var fumenProp = docType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(x => typeof(OngekiFumen).IsAssignableFrom(x.PropertyType) && x.CanRead);

        fumen = fumenProp?.GetValue(document) as OngekiFumen;
        if (fumen is null)
            return false;

        var projectDataProp = docType.GetProperty("EditorProjectData", BindingFlags.Instance | BindingFlags.Public);
        if (projectDataProp?.GetValue(document) is { } projectData)
        {
            var audioDurationProp = projectData.GetType().GetProperty("AudioDuration", BindingFlags.Instance | BindingFlags.Public);
            if (audioDurationProp?.GetValue(projectData) is TimeSpan value)
                duration = value;
        }

        return true;
    }
}

