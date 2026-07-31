using System;
using Avalonia.Controls;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Views;

public partial class FumenVisualEditorView : UserControl
{
    public FumenVisualEditorView()
    {
        DataContextChanged += (_, _) =>
        {
            if (DataContext is FumenVisualEditorViewModel vm)
                vm.View = this;
        };
    }
}
