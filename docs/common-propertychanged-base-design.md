# CommonPropertyChangedBase 设计与落地（属性通知零分配）

> 来源问题：`docs/render-performance-profile-2026-09-29.md` §4.4-A —— Caliburn 的通知闭包占用 75 s 窗口分配字节的 **54.9%**（4,775 MB）。
> 本文记录设计取舍、实现、benchmark 结果与后续阶段。作者：本次性能剖析的落地方案讨论。

---

## 1. 问题回顾（实测）

- 触发链：`GridBase.Unit/Grid` setter → `Caliburn.Micro.PropertyChangedBase.NotifyOfPropertyChange(string)` → 在有订阅者时分配「lambda 的 DisplayClass + `Action` + `PropertyChangedEventArgs`」。
- 订阅者来源：`OngekiTimelineObjectBase.TGrid`（`:18`）/ `OngekiMovableObjectBase.XGrid`（`:16`）在赋值时经 `RegisterOrUnregisterPropertyChangeEvent` 挂上转发处理器，**被对象持有的网格必然有订阅者**。
- 放大：`NormalizeSelf()` 旧实现一次最多写 6 次属性 → 最多 6 笔通知。
- 实测：闭包类型 `Caliburn.Micro.PropertyChangedBase+<>c__DisplayClass9_0` 占 54.9% 分配字节；堆转储取证——闭包捕获的 `<>4__this` 是一个 `OngekiFumenEditor.Base.XGrid`，`propertyName` = `"Unit"`。
- **这条转发链不能删**：`SoflanList.OnSoflanPropChanged` 显式处理 `nameof(TGrid.Grid)`/`nameof(TGrid.Unit)`（`:60-70`），`BpmList.cs:51-56` 的注释把「经转发链上来的 `TGrid.Unit/Grid` 子属性」写进了内容令牌设计。因此只能降低「触发次数」与「单次成本」。

## 2. 设计取舍

| 议题 | 结论 |
| --- | --- |
| `PropertyChangedEventArgs.Broadcast` 之类共享实例 | 无法挂在 BCL 类型上，需自建持有者。本实现提供 `CommonPropertyChangedBase.AllProperties`（`string.Empty`，与 Caliburn `Refresh()` 语义一致）。注意仓库里 `INotifyPropertyChangedEx.Refresh()` 实际无人调用（`.Refresh()` 全是 `CollectionViewSource.Refresh()`），广播不是热点，不必为它设计额外 API |
| args 重载 vs 覆写 `NotifyOfPropertyChange(string)` | **覆写 string 重载才是主收益**：55 个派生类的既有调用（`Set<T>`、`nameof(...)`、表达式重载）全部汇聚到它，零调用点改动即受益。args 重载用于两条快路径：热名字（`GridBase` 用 `static readonly` args，省掉字典查找）与转发链透传 |
| UI 线程语义 | 必须保留。运行时前提：应用使用 `PlatformProvider.Current` 的默认实现 `DefaultPlatformProvider`（`PropertyChangeNotificationsOnUIThread => true`，但 `OnUIThread(action) => action()` 是**内联调用**，仓库内没有任何地方安装 `XamlPlatformProvider`）。因此实现为：平台允许 + 存在 `Application.Current.Dispatcher` + 当前不在其上 → 走 `OnUIThread` hook（与基类一致）；否则直接 raise。在当前宿主下这与基类行为逐条等价 |
| 重载歧义 | 新增 `NotifyOfPropertyChange(PropertyChangedEventArgs)` 后，`NotifyOfPropertyChange(null)`/`(default)` 会编译歧义。已核对仓库当前 **0 处**这种写法；实现里加了 `ArgumentNullException.ThrowIfNull` 并保留原重载语义 |
| `OnPropertyChanged(PropertyChangedEventArgs)` | Caliburn 里是 `protected` **非虚**，无法覆写；因此不能采用「override 事件访问器」之类技巧——`SoflanGroupWrapItemGroup.cs:145` 会直接调用基类 `OnPropertyChanged(args)`，那样会让该路径静默失效 |
| 缓存形态 | 通用层：`ConcurrentDictionary<string, PropertyChangedEventArgs>`（名字是有限集合）。热层：调用方自备 args。注意集合需线程安全（通知可能来自 `Parallel.ForEach` 工作线程） |

## 3. 实现

`OngekiFumenEditor/Utils/CommonPropertyChangedBase.cs`：

```csharp
public class CommonPropertyChangedBase : PropertyChangedBase
{
    public static readonly PropertyChangedEventArgs AllProperties = new(string.Empty);

    private static readonly IDictionary<string, PropertyChangedEventArgs> argsCache = new ConcurrentDictionary<string, PropertyChangedEventArgs>();

    public static PropertyChangedEventArgs ArgsOf(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return AllProperties;

        if (argsCache.TryGetValue(propertyName, out var cached))
            return cached;

        return argsCache[propertyName] = new PropertyChangedEventArgs(propertyName);
    }

    public override void NotifyOfPropertyChange([CallerMemberName] string propertyName = null)
        => NotifyOfPropertyChange(ArgsOf(propertyName));

    public virtual void NotifyOfPropertyChange(PropertyChangedEventArgs arg)
    {
        if (!IsNotifying)
            return;

        if (arg is null)
            throw new System.ArgumentNullException(nameof(arg));

        if (PlatformProvider.Current.PropertyChangeNotificationsOnUIThread &&
            Application.Current?.Dispatcher is { } dispatcher &&
            !dispatcher.CheckAccess())
        {
            DispatchToUIThread(arg);
            return;
        }

        OnPropertyChanged(arg);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DispatchToUIThread(PropertyChangedEventArgs arg) => OnUIThread(() => OnPropertyChanged(arg));
}
```

`GridBase` 接入（`OngekiFumenEditor/Base/GridBase.cs`）：

```csharp
public abstract class GridBase : CommonPropertyChangedBase, IComparable<GridBase>, ISerializable, IComparable
{
    private static readonly PropertyChangedEventArgs GridNotifyArgs = ArgsOf(nameof(Grid));
    private static readonly PropertyChangedEventArgs UnitNotifyArgs = ArgsOf(nameof(Unit));
    ...
    public int Grid
    {
        get => grid;
        set
        {
            if (grid == value)
                return;

            grid = value;
            RecalculateTotalValues();
            NotifyOfPropertyChange(GridNotifyArgs);
        }
    }
    // Unit 同理

    public void NormalizeSelf()
    {
        var oldUnit = unit;
        var oldGrid = grid;

        var newUnit = oldUnit + oldGrid / GridRadix;
        var newGrid = (int)(oldGrid % GridRadix);

        var diff = newUnit - (int)newUnit;
        newUnit = (int)newUnit;
        newGrid += (int)(diff * GridRadix);

        if (newGrid < 0)
        {
            newGrid += (int)GridRadix;
            newUnit--;
        }

        if (newUnit == oldUnit && newGrid == oldGrid)
            return;

        unit = newUnit;
        grid = newGrid;
        RecalculateTotalValues();

        if (newUnit != oldUnit)
            NotifyOfPropertyChange(UnitNotifyArgs);
        if (newGrid != oldGrid)
            NotifyOfPropertyChange(GridNotifyArgs);
    }
}
```

两点说明：

1. **变更检测用显式写法而不是 Caliburn 的 `Set(ref …)`**：`Set` 内部「写字段 → 发通知 → 返回 true」，于是 `RecalculateTotalValues()` 只能落在通知**之后**，回调期间 `TotalGrid/TotalUnit` 是旧值。现有消费者不读总量（索引类 `rebuildProperties` 只含对象级名字），风险低，但「通知时派生字段已一致」是本文件改动前就成立的隐式不变量，故保持「重算在前」。
2. **`NormalizeSelf` 里的两个等值判断不能合并成无条件双通知**：两者并非必然同时变化。反例（已用 float32 语义穷举验证）：当 `|frac(unit)| < 1 / GridRadix` 且 grid 已在 `[0, GridRadix)` 时，`(int)(diff * GridRadix) == 0`，Unit 被截断而 Grid 不变——此时只有 Unit 变。200,000 组随机输入里 199,999 组两者都变、1 组只有 Unit 变，说明这个分支很窄但真实存在；两个比较的代价可忽略。

## 4. 实现期间发现的关键点：热路径方法体内不能有闭包

第一版实现把跨线程派发写成 `OnUIThread(() => OnPropertyChanged(arg))` 直接放在 `NotifyOfPropertyChange(PropertyChangedEventArgs)` 里（该分支在 UI 线程/无 Dispatcher 时根本不会执行）。benchmark 显示每次通知仍有 **32 B** 分配，排查过程：

| 实验 | 结果 |
| --- | --- |
| Caliburn 原实现，**无订阅者**（早退路径）| 32 B/op |
| 纯 BCL 对照：闭包分支**从不执行** | 0 B/op |
| 纯 BCL 对照：闭包分支执行 | 64 B/op（DisplayClass 32 + delegate 32，符合预期）|
| 把闭包移到独立的 `NoInlining` 冷方法后 | **0 B/op** |

结论：**「方法体内含捕获型 lambda」本身导致每次调用约 32 B 分配**（即使该 lambda 所在分支从未执行），这与纯 BCL 对照不符、机制未完全查清（疑与 JIT 对闭包的分配提升相关），但在两个独立计数器（`GC.GetAllocatedBytesForCurrentThread` / `GC.GetTotalAllocatedBytes(precise:true)`）与两个 harness（自建探针 / BDN）下都稳定复现，且 Caliburn 自己的 `NotifyOfPropertyChange(string)` 就是受害者（无订阅者也要 32 B）。

因此落地形态是：**热路径（`NotifyOfPropertyChange(PropertyChangedEventArgs)`）体内不含任何闭包，派发闭包单独放在 `DispatchToUIThread`（`NoInlining`）里**。这条经验对其它热路径同样适用。

## 5. Benchmark

类：`OngekiFumenEditor.Benchmark/Benchmarks/GridNotificationAllocationBenchmarks.cs`（`[MemoryDiagnoser]`，`OperationsPerInvoke = 1000`，含旧实现逐行复刻 `LegacyRadixGrid` 与 `Setup` 中的结果等价性校验：11 组输入下 `NormalizeSelf` 的 `Unit/Grid/TotalGrid/TotalUnit` 必须一致）。

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter "*GridNotificationAllocationBenchmarks*" --job short
```

结果（`short` job，per-op）：

| Method | Mean | Allocated | 说明 |
| --- | --- | --- | --- |
| `SetUnit_Legacy` | 24.26 ns | **120 B** | 旧 setter：无条件通知 + Caliburn 闭包 |
| `SetUnit_Fixed` | 3.46 ns | **0 B** | 变更检测 + args 复用 |
| `NormalizeSelf_AlreadyNormalized_Legacy` | 73.79 ns | **480 B** | 已规范化仍写 4 次属性 |
| `NormalizeSelf_AlreadyNormalized_Fixed` | 4.45 ns | **0 B** | 早退，零通知 |
| `NormalizeSelf_Fractional_Legacy` | 99.80 ns | **600 B** | 赋值 + 4 次规范化写入 = 5 笔通知 |
| `NormalizeSelf_Fractional_Fixed` | 14.68 ns | **0 B** | 至多 2 笔通知，且零分配 |
| `NormalizeSelf_Fresh_Legacy` | 50.42 ns | 248 B | 无订阅者的临时网格 |
| `NormalizeSelf_Fresh_Fixed` | 20.88 ns | 56 B | 只剩 `TGrid` 对象本身 |
| `Notify_ByString_Legacy` | 16.61 ns | **120 B** | Caliburn 通知（带订阅者）|
| `Notify_ByString_Fixed` | 5.39 ns | **0 B** | 走 args 缓存 |
| `Notify_ByCachedArgs_Fixed` | 1.59 ns | **0 B** | 直接发布预置 args |
| `Subscribe_ForwardingHelper_Legacy` | 184.77 ns | 152 B | 转发订阅（阶段 2 目标）|
| `Subscribe_ForwardingHelper_Fixed` | 189.44 ns | 152 B | 阶段 1 未改动此处 |

结论：

- **单笔通知分配 120 B → 0 B（100% 消除）**，耗时 16.6 ns → 1.6–5.4 ns。
- `NormalizeSelf` 在「已规范化」路径上 480 B → 0 B；在真实规范化路径上 600 B → 0 B。
- 连临时网格的构造+规范化也从 248 B 降到 56 B（仅对象本身）。
- 转发订阅（152 B/次 + 每次赋值的闭包）**未变**，属阶段 2。

按 75 s 窗口 2.0M 通知/s 的实测频率折算，本项对应报告里 54.9% 的那部分分配（≈69 MB/s 的闭包 + delegate + args）应整体消失。

## 6. 验证方式

1. **构建**：`dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug`（0 error）。
2. **语义等价**：benchmark `Setup()` 里对 11 组输入（含负数、跨 radix、大数值）比对旧实现复刻与新实现的 `NormalizeSelf` 结果，任何不一致直接抛异常；`NormalizeSelf` 的 `(Unit, Grid, TotalGrid, TotalUnit)` 全部一致。
3. **行为回归**（需人工）：打开谱面后拖动/移动 soflan、移动物件、Hold 端点，确认 `SoflanList` 位置缓存与 `BpmList` 内容令牌仍会失效（它们依赖转发链上的 `TGrid/Unit/Grid` 通知）。
4. **运行时复测**（下一步，需重启应用）：按 `docs/render-performance-profile-2026-09-29.md` §9.1 重跑 75 s trace，预期 `<>c__DisplayClass9_0` 退出分配类型榜首、`dotnet.gc.heap.total_allocated` 从 ~107 MB/s 降到 ~40 MB/s 量级、`gc.collections[gen0]` 从 7 次/s 降到 ~3 次/s。
5. **回滚**：本阶段只新增 `Utils/CommonPropertyChangedBase.cs` 并改动 `Base/GridBase.cs`（含基类替换、setter 变更检测、`NormalizeSelf` 单次通知）；把 `GridBase` 的基类改回 `PropertyChangedBase` 并把两个 setter/`NormalizeSelf` 恢复为「属性写入」形态即可完全回滚。

## 8. 其它高频通知点排查（2026-09-29 追加）

问：除了网格，还有哪些属性变更事件触发频率高、应该改用同一套 args 复用分发？用既有 75 s trace + 源码盘点回答。

### 8.1 数据：这个负载里通知分配 100% 来自网格

给分析器加了「按分配类型过滤」后（`--alloc-type DisplayClass9_0`，闭包类型只由 Caliburn 通知产生，可当通知计数器），75 s 窗口内 **46,971 ticks 全部落在网格族**：

| 站点 | ticks | 占比 | 归谁 |
| --- | --- | --- | --- |
| `GridBase.NormalizeSelf()` | 20,228 | 43.1% | `TGrid/XGrid` 写入 ✅ 已覆盖 |
| `TGrid.op_Addition`（构造新 TGrid） | 10,886 | 23.2% | ✅ 已覆盖 |
| `ConnectableChildObjectBase.CalulateXGrid` | 6,526 | 13.9% | ✅ 已覆盖 |
| `Caliburn…NotifyOfPropertyChange(string)`（caller: NormalizeSelf） | 3,747 | 8.0% | ✅ 已覆盖 |
| `DrawPlayableAreaHelper_new.QueryBoundaryXGridUnit` | 3,631 | 7.7% | ✅ 已覆盖 |
| `ConvertToLimitParam` | 925 | 2.0% | ✅ 已覆盖 |
| `BuildAreaSample` | 912 | 1.9% | ✅ 已覆盖 |
| `ConnectableStartObject.CalulateXGrid` | 61 | 0.13% | ✅ 已覆盖 |
| `AddScreenDistanceSamples` | 55 | 0.12% | ✅ 已覆盖 |

**没有第 10 个站点**；trace 里也查不到任何 `System.Linq.Expressions.*` 分配，说明物件 setter 的表达式重载在这个负载里根本没被调用（物件网格不会逐帧重赋值）。结论：稳态渲染下除网格外没有高频通知点。

### 8.2 结构性约束：不是所有类都能换基类

| 类别 | 例子 | 能否换 `CommonPropertyChangedBase` |
| --- | --- | --- |
| 直接派生 `PropertyChangedBase` | `OngekiObjectBase`、`OngekiFumen`、`FumenMetaInfo`、`RangeValue`、`SoundVolumeProxy`、`WaveformDrawingOptionBase`、`DefaultFumenSoundPlayer`、`DefaultMusicPlayer`、`NAudioManager`、设置页 VM、`KeyBindingDefinition`…… | ✅ 一行换基类 |
| Caliburn 的 `Screen` 家族 | `FumenVisualEditorViewModel : PersistedDocument`、`AudioPlayerToolViewerViewModel : Tool`、`RenderPerfomenceMeasurePanelViewModel : WindowBase` | ❌ 单继承，中间隔着 `Screen`。需要在该类里**本地覆写** `NotifyOfPropertyChange(string)`，用 `CommonPropertyChangedBase.ArgsOf(propertyName)` 取共享 args，再在自己的类里 `OnPropertyChanged(args)`（`OnPropertyChanged` 是 `protected`，只能由派生类自身调用）——仅在它变成热点时才值得做 |

### 8.3 表达式重载是最贵的通知形式（实测）

`NotifyOfPropertyChange(() => X)`（全仓库 **217 处**）会在**调用点**构造表达式树，因此代价与订阅者无关：

| 形式 | 每次成本（实测） | 说明 |
| --- | --- | --- |
| `NotifyOfPropertyChange(() => X)` | **368–464 B** | 表达式树 + `GetMemberInfo()` 反射 + 通知本身；换成 `nameof` 前这笔开销无法通过换基类消除 |
| `NotifyOfPropertyChange(nameof(X))`（Caliburn） | 120 B | 闭包 + 委托 + args |
| `NotifyOfPropertyChange(nameof(X))`（本基类） | **0 B** | 复用 args、热路径无闭包 |
| 对比：`Set(ref x, v)`（285 处） | 120 B → 0 B | 换基类后即 0（`Set` 内部走 `NotifyOfPropertyChange(string)`）|

### 8.4 本次追加的迁移与验证

- `OngekiObjectBase` → `CommonPropertyChangedBase`（一行，覆盖全部物件：`Bullet`/`Bell`/`Hold`/`Tap`/`Soflan`/`LaneStart`……）。
- 实测（真实物件 `OngekiFumenEditor.Base.OngekiObjects.Tap`，独立进程探针）：
  - `obj.NotifyOfPropertyChange(nameof(Tag))`：**0 B/op**（换基类前为 120 B）；
  - `obj.NotifyOfPropertyChange(() => Tag)`：368 B/op（表达式树仍在调用点，换基类只省下其中约 120 B）；
  - `obj.TGrid = new TGrid(...)`（含转发订阅）：472 B/op，其中转发订阅 152 B 属阶段 2 目标。
- 未做：317 处表达式重载调用点（217 表达式 + 100 其它）暂不批量改写——当前无热点证据，改写属于纯 churn；等某个物件通知路径真的进入逐帧/逐物件写入时，再按「`nameof(...)` + （可选）静态 args 字段」改写该处即可。

### 8.5 观察名单（若将来变热，按此顺序处理）

1. `FumenVisualEditorViewModel`（含 partial）与 `FumenVisualEditorViewModel.ScrollViewer`：视口/滚动/调试信息写入在拖动时逐帧发生；当前走 `Set(ref ...)`（换基类不可行 → 用 8.2 的本地覆写方案）。
2. `AudioPlayerToolViewerViewModel`（波形拖动、播放位置、`SliderValue`）与 `DefaultFumenSoundPlayer`/`DefaultMusicPlayer`（后者可直接换基类）。
3. `RenderPerfomenceMeasurePanelViewModel` + `RenderPerfomenceMeasureItem`（FPS/耗时面板：前者是 `WindowBase`，后者可直接换基类）。
4. 物件侧：把 `Bullet`/`Bell`/`Hold`/`Soflan` 等 setter 里的 `NotifyOfPropertyChange(() => X)` 改成 `nameof(X)`（每处省 368 B，且消除反射），前提是这些 setter 进入逐帧路径。


## 9. 后续（阶段 2 及以后）

1. **转发链去闭包**：`Utils/PropertyChangedBaseExtensionMethod` 目前每次赋值分配闭包 + 委托（实测 152 B/次），并用 `ConcurrentDictionary<int, WeakReference<PropertyChangedEventHandler>>` 以 `RuntimeHelpers.GetHashCode` 为键做收发管理（哈希碰撞会解错 handler）。可改为：网格持 `ForwardTarget`（宿主）字段 + 单个静态委托（`ArgsOf` 透传 args），把 67,084 个常驻 `PropertyChangedEventHandler`（4.09 MiB）与每次赋值的闭包一并去掉。
2. **其余派生类按需迁移**：`GridBase`、`OngekiObjectBase`（全部物件）已接入；`OngekiFumen`、集合类、音频/设置页等 50+ 个直接派生类可逐步跟进（API 全保留，编译期安全），`Screen` 家族按 §8.2 的本地覆写方案处理；低频类型收益有限，不必一次全换。
3. **表达式重载清理**：217 处 `NotifyOfPropertyChange(() => X)` 按 §8.5 的名单，在对应路径变热时逐处改成 `nameof(...)`（每次省 368 B 与一次反射）。
4. **运行时复测**：见 §6.4。
