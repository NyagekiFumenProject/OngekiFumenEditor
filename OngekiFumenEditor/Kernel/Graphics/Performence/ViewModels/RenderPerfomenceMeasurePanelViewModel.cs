using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Kernel.Graphics;
using OngekiFumenEditor.Kernel.Graphics.Performence;
using OngekiFumenEditor.Properties;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
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

        public ObservableCollection<RenderPerfomenceMeasureItem> Items { get; } = new();

        public bool HasRenderContexts => Items.Count > 0;

        public bool HasNoRenderContexts => !HasRenderContexts;

        public RenderPerfomenceMeasurePanelViewModel()
        {
            DisplayName = GetResourceString(
                "RenderPerfomenceMeasurePanelTitle",
                "Render Performance Measurement");

            refreshTimer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromSeconds(1)
            };
            refreshTimer.Tick += (_, _) => RefreshStatistics();
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
            Items.Clear();

            foreach (var renderManager in IoC.GetAll<IRenderManagerImpl>())
            {
                foreach (var context in renderManager.GetRenderContexts())
                    Items.Add(new RenderPerfomenceMeasureItem(renderManager.Name, context));
            }

            NotifyOfPropertyChange(() => HasRenderContexts);
            NotifyOfPropertyChange(() => HasNoRenderContexts);

            RefreshStatistics();
            refreshTimer.Start();
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

        public sealed class RenderPerfomenceMeasureItem : PropertyChangedBase
        {
            private readonly IRenderContext context;
            private readonly StringBuilder builder = new();
            private string statisticsText = NoPerfomenceDataText;

            public RenderPerfomenceMeasureItem(string renderManagerName, IRenderContext context)
            {
                this.context = context;

                var monitor = context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;
                Header = $"{context.Name} {renderManagerName} / {context.GetType().Name} #{context.GetHashCode()} ({monitor.GetType().Name})";
            }

            public string Header { get; }

            public string StatisticsText
            {
                get => statisticsText;
                private set => Set(ref statisticsText, value);
            }

            public void RefreshStatistics()
            {
                var monitor = context.PerfomenceMonitor ?? DummyPerformenceMonitor.Instance;

                builder.Clear();
                monitor.FormatStatistics(builder);

                var text = builder.ToString().TrimEnd();
                StatisticsText = string.IsNullOrWhiteSpace(text) ? NoPerfomenceDataText : text;
            }
        }
    }
}
