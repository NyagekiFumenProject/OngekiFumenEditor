using System.ComponentModel;
using System.Windows;
using BenchmarkDotNet.Attributes;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 对应 DrawPlayableAreaHelper 性能优化点 #7:
/// `EditorGlobalSetting.Default.PropertyChanged += Default_PropertyChanged` 永不解绑。
///
/// 每次打开新编辑器都创建一个新 DrawPlayableAreaHelper 并订阅同一个全局 setting 的
/// PropertyChanged。旧 helper 永远被事件源根引用,无法 GC;之后每次 setting 变更都会
/// 同步回调所有历史 helper (UpdateProps 是同步链)。
///
/// 候选:
///  - Leaked        : 模拟现状 — N 个 helper 全部活着 (N 来自 [Params])
///  - DisposedOnce  : 模拟修复 — 旧 helper 在 "卸载" 时 -= 取消订阅,只剩 1 个 active
///  - WeakEvent     : 用 WeakEventManager 替代;依赖 GC 把旧 helper 断开
///
/// 度量:
///  - 单次 PropertyChanged 触发的 dispatch 耗时 (CPU)
///  - 长期累积下来活着的 helper 数(用 WeakReference + GC 验证)
/// </summary>
[MemoryDiagnoser]
public class PropertyChangedSubscriptionLeakBenchmarks
{
    /// <summary>
    /// 模拟一次开发会话里累计打开过多少个编辑器(每个 = 一个 helper 订阅)。
    /// </summary>
    [Params(1, 10, 100, 1000)]
    public int CumulativeEditorCount;

    private FakeGlobalSetting setting = null!;
    private List<FakeHelperLeaked> leakedHelpers = new();
    private FakeHelperDisposable activeDisposable = null!;
    private List<FakeHelperWeak> weakHelpers = new();

    // ===== Leaked scenario setup =====

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

    // ===== Disposed scenario setup =====

    [GlobalSetup(Target = nameof(DisposedBroadcast))]
    public void SetupDisposed()
    {
        setting = new FakeGlobalSetting();
        // 模拟历史 helper 都已正确 -= 解绑,只留最后一个 active
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

    // ===== WeakEvent scenario setup =====

    [GlobalSetup(Target = nameof(WeakEventBroadcast))]
    public void SetupWeak()
    {
        setting = new FakeGlobalSetting();
        // 历史 helper 不持引用,期望 GC 后被回收
        for (var i = 0; i < CumulativeEditorCount - 1; i++)
        {
            _ = new FakeHelperWeak(setting);
        }
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

    // ===== 数据类型 =====

    private sealed class FakeGlobalSetting : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // 模拟两个真正会触发 UpdateProps 的属性切换 + 一个无关属性广播
        private bool enable;
        public bool EnablePlayFieldDrawing
        {
            get => enable;
            set
            {
                if (enable == value) return;
                enable = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnablePlayFieldDrawing)));
            }
        }

        public void RaiseAny()
            => PropertyChanged?.Invoke(this, AnyArgs);

        // ApplicationSettingsBase 真实场景下每次 setting 变化都会广播
        private static readonly PropertyChangedEventArgs AnyArgs = new("EnablePlayFieldDrawing");
    }

    // ----- 现状模型 -----
    private sealed class FakeHelperLeaked
    {
        public int UpdateCount;
        public FakeHelperLeaked(FakeGlobalSetting s)
        {
            s.PropertyChanged += OnChanged;
        }
        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 模拟 UpdateProps 的 switch
            switch (e.PropertyName)
            {
                case "EnablePlayFieldDrawing":
                case "PlayFieldForegroundColor":
                    UpdateCount++;
                    break;
            }
        }
    }

    // ----- 修复模型: IDisposable -----
    private sealed class FakeHelperDisposable : IDisposable
    {
        public int UpdateCount;
        private readonly FakeGlobalSetting setting;
        private readonly PropertyChangedEventHandler handler;
        public FakeHelperDisposable(FakeGlobalSetting s)
        {
            setting = s;
            handler = OnChanged;
            s.PropertyChanged += handler;
        }
        private void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "EnablePlayFieldDrawing":
                case "PlayFieldForegroundColor":
                    UpdateCount++;
                    break;
            }
        }
        public void Dispose() => setting.PropertyChanged -= handler;
    }

    // ----- 修复模型: WeakEvent (WPF) -----
    private sealed class FakeHelperWeak : IWeakEventListener
    {
        public int UpdateCount;
        public FakeHelperWeak(FakeGlobalSetting s)
        {
            PropertyChangedEventManager.AddListener(s, this, string.Empty);
        }
        public bool ReceiveWeakEvent(Type managerType, object? sender, EventArgs e)
        {
            if (e is PropertyChangedEventArgs pce)
            {
                switch (pce.PropertyName)
                {
                    case "EnablePlayFieldDrawing":
                    case "PlayFieldForegroundColor":
                        UpdateCount++;
                        break;
                }
            }
            return true;
        }
    }
}
