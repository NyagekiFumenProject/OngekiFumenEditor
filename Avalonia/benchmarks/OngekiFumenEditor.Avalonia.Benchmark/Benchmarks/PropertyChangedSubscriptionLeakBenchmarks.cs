using System.ComponentModel;
using BenchmarkDotNet.Attributes;

namespace OngekiFumenEditor.Avalonia.Benchmark.Benchmarks;

/// <summary>
/// 比较全局 PropertyChanged 订阅在累计打开编辑器后的广播成本：泄漏、显式解绑和弱订阅。
/// </summary>
[MemoryDiagnoser]
public class PropertyChangedSubscriptionLeakBenchmarks
{
    [Params(1, 10, 100, 1000)]
    public int CumulativeEditorCount;

    private FakeGlobalSetting setting = null!;
    private List<FakeHelperLeaked> leakedHelpers = new();
    private FakeHelperDisposable activeDisposable = null!;
    private List<FakeHelperWeak> weakHelpers = new();

    [GlobalSetup(Target = nameof(LeakedBroadcast))]
    public void SetupLeaked()
    {
        setting = new FakeGlobalSetting();
        leakedHelpers = new List<FakeHelperLeaked>(CumulativeEditorCount);
        for (var i = 0; i < CumulativeEditorCount; i++)
            leakedHelpers.Add(new FakeHelperLeaked(setting));
    }

    [Benchmark(Baseline = true)]
    public int LeakedBroadcast()
    {
        setting.RaiseAny();
        return leakedHelpers[^1].UpdateCount;
    }

    [GlobalSetup(Target = nameof(DisposedBroadcast))]
    public void SetupDisposed()
    {
        setting = new FakeGlobalSetting();
        for (var i = 0; i < CumulativeEditorCount - 1; i++)
        {
            var disposable = new FakeHelperDisposable(setting);
            disposable.Dispose();
        }

        activeDisposable = new FakeHelperDisposable(setting);
    }

    [Benchmark]
    public int DisposedBroadcast()
    {
        setting.RaiseAny();
        return activeDisposable.UpdateCount;
    }

    [GlobalSetup(Target = nameof(WeakEventBroadcast))]
    public void SetupWeak()
    {
        setting = new FakeGlobalSetting();
        for (var i = 0; i < CumulativeEditorCount - 1; i++)
            _ = new FakeHelperWeak(setting);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        weakHelpers = new List<FakeHelperWeak> { new FakeHelperWeak(setting) };
    }

    [Benchmark]
    public int WeakEventBroadcast()
    {
        setting.RaiseAny();
        return weakHelpers[0].UpdateCount;
    }

    private sealed class FakeGlobalSetting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaiseAny()
            => PropertyChanged?.Invoke(this, AnyArgs);

        private static readonly PropertyChangedEventArgs AnyArgs =
            new("EnablePlayFieldDrawing");
    }

    private sealed class FakeHelperLeaked
    {
        public int UpdateCount;

        public FakeHelperLeaked(FakeGlobalSetting setting)
            => setting.PropertyChanged += OnChanged;

        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "EnablePlayFieldDrawing" or "PlayFieldForegroundColor")
                UpdateCount++;
        }
    }

    private sealed class FakeHelperDisposable : IDisposable
    {
        public int UpdateCount;
        private readonly FakeGlobalSetting setting;
        private readonly PropertyChangedEventHandler handler;

        public FakeHelperDisposable(FakeGlobalSetting setting)
        {
            this.setting = setting;
            handler = OnChanged;
            setting.PropertyChanged += handler;
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "EnablePlayFieldDrawing" or "PlayFieldForegroundColor")
                UpdateCount++;
        }

        public void Dispose()
            => setting.PropertyChanged -= handler;
    }

    private sealed class FakeHelperWeak
    {
        public int UpdateCount;

        public FakeHelperWeak(FakeGlobalSetting setting)
        {
            var weakTarget = new WeakReference<FakeHelperWeak>(this);
            PropertyChangedEventHandler? handler = null;
            handler = (sender, args) =>
            {
                if (weakTarget.TryGetTarget(out var target))
                {
                    target.OnChanged(sender, args);
                }
                else
                {
                    setting.PropertyChanged -= handler;
                }
            };
            setting.PropertyChanged += handler;
        }

        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "EnablePlayFieldDrawing" or "PlayFieldForegroundColor")
                UpdateCount++;
        }
    }
}
