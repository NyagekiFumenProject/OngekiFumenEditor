using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Graphics.Drawing;

namespace OngekiFumenEditor.Avalonia.Modules.FumenEditorRenderControlViewer.ViewModels;

public sealed class RenderControlItem : ObservableObject
{
    private readonly IFumenEditorDrawingTarget target;

    public RenderControlItem(IFumenEditorDrawingTarget target)
    {
        this.target = target;
        Name = target.GetType().Name.TrimEnd("DrawingTarget").TrimEnd("DrawTarget");
    }

    public string Name { get; }
    public IFumenEditorDrawingTarget Target => target;

    public bool IsDesignEnable
    {
        get => target.Visible.HasFlag(DrawingVisible.Design);
        set
        {
            if (value)
                target.Visible |= DrawingVisible.Design;
            else
                target.Visible &= ~DrawingVisible.Design;
            OnPropertyChanged();
        }
    }

    public bool IsPreviewEnable
    {
        get => target.Visible.HasFlag(DrawingVisible.Preview);
        set
        {
            if (value)
                target.Visible |= DrawingVisible.Preview;
            else
                target.Visible &= ~DrawingVisible.Preview;
            OnPropertyChanged();
        }
    }

    public int RenderOrder
    {
        get => target.CurrentRenderOrder;
        set
        {
            target.CurrentRenderOrder = value;
            OnPropertyChanged();
        }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(IsDesignEnable));
        OnPropertyChanged(nameof(IsPreviewEnable));
        OnPropertyChanged(nameof(RenderOrder));
    }

    public override string ToString() => $"[{target.Visible}] : {Name}";
}
