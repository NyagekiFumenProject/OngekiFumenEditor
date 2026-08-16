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
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();
    private IPreviewSvgGenerator PreviewSvgGenerator => OngekiFumenEditor.Avalonia.IoC.Get<IPreviewSvgGenerator>();
    private IDialogManager DialogManager => OngekiFumenEditor.Avalonia.IoC.Get<IDialogManager>();

    public override Task Update(Command command)
    {
        command.Enabled = TryGetActiveFumen(Shell.ActiveDocument, out _, out _);
        return base.Update(command);
    }

    public override async Task Run(Command command)
    {
        if (!TryGetActiveFumen(Shell.ActiveDocument, out var fumen, out var duration))
        {
            await DialogManager.ShowMessageDialog("No active fumen document found.");
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

        // 文档 ViewModel 不再直接暴露谱面/项目数据，统一从 EditorContext 反射读取。
        var docType = document.GetType();
        var context = docType.GetProperty("EditorContext", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(document);
        if (context is null)
            return false;

        var contextType = context.GetType();
        fumen = contextType.GetProperty("Fumen", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(context) as OngekiFumen;
        if (fumen is null)
            return false;

        if (contextType.GetProperty("ProjectData", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(context) is { } projectData)
        {
            var audioDurationProp = projectData.GetType().GetProperty("AudioDuration", BindingFlags.Instance | BindingFlags.Public);
            if (audioDurationProp?.GetValue(projectData) is TimeSpan value)
                duration = value;
        }

        return true;
    }
}

