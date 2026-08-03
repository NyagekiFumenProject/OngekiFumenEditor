using Gekimini.Avalonia.Framework.Commands;
using Gekimini.Avalonia.Framework.Dialogs;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Commands.OgkrImpl.FastOpenFumen;

[RegisterSingleton<ICommandHandler>]
public class FastOpenFumenCommandHandler : CommandHandlerBase<FastOpenFumenCommandDefinition>
{
    public override async Task Run(Command command)
    {
        var filePath = command.Tag as string;
        try
        {
            bool opened;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var file = await FileDialogHelper.OpenFileAsync(
                    Lang.FastOpenOgkrFumen,
                    [(".ogkr", Lang.OngekiFumen), (".nyageki", Lang.OngekiFumen)]);
                if (file is null)
                    return;

                opened = await DocumentOpenHelper.TryOpenAsDocument(file);
            }
            else
            {
                if (!File.Exists(filePath))
                    return;

                opened = await DocumentOpenHelper.TryOpenAsDocument(filePath);
            }

            if (!opened)
                await IoC.Get<IDialogManager>().ShowMessageDialog(
                    Lang.CantFastOpenFumen,
                    DialogMessageType.Error);
        }
        catch (Exception exception)
        {
            Log.LogError("Fast open failed.", exception);
            await IoC.Get<IDialogManager>().ShowMessageDialog(
                $"{Lang.CantFastOpenFumen}{exception.Message}",
                DialogMessageType.Error);
        }
    }
}
