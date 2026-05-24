using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.Graphics.Performence.ViewModels
{
    [Export(typeof(IRenderPerfomenceMeasurePanel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class RenderPerfomenceMeasurePanelViewModel : WindowBase, IRenderPerfomenceMeasurePanel
    {
        private static readonly string NoPerfomenceDataText = GetResourceString(
            "RenderPerfomenceMeasurePanelNoData",
            "No performance measurement data");

        private readonly DispatcherTimer refreshTimer;
        private bool isInitialized;

        public IReadOnlyList<PerfomenceMonitorOption> PerfomenceMonitorOptions { get; }

        public ObservableCollection<RenderPerfomenceMeasureItem> Items { get; } = new();

        public bool HasRenderContexts => Items.Count > 0;

        public bool HasNoRenderContexts => !HasRenderContexts;

        public RenderPerfomenceMeasurePanelViewModel()
        {
            PerfomenceMonitorOptions = IoC.GetAll<IPerfomenceMonitor>()
                .Select(x => x.GetType())
                .Distinct()
                .OrderBy(x => x.Name)
                .Select(x => new PerfomenceMonitorOption(x))
                .ToArray();

            DisplayName = GetResourceString(
                "RenderPerfomenceMeasurePanelTitle",
                "Render Performance Measurement");

            refreshTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(1)
            };
            refreshTimer.Tick += (_, _) => RefreshPanel();
        }

        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
            InitializePanelOnce();
        }

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            InitializePanelOnce();
            return base.OnActivatedAsync(cancellationToken);
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
                refreshTimer.Stop();

            return base.OnDeactivateAsync(close, cancellationToken);
        }

        private void InitializePanelOnce()
        {
            if (isInitialized)
                return;

            isInitialized = true;

            RefreshPanel();
            refreshTimer.Start();
        }

        private void RefreshPanel()
        {
            SyncRenderContexts();
            RefreshStatistics();
        }

        private void SyncRenderContexts()
        {
            var snapshots = EnumerateRenderContextSnapshots();
            var changed = false;

            for (var i = Items.Count - 1; i >= 0; i--)
            {
                var item = Items[i];
                if (snapshots.Any(x => ReferenceEquals(x.Context, item.Context)))
                    continue;

                Items.RemoveAt(i);
                changed = true;
            }

            for (var targetIndex = 0; targetIndex < snapshots.Count; targetIndex++)
            {
                var snapshot = snapshots[targetIndex];
                var existingIndex = IndexOfItem(snapshot.Context);

                if (existingIndex < 0)
                {
                    Items.Insert(targetIndex, new RenderPerfomenceMeasureItem(
                        snapshot.RenderManagerName,
                        snapshot.Context,
                        PerfomenceMonitorOptions));
                    changed = true;
                    continue;
                }

                var item = Items[existingIndex];
                item.RefreshHeader(snapshot.RenderManagerName);

                if (existingIndex != targetIndex)
                {
                    Items.Move(existingIndex, targetIndex);
                    changed = true;
                }
            }

            if (changed)
            {
                NotifyOfPropertyChange(() => HasRenderContexts);
                NotifyOfPropertyChange(() => HasNoRenderContexts);
            }
        }

        private List<RenderContextSnapshot> EnumerateRenderContextSnapshots()
        {
            var snapshots = new List<RenderContextSnapshot>();

            foreach (var renderManager in IoC.GetAll<IRenderManagerImpl>())
            {
                foreach (var context in renderManager.GetRenderContexts())
                {
                    if (context is null || snapshots.Any(x => ReferenceEquals(x.Context, context)))
                        continue;

                    snapshots.Add(new RenderContextSnapshot(renderManager.Name, context));
                }
            }

            return snapshots;
        }

        private int IndexOfItem(IRenderContext context)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (ReferenceEquals(Items[i].Context, context))
                    return i;
            }

            return -1;
        }

        private void RefreshStatistics()
        {
            foreach (var item in Items)
                item.RefreshStatistics();
        }

        private static string GetResourceString(string key, string fallback)
        {
            return Resources.ResourceManager.GetString(key) ?? fallback;
        }

        private readonly record struct RenderContextSnapshot(string RenderManagerName, IRenderContext Context);

        public sealed class PerfomenceMonitorOption
        {
            public PerfomenceMonitorOption(Type monitorType)
            {
                MonitorType = monitorType;
                Name = monitorType.Name;
            }

            public Type MonitorType { get; }

            public string Name { get; }

            public IPerfomenceMonitor CreateMonitor()
            {
                return IoC.GetAll<IPerfomenceMonitor>().First(x => x.GetType() == MonitorType);
            }
        }

        public sealed class RenderPerfomenceMeasureItem : PropertyChangedBase
        {
            private readonly StringBuilder builder = new();
            private readonly IReadOnlyList<PerfomenceMonitorOption> perfomenceMonitorOptions;
            private string header;
            private string renderManagerName;
            private PerfomenceMonitorOption selectedPerfomenceMonitorOption;
            private string statisticsText = NoPerfomenceDataText;

            public RenderPerfomenceMeasureItem(
                string renderManagerName,
                IRenderContext context,
                IReadOnlyList<PerfomenceMonitorOption> perfomenceMonitorOptions)
            {
                Context = context;
                this.perfomenceMonitorOptions = perfomenceMonitorOptions;
                RefreshHeader(renderManagerName);
            }

            public IRenderContext Context { get; }

            public IReadOnlyList<PerfomenceMonitorOption> PerfomenceMonitorOptions => perfomenceMonitorOptions;

            public PerfomenceMonitorOption SelectedPerfomenceMonitorOption
            {
                get => selectedPerfomenceMonitorOption;
                set
                {
                    if (value is null || ReferenceEquals(value, selectedPerfomenceMonitorOption))
                        return;

                    Context.PerfomenceMonitor = value.CreateMonitor();
                    Set(ref selectedPerfomenceMonitorOption, value);
                    RefreshHeader(renderManagerName);
                    RefreshStatistics();
                }
            }

            public string Header
            {
                get => header;
                private set => Set(ref header, value);
            }

            public void RefreshHeader(string renderManagerName)
            {
                this.renderManagerName = renderManagerName;

                var monitor = Context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;
                RefreshSelectedPerfomenceMonitorOption(monitor.GetType());
                Header = $"{Context.Name} {renderManagerName} / {Context.GetType().Name} #{Context.GetHashCode()} ({monitor.GetType().Name})";
            }

            private void RefreshSelectedPerfomenceMonitorOption(Type monitorType)
            {
                var option = perfomenceMonitorOptions.FirstOrDefault(x => x.MonitorType == monitorType);
                if (!ReferenceEquals(option, selectedPerfomenceMonitorOption))
                    Set(ref selectedPerfomenceMonitorOption, option, nameof(SelectedPerfomenceMonitorOption));
            }

            public string StatisticsText
            {
                get => statisticsText;
                private set => Set(ref statisticsText, value);
            }

            public void RefreshStatistics()
            {
                var monitor = Context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

                builder.Clear();
                monitor.FormatStatistics(builder);

                var text = builder.ToString().TrimEnd();
                StatisticsText = string.IsNullOrWhiteSpace(text) ? NoPerfomenceDataText : text;
            }
        }
    }
}
