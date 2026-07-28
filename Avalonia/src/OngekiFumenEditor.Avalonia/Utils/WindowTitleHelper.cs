using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Utils;

[RegisterSingleton]
public class WindowTitleHelper
{
    private string titlePrefix = "Ongeki Fumen Editor";
    public string TitlePrefix
    {
        get => titlePrefix;
        set
        {
            titlePrefix = value;
            UpdateWindowTitle();
        }
    }

    private string titleSuffix = string.Empty;
    public string TitleSuffix
    {
        get => titleSuffix;
        set
        {
            titleSuffix = value;
            UpdateWindowTitle();
        }
    }

    private string titleContent = string.Empty;
    public string TitleContent
    {
        get => titleContent;
        set
        {
            titleContent = value;
            UpdateWindowTitle();
        }
    }

    public string ActualFormattedWindowTitle { get; private set; } = "Ongeki Fumen Editor";

    public void UpdateWindowTitle()
    {
        ActualFormattedWindowTitle = TitlePrefix + TitleContent + TitleSuffix;
    }

    public void UpdateWindowTitleByEditor(FumenVisualEditorViewModel editor)
    {
        TitleContent = editor is not null ? $" - {editor.DisplayName} " : string.Empty;
    }
}
