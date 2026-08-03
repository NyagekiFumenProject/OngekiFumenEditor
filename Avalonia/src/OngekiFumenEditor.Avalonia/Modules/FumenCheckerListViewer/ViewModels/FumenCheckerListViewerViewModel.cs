using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.Tools;
using Gekimini.Avalonia.Utils.MethodExtensions;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.Base;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia;

namespace OngekiFumenEditor.Avalonia.Modules.FumenCheckerListViewer.ViewModels;

[RegisterSingleton<IFumenCheckerListViewer>]
public partial class FumenCheckerListViewerViewModel : ToolViewModelBase, IFumenCheckerListViewer
{
    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly List<IFumenCheckRule> checkRules;
    private readonly List<ICheckResult> allCheckResults = [];

    public ObservableCollection<ICheckResult> CheckResults { get; } = [];

    public int ErrorCount => allCheckResults.Count(x => x.Severity == RuleSeverity.Error);
    public int ProblemCount => allCheckResults.Count(x => x.Severity == RuleSeverity.Problem);
    public int SuggestCount => allCheckResults.Count(x => x.Severity == RuleSeverity.Suggest);
    public bool IsEnable => Editor?.Fumen is not null;

    public bool EnableShowError
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                RefreshFilter();
        }
    } = true;

    public bool EnableShowProblem
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                RefreshFilter();
        }
    } = true;

    public bool EnableShowSuggest
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
                RefreshFilter();
        }
    } = true;

    public FumenVisualEditorViewModel Editor
    {
        get => field;
        set
        {
            this.RegisterOrUnregisterPropertyChangeEvent(field, value, OnEditorPropChanged);
            if (SetProperty(ref field, value))
            {
                RefreshCurrentFumen();
                OnPropertyChanged(nameof(IsEnable));
            }
        }
    }

    public FumenCheckerListViewerViewModel() : base(Lang.B.FumenCheckerListViewer.ToLocalizedString())
    {
        Dock = DockMode.Bottom;
        editorDocumentManager = IoC.Get<IEditorDocumentManager>();
        checkRules = IoC.GetAll<IFumenCheckRule>().ToList();

        editorDocumentManager.OnActivateEditorChanged += OnActivateEditorChanged;
        Editor = editorDocumentManager.CurrentActivatedEditor;
    }

    private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
    {
        Editor = @new;
    }

    private void OnEditorPropChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FumenVisualEditorViewModel.Fumen))
        {
            RefreshCurrentFumen();
            OnPropertyChanged(nameof(IsEnable));
        }
    }

    [RelayCommand]
    private void NavigateToResult(ICheckResult checkResult)
    {
        checkResult?.NavigateBehavior?.Navigate(Editor);
    }

    [RelayCommand]
    public void RefreshCurrentFumen()
    {
        allCheckResults.Clear();

        try
        {
            if (Editor?.Fumen is not null)
            {
                foreach (var checkResult in checkRules.SelectMany(x => x.CheckRule(Editor.Fumen, Editor)))
                    allCheckResults.Add(checkResult);
            }
        }
        catch (Exception e)
        {
            allCheckResults.Clear();
            Log.LogError("FumenCheckerListViewer can't refresh checkers", e);
        }

        RefreshFilter();
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(ProblemCount));
        OnPropertyChanged(nameof(SuggestCount));
    }

    public void RefreshFilter()
    {
        CheckResults.Clear();
        foreach (var result in allCheckResults.Where(OnCheckResultsFilter))
            CheckResults.Add(result);
    }

    private bool OnCheckResultsFilter(ICheckResult checkResult)
    {
        return checkResult.Severity switch
        {
            RuleSeverity.Suggest => EnableShowSuggest,
            RuleSeverity.Problem => EnableShowProblem,
            RuleSeverity.Error => EnableShowError,
            _ => false
        };
    }
}

