using Avalonia.Controls;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

internal interface IEditorKeyBindingRouter
{
    void Attach(TopLevel topLevel);

    void Detach();
}
