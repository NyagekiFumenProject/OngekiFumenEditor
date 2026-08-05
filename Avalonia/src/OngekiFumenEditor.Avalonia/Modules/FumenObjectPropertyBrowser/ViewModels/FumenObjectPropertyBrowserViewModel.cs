using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Avalonia.Utils;
using System.Collections.ObjectModel;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Modules.FumenObjectPropertyBrowser.ViewModels;

[RegisterSingleton<IFumenObjectPropertyBrowser>]
public class FumenObjectPropertyBrowserViewModel : ToolViewModelBase, IFumenObjectPropertyBrowser
{
    private IEditorDocumentManager EditorDocumentManager => IoC.Get<IEditorDocumentManager>();

    private HashSet<ISelectableObject> selectedObjects = [];
    public IReadOnlySet<ISelectableObject> SelectedObjects => selectedObjects;

    private FumenVisualEditorViewModel referenceEditor;
    private HashSet<Type> supportTypes = [];

    public ObservableCollection<IObjectPropertyAccessProxy> PropertyInfoWrappers { get; } = [];
    public FumenVisualEditorViewModel Editor => referenceEditor;

    public FumenObjectPropertyBrowserViewModel() : base(Lang.B.ObjectProperty.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Right;

        UpdateDisplayName();
        supportTypes = IoC.GetAll<ITypeUIGenerator>().SelectMany(x => x.SupportTypes).ToHashSet();

        EditorDocumentManager.OnNotifyDestoryed += OnEditorDestoryed;
    }

    private void OnEditorDestoryed(FumenVisualEditorViewModel sender)
    {
        if (sender == referenceEditor)
            RefreshSelected(null);
    }

    private void OnObjectChanged()
    {
        foreach (var wrapper in PropertyInfoWrappers)
            wrapper.Clear();
        PropertyInfoWrappers.Clear();

        if (SelectedObjects.Count == 0)
            return;

        var genericProperties = SelectedObjects
            .Select(x => x.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(prop =>
                {
                    var type = prop.PropertyType;
                    return supportTypes.Contains(type) || type.IsEnum;
                }))
            .SelectMany(x => x)
            .GroupBy(x => (x.PropertyType, x.Name))
            .Where(x => x.Count() == SelectedObjects.Count)
            .Select(x => x.First())
            .Select(x => (x.PropertyType, x.Name, x))
            .ToArray();

        var wrappers = new List<IObjectPropertyAccessProxy>();
        foreach ((var propType, var propName, var refPropInfo) in genericProperties)
        {
            IObjectPropertyAccessProxy wrapper = null;
            if (SelectedObjects.Count > 1)
            {
                if (MultiObjectsPropertyInfoWrapper.TryCreate(propName, propType, selectedObjects, out var multi))
                    wrapper = new UndoableMultiObjectPropertyInfoWrapper(multi, referenceEditor);
            }
            else
            {
                if (!refPropInfo.CanWrite && refPropInfo.GetCustomAttribute<ObjectPropertyBrowserShow>() is null)
                    continue;
                if (refPropInfo.GetCustomAttribute<ObjectPropertyBrowserHide>() is not null)
                    continue;

                wrapper = new UndoablePropertyInfoWrapper(new PropertyInfoWrapper(refPropInfo, SelectedObjects.FirstOrDefault()), referenceEditor);
            }

            if (wrapper is not null)
                wrappers.Add(wrapper);
        }

        foreach (var wrapper in wrappers.OrderBy(x => x.DisplayPropertyName))
            PropertyInfoWrappers.Add(wrapper);

        UpdateDisplayName();
    }

    private void UpdateDisplayName()
    {
        var singleObj = selectedObjects.Count == 1 ? selectedObjects.First() : null;
        Title = LocalizedString.CreateFromTemplateFunc(() =>
            Lang.ObjectProperty + (singleObj is null ? string.Empty : $" - {((OngekiObjectBase)singleObj).Name}"));
    }

    public void RefreshSelected(IEnumerable<ISelectableObject> objects, FumenVisualEditorViewModel referenceEditor)
    {
        selectedObjects.Clear();
        foreach (var o in objects ?? [])
            selectedObjects.Add(o);

        this.referenceEditor = referenceEditor;
        OnObjectChanged();
        referenceEditor?.NotifyOfPropertyChange(nameof(FumenVisualEditorViewModel.SelectObjects));
        OnPropertyChanged(nameof(SelectedObjects));
        UpdateDisplayName();
    }

    public void RefreshSelected(FumenVisualEditorViewModel referenceEditor, params object[] ongekiObj)
    {
        RefreshSelected(ongekiObj?.OfType<ISelectableObject>() ?? [], referenceEditor);
    }

    public void RefreshSelected(FumenVisualEditorViewModel referenceEditor)
    {
        RefreshSelected(referenceEditor?.SelectObjects ?? [], referenceEditor);
    }
}

