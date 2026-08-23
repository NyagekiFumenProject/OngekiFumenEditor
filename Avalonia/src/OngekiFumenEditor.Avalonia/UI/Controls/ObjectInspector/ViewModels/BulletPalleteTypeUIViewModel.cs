using CommunityToolkit.Mvvm.Input;
using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public partial class BulletPalleteTypeUIViewModel : CommonUIViewModelBase<BulletPallete>
{
    private string cacheStrId = string.Empty;

    public object StrId
    {
        get
        {
            var val = ProxyValue;
            if (val is BulletPallete pallete)
                return pallete.StrID;
            return cacheStrId;
        }
        set
        {
            var str = value?.ToString()?.Trim() ?? string.Empty;
            cacheStrId = str;
            TryApplyValue(str);
            OnPropertyChanged(nameof(StrId));
        }
    }

    public BulletPalleteTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
    {
    }

    private void TryApplyValue(string strId)
    {
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        if (editor is null)
            return;

        var pallete = editor.EditorContext.Fumen.BulletPalleteList
            .FirstOrDefault(x => x.StrID.Equals(strId, StringComparison.CurrentCultureIgnoreCase));
        if (pallete is null)
            return;

        TypedProxyValue = pallete;
        OnPropertyChanged(nameof(StrId));
    }

    [RelayCommand]
    private async Task OpenSelectListAsync()
    {
        Log.LogInfo("OpenSelectListAsync triggered.");
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        if (editor is null)
            return;

        var bplList = editor.EditorContext.Fumen.BulletPalleteList.Prepend(BulletPallete.DummyCustomPallete).ToArray();
        await OpenSelectListCoreAsync(bplList, IoC.Get<IWindowManager>());
    }

    internal async Task<bool> OpenSelectListCoreAsync(
        IEnumerable<BulletPallete> bulletPalleteList,
        IWindowManager windowManager)
    {
        var dialog = new BulletPalleteSelectDialogViewModel(bulletPalleteList, TypedProxyValue);
        var result = await windowManager.ShowDialogAsync(dialog);
        var selectedPallete = dialog.SelectedPallete;
        if (result != true || selectedPallete is null || ReferenceEquals(selectedPallete, TypedProxyValue))
            return false;

        TypedProxyValue = selectedPallete;
        OnPropertyChanged(nameof(StrId));
        return true;
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
            Log.LogError($"Can't set null for prop {PropertyInfo.DisplayPropertyName}: {e.Message}");
            TypedProxyValue = rollback;
        }
    }
}
