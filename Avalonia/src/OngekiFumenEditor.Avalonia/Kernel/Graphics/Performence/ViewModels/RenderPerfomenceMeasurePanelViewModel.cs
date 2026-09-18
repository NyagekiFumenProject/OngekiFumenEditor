using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Gekimini.Avalonia.Modules.Window.ViewModels;
using Gekimini.Avalonia.Views;
using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Kernel;

namespace OngekiFumenEditor.Avalonia.Kernel.Graphics.Performence.ViewModels;

[RegisterTransient<RenderPerfomenceMeasurePanelViewModel>]
public sealed class RenderPerfomenceMeasurePanelViewModel : WindowViewModelBase
{
    private readonly IRenderManagerImpl[] renderManagers;
    private readonly IEditorDocumentManager editorDocumentManager;
    private readonly DispatcherTimer refreshTimer;

    public ObservableCollection<RenderPerfomenceMeasureItem> Items { get; } = [];
    public bool HasRenderContexts => Items.Count != 0;
    public bool HasNoRenderContexts => !HasRenderContexts;

    public RenderPerfomenceMeasurePanelViewModel(IEnumerable<IRenderManagerImpl> renderManagers,
        IEditorDocumentManager editorDocumentManager)
    {
        this.renderManagers = renderManagers.ToArray();
        this.editorDocumentManager = editorDocumentManager;
        refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        refreshTimer.Tick += (_, _) => RefreshPanel();
    }

    public override void OnViewAfterLoaded(IView view)
    {
        base.OnViewAfterLoaded(view);
        RefreshPanel();
        refreshTimer.Start();
    }

    public override void OnViewBeforeUnload(IView view)
    {
        refreshTimer.Stop();
        Items.Clear();
        NotifyContextVisibility();
        base.OnViewBeforeUnload(view);
    }

    private void RefreshPanel()
    {
        var editors = editorDocumentManager.GetCurrentEditors().ToArray();
        var liveContexts = new HashSet<IRenderContext>(ReferenceEqualityComparer.Instance);
        var targetIndex = 0;
        foreach (var manager in renderManagers)
        {
            foreach (var context in manager.GetRenderContexts())
            {
                if (!liveContexts.Add(context))
                    continue;
                var existingIndex = -1;
                for (var i = targetIndex; i < Items.Count; i++)
                {
                    if (ReferenceEquals(Items[i].Context, context))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex < 0)
                    Items.Insert(targetIndex, new RenderPerfomenceMeasureItem(context));
                else if (existingIndex != targetIndex)
                    Items.Move(existingIndex, targetIndex);

                var editor = editors.FirstOrDefault(x => ReferenceEquals(x.RenderContext, context));
                Items[targetIndex++].Refresh(manager.Name, editor?.DisplayName);
            }
        }

        while (Items.Count > targetIndex)
            Items.RemoveAt(Items.Count - 1);
        NotifyContextVisibility();
    }

    private void NotifyContextVisibility()
    {
        OnPropertyChanged(nameof(HasRenderContexts));
        OnPropertyChanged(nameof(HasNoRenderContexts));
    }
}

public sealed record PerfomenceMonitorOption(Type MonitorType, Func<IPerfomenceMonitor> CreateMonitor)
{
    public string Name => MonitorType.Name;
}

public sealed class RenderPerfomenceMeasureItem : ObservableObject
{
    private readonly StringBuilder builder = new();
    private string headerTitle;
    private string headerContext;
    private string headerMonitor;
    private string statisticsText;
    private PerfomenceMonitorOption selectedPerfomenceMonitorOption;

    public IRenderContext Context { get; }
    public IReadOnlyList<PerfomenceMonitorOption> PerfomenceMonitorOptions { get; } =
    [
        new(typeof(DummyPerformenceMonitor), static () => DummyPerformenceMonitor.Instance),
        new(typeof(DefaultReleasePerfomenceMonitor), static () => new DefaultReleasePerfomenceMonitor()),
        new(typeof(DefaultDebugPerfomenceMonitor), static () => new DefaultDebugPerfomenceMonitor())
    ];

    public RenderPerfomenceMeasureItem(IRenderContext context) => Context = context;

    public string HeaderTitle { get => headerTitle; private set => SetProperty(ref headerTitle, value); }
    public string HeaderContext { get => headerContext; private set => SetProperty(ref headerContext, value); }
    public string HeaderMonitor { get => headerMonitor; private set => SetProperty(ref headerMonitor, value); }
    public string StatisticsText { get => statisticsText; private set => SetProperty(ref statisticsText, value); }

    public PerfomenceMonitorOption SelectedPerfomenceMonitorOption
    {
        get => selectedPerfomenceMonitorOption;
        set
        {
            if (value is null || ReferenceEquals(value, selectedPerfomenceMonitorOption))
                return;
            Context.PerfomenceMonitor = value.CreateMonitor();
            RefreshStatistics();
        }
    }

    public void Refresh(string managerName, string editorDisplayName)
    {
        var name = string.IsNullOrWhiteSpace(Context.Name) ? Context.GetType().Name : Context.Name;
        HeaderTitle = string.IsNullOrWhiteSpace(editorDisplayName) ? name : editorDisplayName;
        HeaderContext = $"{name} | {managerName} / {Context.GetType().Name} #{RuntimeHelpers.GetHashCode(Context):X}";
        RefreshStatistics();
    }

    private void RefreshStatistics()
    {
        var monitor = Context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;
        var option = PerfomenceMonitorOptions.FirstOrDefault(x => x.MonitorType == monitor.GetType());
        SetProperty(ref selectedPerfomenceMonitorOption, option, nameof(SelectedPerfomenceMonitorOption));
        HeaderMonitor = $"{Lang.RenderPerfomenceMeasurePanelMonitor} {monitor.GetType().Name}";
        builder.Clear();
        monitor.FormatStatistics(builder);
        StatisticsText = builder.Length == 0 ? Lang.RenderPerfomenceMeasurePanelNoData : builder.ToString().TrimEnd();
    }
}
