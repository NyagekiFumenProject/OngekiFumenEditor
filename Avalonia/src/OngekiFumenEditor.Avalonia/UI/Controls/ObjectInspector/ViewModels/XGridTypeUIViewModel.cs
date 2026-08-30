using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using CommunityToolkit.Mvvm.Input;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class XGridTypeUIViewModel : CommonUIViewModelBase<XGrid>
{
    private object cacheGrid;
    public object Grid
    {
        get
        {
            var val = ProxyValue;
            if (val is XGrid xGrid)
                return xGrid.Grid;
            return cacheGrid;
        }
        set
        {
            if (int.TryParse(value?.ToString(), out var v))
            {
                cacheGrid = v;
                TryApplyValue(v, Unit);
                OnPropertyChanged(nameof(Grid));
            }
        }
    }

    private object cacheUnit;
    public object Unit
    {
        get
        {
            var val = ProxyValue;
            if (val is XGrid xGrid)
                return xGrid.Unit;
            return cacheUnit;
        }
        set
        {
            if (float.TryParse(value?.ToString(), out var v))
            {
                cacheUnit = v;
                TryApplyValue(Grid, v);
                OnPropertyChanged(nameof(Unit));
            }
        }
    }

    private void TryApplyValue(object gridObj, object unitObj)
    {
        if (gridObj is int grid && unitObj is float unit)
            TypedProxyValue = new XGrid(unit, grid);
    }

    public XGridTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    [RelayCommand]
    private void SetNull()
    {
        var rollback = TypedProxyValue;
        Log.LogInfo("SetNull triggered.");
        try
        {
            TypedProxyValue = null;
        }
        catch (Exception e)
        {
            Log.LogError($"Can't set null for prop {PropertyInfo.DisplayPropertyName}: {e.Message}", e);
            TypedProxyValue = rollback;
        }
    }
}
