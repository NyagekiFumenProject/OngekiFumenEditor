using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework.Documents;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Modules.Shell;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Gekimini.Avalonia.Views;
using Injectio.Attributes;
using Microsoft.Extensions.Logging;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Base;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser.ViewModels;

[RegisterSingleton<IFumenMetaInfoBrowser>]
public partial class FumenMetaInfoBrowserViewModel : ToolViewModelBase, IFumenMetaInfoBrowser
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo> FumenPropertyCache = new();

    private INotifyPropertyChanged observedDocument;
    private OngekiFumen fumen;
    private string errorMessage;
    private OngekiFumenModelProxy fumenProxy;
    private IShell Shell => OngekiFumenEditor.Avalonia.IoC.Get<IShell>();
    private ILogger<FumenMetaInfoBrowserViewModel> Logger => OngekiFumenEditor.Avalonia.IoC.Get<ILogger<FumenMetaInfoBrowserViewModel>>();

    public FumenMetaInfoBrowserViewModel() : base(Lang.B.FumenMetaInfoBrowser.ToLocalizedString())
    {
        Dock = global::Dock.Model.Core.DockMode.Right;
    }

    public OngekiFumen Fumen
    {
        get => fumen;
        set
        {
            if (ReferenceEquals(fumen, value))
                return;

            fumen = value;
            OnPropertyChanged();

            if (fumen is null)
            {
                FumenProxy = null;
                ErrorMessage = "Open a Fumen editor document before using this tool.";
            }
            else
            {
                ErrorMessage = null;
                FumenProxy = new OngekiFumenModelProxy(fumen);
            }
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        set
        {
            if (SetProperty(ref errorMessage, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Logger.LogError("Current error message: {ErrorMessage}", value);
                OnPropertyChanged(nameof(IsErrorVisible));
            }
        }
    }

    public bool IsErrorVisible => !string.IsNullOrWhiteSpace(ErrorMessage);

    public OngekiFumenModelProxy FumenProxy
    {
        get => fumenProxy;
        set => SetProperty(ref fumenProxy, value);
    }

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);

        Shell.ActiveDocumentChanged += OnActiveDocumentChanged;
        AttachDocument(Shell.ActiveDocument);
    }

    public override void OnViewBeforeUnload(IView view)
    {
        base.OnViewBeforeUnload(view);

        Shell.ActiveDocumentChanged -= OnActiveDocumentChanged;
        AttachDocument(null);
    }

    private void OnActiveDocumentChanged(object sender, IDocumentViewModel document)
    {
        AttachDocument(document);
    }

    private void AttachDocument(IDocumentViewModel document)
    {
        if (observedDocument is not null)
            observedDocument.PropertyChanged -= OnActiveDocumentPropertyChanged;

        observedDocument = document as INotifyPropertyChanged;

        if (observedDocument is not null)
            observedDocument.PropertyChanged += OnActiveDocumentPropertyChanged;

        Fumen = ExtractFumen(document);
    }

    private void OnActiveDocumentPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var property = GetFumenProperty(sender.GetType());
        if (property is not null && e.PropertyName == property.Name)
            Fumen = property.GetValue(sender) as OngekiFumen;
    }

    private static OngekiFumen ExtractFumen(IDocumentViewModel document)
    {
        if (document is null)
            return null;

        var property = GetFumenProperty(document.GetType());
        return property?.GetValue(document) as OngekiFumen;
    }

    private static PropertyInfo GetFumenProperty(Type documentType)
    {
        if (documentType is null)
            return null;

        return FumenPropertyCache.GetOrAdd(documentType, static type =>
            type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(x => typeof(OngekiFumen).IsAssignableFrom(x.PropertyType) && x.CanRead));
    }
}

