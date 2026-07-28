using Gekimini.Avalonia.Platforms.Services.Window;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels.Dialogs;
using OngekiFumenEditor.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.ViewModels;

public class BulletPalleteTypeUIViewModel : CommonUIViewModelBase<BulletPallete>
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

        var pallete = editor.Fumen.BulletPalleteList
            .FirstOrDefault(x => x.StrID.Equals(strId, StringComparison.CurrentCultureIgnoreCase));
        if (pallete is null)
            return;

        TypedProxyValue = pallete;
        OnPropertyChanged(nameof(StrId));
    }

    public async void OpenSelectList()
    {
        var editor = IoC.Get<IEditorDocumentManager>()?.CurrentActivatedEditor;
        if (editor is null)
            return;

        var bplList = editor.Fumen.BulletPalleteList.Prepend(BulletPallete.DummyCustomPallete).ToArray();
        var dialog = new BulletPalleteSelectDialogViewModel(bplList, TypedProxyValue);
        await IoC.Get<IWindowManager>().ShowDialogAsync(dialog);
        if (dialog.SelectedPallete is not null)
            TypedProxyValue = dialog.SelectedPallete;

        OnPropertyChanged(nameof(StrId));
    }

    public void SetNull()
    {
        var rollback = TypedProxyValue;
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
