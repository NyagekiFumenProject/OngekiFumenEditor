# OngekiFumenEditor 渲染期性能剖析报告（2026-09-29 实测）

> 测量对象：正在持续渲染谱面的 `OngekiFumenEditor.exe`（PID 29936，WPF 树）
> 测量方式：EventPipe 采样/GC/分配追踪 + 运行时计数器 + GCDump + 进程堆转储（SOS）
> 原始数据目录：`F:\perf-artifacts\ongeki-20260929\`（清单见 §9）
> 单位约定：本文 MB 一律指 MiB（1 MiB = 1,048,576 B），文件体积除外（§9 按十进制 GB）；百分比若无特别说明，均相对本轮窗口内该维度的总量。

---

## 0. 结论速览

| # | 发现 | 量化影响 | 证据 | 处置方向 |
|---|------|----------|------|----------|
| 1 | **NAudio WASAPI 输出线程忙轮询**：`new WasapiOut(AudioClientShareMode.Shared, 0)` 使 NAudio 的等待超时恒为 0，`WasapiOut.PlayThread` 退化为「立即返回的等待 + 缓冲检查」死循环 | **~70 s CPU / 76 s ≈ 1 个物理核，占进程 CPU 的 44.8%**；即使不播放（`Stopped` 之外）也持续燃烧 | OS 线程 CPU 增量 + 线程状态 199/200 采样为 Running + 托管栈 84.9% 落在 `WaitHandle.WaitMultiple` | 传入合理 latency（NAudio 默认 200ms）或升级/修补 NAudio，见 §3.3 |
| 2 | **对象/闭包分配风暴**：`GridBase.Unit/Grid` 的 setter 每次都触发 Caliburn `NotifyOfPropertyChange`，后者在有订阅者时分配闭包 + `PropertyChangedEventArgs`；`NormalizeSelf()` 一次最多写 6 次属性 | **分配速率稳态 106.7 MB/s（中位）、峰值 118.6 MB/s**；其中 `<>c__DisplayClass9_0` 闭包占 **54.9%**（4,775 MB/75 s） | GCAllocationTick 采样（类型 + 调用点） | 见 §4.3；预计可削掉一半以上分配量 |
| 3 | **网格对象算术分配**：`TGrid.op_Addition`（`new TGrid`）、`XGrid.op_Addition`、`GridOffset`、`GetChildObjectsFromTGrid`（`GetRange` 产生 `List` + 数组）在每帧可见对象循环中反复分配 | TGrid 1,118 MB + XGrid 894 MB + GridOffset 431 MB + List/数组 660 MB（合计 ≈ 3.1 GiB/75 s） | 同上 + 调用者链 | 复用现有 `TryGetValidPathChildRange`/`ObjectPool`，见 §4.4 |
| 4 | **GC 停顿不是瓶颈**：546 次 GC / 75 s（7.3 次/s，其中 gen0 526），全部由分配触发，0 次显式 `GC.Collect` | 暂停总时长 **442 ms / 75 s = 0.59%**，p50 0.79 ms，最大 1.87 ms | GCStart/GCStop/GCHeapStats 事件 + 计数器 | 无需处理暂停；应处理产生 GC 的分配速率（第 2/3 条） |
| 5 | **托管堆「虚胖」**：GC 堆分配 515 MiB，其中**活跃对象仅 ~187 MiB，空闲 328 MiB（63.7%）**，主要来自 LOH 碎片 | LOH 报告尺寸 410 MiB，其中碎片 ~308 MiB；进程工作集 ~1.05 GiB | SOS `eeheap -gc` + `dumpheap -stat` + GCDump 交叉验证 | 大缓冲池化 / 加载后一次 LOH 压缩，见 §6 |
| 6 | UI 线程承担全部渲染：帧循环在 UI 线程，其中 16.4% 的线程时间为等待 GL 呈现，4.35% 花在 `OnEditorRender` 里对 Bullets 的 LINQ `Where` 上 | UI 线程占进程 CPU 24.8% | 采样栈 + Scout 源码映射 | §3.4 / §4.4 |
| 7 | 杂项：锁竞争 87 次/s，线程池工作项 1,770 个/s（`TaskReplicator+Replica` 分配 78 MB/75 s），UI Automation peer 数组分配 ~85 MB/75 s，0 异常 | 次要但可观 | 计数器 + 分配采样 | §4.5 / §7 |

---

## 1. 测量对象与场景

| 项 | 值 |
|----|----|
| 进程 | `OngekiFumenEditor.exe`，PID 29936，启动于 2026-09-29 06:39:05 |
| 部署 | `C:\Users\mikir\Desktop\OngekiFumenEditor\`（framework-dependent single-file，`PublishSingleFile`） |
| 版本 | `0.9.8.132+7f0f2953ea`，即仓库 HEAD `7f0f2953ea`（`feat(editor): show cancellable loading dialog while opening a fumen`），与仓库 `OngekiFumenEditor/` 源码一致 |
| 运行时 | .NET 10（SOS 版本行 `10.0.1226.42308`），工作区模式 GC（Workstation，单 GC 堆） |
| 机器 | Ryzen 7 5800X（8C/16T）、GTX 1660 SUPER、Windows 11 26100 |
| 场景 | 已打开谱面 `F:\refreshAct2\package\option\A012\music\music8090\8090_10.ogkr`，编辑器持续渲染；音频（`music0769.wav`，由 `.acb` 解码缓存）已加载，06:42 播放结束后界面仍在连续重绘 |
| 渲染配置 | `DefaultRenderManagerImplementName=OpenGL`、`SkiaRenderBackend=DirectX`、`D3DRenderQueueFrameCount=3`、`MsaaSampleCount=0`、`EnablePlayFieldDrawing=true`、`EnableMcpServerInGUIMode=true` |

测量期间进程保持稳定占用约 2 个核、分配速率稳定，属于典型「打开谱面后的稳态渲染」负载，而非加载瞬时峰值。

## 2. 测量方法与可信度

### 2.1 使用的工具与命令

| 目的 | 命令 | 产出 |
|------|------|------|
| 运行时计数器基线（无采样器干扰，150 s） | `dotnet-counters collect -p 29936 --counters System.Runtime --format csv -o counters-runtime.csv --refresh-interval 1 --duration 00:00:02:30` | `counters-runtime.csv` |
| 线程时间采样 + GC + 分配点（75 s） | `dotnet-trace collect -p 29936 -o render.nettrace --duration 00:00:01:15 --providers Microsoft-DotNETCore-SampleProfiler:0x0000F00000000000:4,Microsoft-Windows-DotNETRuntime:0x41008019:5` | `render.nettrace`（54.6 MB，1,398,279 事件） |
| 托管堆对象图 | `dotnet-gcdump collect -p 29936 -o heap.gcdump -t 120` | `heap.gcdump`（29 MB）、`heap-report.txt` |
| 堆段/代/存活统计 | `dotnet-dump collect -p 29936 --type Heap -o heap.dmp`，随后 `dotnet-dump analyze heap.dmp -c "eeheap -gc" -c "clrthreads" -c "dumpheap -stat"` | `heap.dmp`（1.03 GB）、`sos-gc.txt`、`sos-dumpheap.txt` |
| 逐线程 CPU（权威量） | PowerShell 两次快照 `Get-Process -Id 29936` 的 `Threads[].TotalProcessorTime`（trace 前后各一次，窗口 06:59:02 → 07:00:18） | `threads-t0.txt`、`threads-t1.txt` |
| 分析器 | 本仓库外的一次性工具 `F:\perf-artifacts\ongeki-20260929\toolkit\`（C#，基于 `Microsoft.Diagnostics.Tracing.TraceEvent`，把 `.nettrace` 解析为：采样归属、分配归属、GC 统计） | `render-report.md` / `.txt` |

### 2.2 采样语义与限制（务必先读）

1. **没有内核 ETW CPU 采样**。本机账号不在 Administrators 组，ETW 内核会话 `Access Denied`（PerfView 同样要求提权），且不应在用户机器上触发 UAC。因此使用 **EventPipe 的 `Microsoft-DotNETCore-SampleProfiler`**：它由运行时周期性（本机实测 **每线程 40,192 次 / 75.02 s ≈ 536 次/s，即 1.867 ms 一个采样点**）挂起运行时并遍历**所有托管线程**的栈。
2. 因此 **采样计数是「线程墙钟时间」而不是「CPU 时间」**：一个阻塞在信号量上的线程和一个满负荷计算的线程拿到的样本数相同。凡是涉及「谁吃 CPU」的结论，本文都以 **OS 线程 CPU 增量**为准（§3.2），采样栈只用于回答「该线程把时间花在什么代码上」。
3. 采样事件的 `Type` 字段（`Managed` / `External`）实测与线程是否在执行托管代码高度相关：音频线程 `Managed=39,622` 且烧掉约一个核；WPF 笔线程 `External=40,192` 且几乎不耗 CPU。据此把 `Managed` 样本当作 CPU 归因的近似（§3.6），并显式标注其为近似值。
4. **原生线程不可见**：纯原生线程（GL/WPF 合成）没有托管栈，采样里查不到（§3.5 中 4 万/4.9 万/3.9 万号线程即如此）。
5. **采样器自身开销**：每次遍历都要挂起运行时，实测非 GC 的挂起窗口 ≈ 533 次/s × 0.07–0.16 ms ≈ **3–5% 额外开销**。这也是为什么下文 `PollGCWorker`（运行时的挂起/GC 停靠路径）样本量偏大——它是**运行时代码里等停靠**，不是业务 CPU 消耗，读数需打折看待。
6. 所有 GC 结论来自 **GCStart/GCStop/GCHeapStats 事件**，与采样器无关，不受上面干扰。

### 2.3 交叉验证（三处独立一致性）

| 指标 | 方法 A | 方法 B | 偏差 |
|------|--------|--------|------|
| 分配速率 | 计数器 `dotnet.gc.heap.total_allocated` 中位 112 MB/s | GCAllocationTick 采样 8,705 MB / 75 s = 116 MB/s | ≈ 3.5% |
| 活跃堆大小 | GCDump「GC Heap bytes」= 183.1 MiB | SOS `dumpheap -stat` 总量 − `Free` = 186.9 MiB | ≈ 2% |
| 进程 CPU | OS 线程 CPU 增量合计 151.1 s / 76 s = 1.99 核 | `Managed` 采样换算 168.4 s / 75 s ≈ 2.2 核 | ≈ 10%（采样换算会高估，见 §2.2-3/5） |

---

## 3. CPU 热点

### 3.1 进程总体

- 稳态基线（150 s，无采样器）：**286.4 s CPU = 1.91 核**；其中用户态 162.9 s、内核态 123.4 s（**内核态占比 43%**，与 GL 驱动、WASAPI、系统调用和分配器路径相符）。
- 采样窗口（76 s）：**156.45 s / 76 s = 2.06 核**，瞬时抽样 10 s 亦为 1.94 核。
- 工作集约 1.0–1.05 GiB（峰值 1.05 GiB），私有内存约 872 MiB，句柄约 1,860，托管线程 47–49 条。

### 3.2 线程级 CPU（权威：OS `TotalProcessorTime` 增量，窗口 76 s）

| TID（十进制/十六进制） | CPU 秒 | 占比 | 身份判定 | 依据 |
|---|---|---|---|---|
| 49892 (0xC2E4) | **70.06** | **44.8%** | NAudio `WasapiOut.PlayThread` 音频输出线程 | 栈：`WasapiOut.PlayThread`；状态采样 199/200 = Running |
| 31496 (0x7B08) | **38.83** | 24.8% | WPF 主/UI 线程 | 栈：`Startup.Main → Application.RunInternal → Dispatcher.PushFrameImpl → GetMessageW`；`clrthreads` OSID 匹配 |
| 40004 (0x9C44) | 11.42 | 7.3% | 原生线程（无托管样本） | `clrthreads` 存在但采样无栈 |
| 48984 (0xBF58) | 5.53 | 3.5% | 原生线程（无托管样本） | 同上 |
| 38996 (0x9854) | 5.33 | 3.4% | 原生线程（无托管样本） | 同上 |
| 其余 40+ 条 | ≈ 25.3 | 16.2% | 线程池/终结器/定时器/诊断等 | — |

> 占比以**进程总 CPU 增量 156.45 s** 为分母；其中可匹配到 t0/t1 快照的线程合计 151.1 s，差值来自期间创建/退出的线程。

**结论**：进程 CPU 的两大来源是**音频输出线程（44.8%）**与**UI 渲染线程（24.8%）**，两者合计 ≈ 70%。

### 3.3 音频线程：WASAPI 忙轮询（本轮最大单项问题）

证据链：

1. **CPU**：76 s 窗口 70.06 s（92% 占用）；独立复测 10 s 窗口 9.578 s（96%）。
2. **状态**：50 ms 间隔采样 200 次，199 次为 `Running`，仅 1 次 `Wait`。
3. **托管栈（40,192 样本）**：
   - `System.Threading.WaitHandle.WaitMultiple(...)` 34,123（**84.9%**）
   - `NAudio.Wave.WasapiOut.PlayThread()` 6,012（15.0%）
   - `NAudio.Wave.SampleProviders.MixingSampleProvider.Read` / `FillBuffer` 极少（<0.1%）
4. **代码路径**：`OngekiFumenEditor/Kernel/Audio/NAudioImpl/NAudioManager.cs:115`
   ```csharp
   AudioOutputType.Wasapi => new WasapiOut(AudioClientShareMode.Shared, 0),
   ```
   NAudio 2.3.0 中该构造重载等价于 `WasapiOut(device, shareMode, useEventSync: true, latency: 0)`；`PlayThread` 主循环（NAudio `WasapiOut.cs:126-136`）为：
   ```csharp
   while (playbackState != PlaybackState.Stopped) {
       if (isUsingEventSync) WaitHandle.WaitAny(waitHandles, 3 * latencyMilliseconds, false); // 3 * 0 = 0 → 立即返回
       else Thread.Sleep(latencyMilliseconds / 2);
       ...
   }
   ```
   `latencyMilliseconds == 0` 使等待超时为 0，循环退化为自旋；NAudio 仅在共享模式下用 `audioClient.StreamLatency` 回填该值（`WasapiOut.cs:456-462`），而它自己的注释就写着 *“Windows 10 returns 0 from stream latency, resulting in maxing out CPU usage later”*——本机正是该分支下取到 0 的情形。
5. **持续性**：循环退出条件是 `Stopped`；谱面播放已于 06:42 结束、07:00 仍在满负荷，说明**只要音频设备对象存活就会持续烧核**。

**影响**：约 1 个物理核 = 进程 CPU 的 44.8%，且与渲染无关（用户关闭/暂停播放也不消失）。**建议**：传入非 0 latency（NAudio 自身默认 200 ms），例如 `new WasapiOut(AudioClientShareMode.Shared, 100)`；或改用 `WaveOut { DesiredLatency = 100 }`（同文件 `WaveOut` 分支已是 100）。修复后应复测该线程 CPU 应降至接近 0。

### 3.4 UI 线程（帧内剖析）

UI 线程 40,192 样本，其中 `Managed=19,514`（≈36–39 s CPU，与 OS 的 38.83 s 吻合）。

自耗时前 15 位（占该线程样本数）：

| 占比 | 方法 | 解读 |
|---|---|---|
| 30.05% | `MS.Win32.UnsafeNativeMethods.GetMessageW` | 消息泵空闲等待（正常） |
| 20.81% | `Thread.<PollGC>g__PollGCWorker` | 运行时挂起/GC 停靠路径；**含采样器自身挂起造成的放大**（§2.2-5），不能直接当作业务 CPU |
| 16.39% | `WaitForMultipleObjectsEx`（栈：`GLWpfControlRenderer.Render` → `UIElement.Arrange` → `MediaContext.RenderMessageHandlerCore`） | **等待 GL 呈现线程**：每帧同步点 |
| 7.81% | `OpenTK.Wpf.GLWpfControlRenderer.Render` | GL 控件渲染入口（原生工作量另计） |
| 4.35% | `FumenVisualEditorViewModel.<OnEditorRender>b__130_3(Bullet)` → `Enumerable.WhereIterator.MoveNext` → `PooledList.AddRange` | **每帧对 Bullets 的 LINQ `Where` 投影** |
| 4.08% | `Monitor.Enter_Slowpath` | 锁竞争（UI 线程上） |
| 1.33% | `CommonBatchDrawTargetBase.Post` | 绘制对象入桶 |
| 1.15% | `DefaultWaveformDrawing.<Draw>g__applyObjCounting` | 波形绘制内计数 |
| 0.79% | `CompareInfo.IcuGetHashCodeOfString` | 字符串/字典查找（国际化比较） |
| 0.75% | `DrawCommandListContextSlots.Present` | 命令列表提交 |
| 0.67% | `DefaultInstancedLineDrawing.FlushDraw` | 线绘制 flush |
| 0.63% | `DefaultBatchTextureDrawing.Draw` | 纹理批次绘制 |
| 0.62% | `ObjectPool.PooledList<T>.AddRange` | 池化列表填充 |

帧驱动链（源码映射来自只读 Scout 扫描，行号为仓库当前 HEAD）：

```
CompositionTarget.Rendering (WPF, UI 线程)
  └─ OpenTK GLWpfControl.Render                        ← 实测栈叶 GLWpfControlRenderer.Render
      └─ DefaultOpenGLRenderContext.GlView_Render       Kernel/Graphics/OpenGL/DefaultOpenGLRenderContext.cs:85
          └─ FumenVisualEditorViewModel.Render          Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:283
              └─ OnEditorLoop → OnEditorRender          同文件 :269 / :306
                  ├─ 每帧重建可见对象桶（池化列表）      :432-532、EnumerateAllDisplayableObjects :459/:812
                  ├─ drawTargetOrder 循环 → Begin/Post/End(DrawBatch)  :621-655
                  ├─ RecalculateMagaticXGridLines()      :548 / 定义 :952
                  └─ PostDrawCommandList → 渲染队列       :929
Caveat: 预览模式下 ProjectileBatchDrawTargetBase 内部使用 Parallel.ForEach（:363-373），
        其工作项会**内联执行在 UI 线程**（采样里可见 Parallel.ForWorker → TaskReplicator → ExecutionContext）。
```

要点：
- **渲染完全在 UI 线程**（GLWpfControl 在 `CompositionTarget.Rendering` 中同步触发），因此 UI 线程上的任何停顿都会直接变成掉帧/输入延迟。
- 每帧 16.4% 的线程时间停在「等 GL 呈现」，说明同步点（呈现/交换）是 UI 线程的主要阻塞来源；`D3DRenderQueueFrameCount=3` 的队列设计下可考察是否可异步化。
- `OnEditorRender` 中 4.35% 花在 LINQ `Where` over Bullets + `PooledList.AddRange`，是**纯托管热点**，改直写循环可立即收益（§4.4）。

### 3.5 原生渲染线程

3 条原生线程（40004/48984/38996）合计 22.3 s（14.8%），无托管栈。它们与 UI 线程上 16.4% 的 `WaitForMultipleObjectsEx` 相对应，属于 OpenTK/WPF 的呈现/合成路径；本次工具链看不到其内部符号（需内核 ETW + 公开符号），不做进一步归因。

### 3.6 托管侧 CPU 归因（`Managed` 样本近似，全 75 s）

将 `Managed` 样本按 1.867 ms/样本换算，总量 90,198 样本 ≈ 168.4 s（**近似值，偏高约 10%，见 §2.3**）：

| 样本 | ≈CPU 秒 | 方法 |
|---|---|---|
| 33,629 | 62.8 | `WaitHandle.WaitMultiple`（= NAudio 音频线程忙轮询） |
| 16,400 | 30.6 | `Thread.PollGCWorker`（运行时挂起停靠；含采样器放大，非业务 CPU） |
| 7,656 | 14.3 | `LowLevelLifoSemaphore.WaitForSignal`（线程池空闲等待） |
| 5,936 | 11.1 | `NAudio.Wave.WasapiOut.PlayThread`（填充缓冲） |
| 5,490 | 10.2 | `LowLevelLifoSemaphore.ReleaseCore`（线程池唤醒/分发） |
| 4,521 | 8.4 | `Monitor.Enter_Slowpath`（锁竞争） |
| 3,164 | 5.9 | `LowLevelLifoSemaphore.Wait` |
| 2,117 | 4.0 | `Thread.Sleep` |
| 1,747 | 3.3 | `FumenVisualEditorViewModel.<OnEditorRender>b__130_3(Bullet)` |
| 1,145 | 2.1 | `OpenTK.Wpf.GLWpfControlRenderer.Render` |
| 535 | 1.0 | `CommonBatchDrawTargetBase<T>.Post` |
| 463 | 0.9 | `DefaultWaveformDrawing.<Draw>g__applyObjCounting` |

**业务代码自身的托管 CPU 占比很小**：除音频线程外，应用代码方法（`Post`、`AddRange`、`FlushDraw`、`QueryBoundaryXGridUnit`、`OnEditorRender` 等）单项都在 1 s 量级。也就是说，**当前瓶颈不是「某段算法太慢」，而是 ① 音频忙轮询、② 巨量分配导致的 GC/内存子系统开销、③ 同步与线程池调度开销**。

---

## 4. 内存分配

### 4.1 速率

| 指标 | 稳态基线（150 s，计数器） | 采样窗口（75 s，分配事件） |
|---|---|---|
| SOH 分配速率（中位/均值/峰值） | 106.7 / 79.5 / 118.6 MB/s | 116 MB/s（8,705 MB 采样量） |
| LOH 分配 | — | **0 次大对象分配 tick**（窗口内 LOH 无新增） |
| gen0 GC 频率 | 732 次 / 150 s ≈ 4.9 次/s（中位 7 次/s） | 526 次 / 75 s ≈ 7.0 次/s |

按均值计，**每分钟会产生约 4.8 GiB 的短命对象**；这些对象全部由 gen0 GC 回收，是 GC 与内存带宽的主要压力来源。

### 4.2 按类型（75 s，运行时按约 100 KB 分配粒度采样）

| 字节 | MB | 类型 | 说明 |
|---|---|---|---|
| 5,007,181,888 | 4,775.2 | `<>c__DisplayClass9_0` | **Caliburn.Micro 通知闭包**（占 54.9%） |
| 1,172,283,424 | 1,118.0 | `OngekiFumenEditor.Base.TGrid` | 网格时间对象 |
| 937,455,832 | 894.0 | `OngekiFumenEditor.Base.XGrid` | 横向网格对象 |
| 452,414,096 | 431.5 | `OngekiFumenEditor.Base.GridOffset` | 网格偏移 |
| 347,415,320 | 331.3 | `List<ConnectableChildObjectBase>` | `GetChildObjectsFromTGrid` 的 `GetRange` 结果 |
| 345,177,200 | 329.2 | `ConnectableChildObjectBase[]` | 同上（List 内部数组） |
| 64,287,664 | 61.3 | `Replica<RangeWorker>` | `Parallel.ForEach` 复制体 |
| 60,762,848 | 58.0 | `<EnumerateTextureInstances>d__3` | 纹理实例枚举迭代器 |
| 59,376,752 | 56.6 | `Entry<AutomationPeer>[]` | WPF UI Automation 哈希表扩容 |
| 31,873,400 | 30.4 | `RangeValuePair<TGrid,BeamStart>[]` | 可见对象索引数组 |
| 31,023,552 | 29.6 | `AutomationPeer[]` | UI Automation |
| 29,209,248 | 27.9 | `Int32[]` | — |

### 4.3 按分配点（75 s）

| 字节 | MB | 调用点 | 上游调用者（占比最大者） |
|---|---|---|---|
| 2,158,883,976 | 2,058.9 | `TGrid.op_Addition(TGrid, GridOffset)` | `Hold.<CalculateJudgeTGrid>d__28.MoveNext()`（≈ 98%） |
| 2,156,338,088 | 2,056.4 | `GridBase.NormalizeSelf()` | `ConnectableChildObjectBase.CalulateXGrid`（≈ 56%）、`DrawPlayableAreaHelper_new.QueryBoundaryXGridUnit`（≈ 32%）、`ConnectableStartObject.CalulateXGrid` |
| 1,296,170,768 | 1,236.1 | `ConnectableChildObjectBase.CalulateXGrid(TGrid)` | `VisibleLineVerticesQuery.QueryVisibleLineVertices`（100%） |
| 717,103,648 | 683.9 | `DrawPlayableAreaHelper_new.QueryBoundaryXGridUnit(...)` | `DrawPlayableAreaHelper_new.BuildAreaSample` |
| 692,592,520 | 660.5 | `ConnectableStartObject.GetChildObjectsFromTGrid(TGrid)` | `VisibleLineVerticesQuery.QueryVisibleLineVertices`（≈ 98.7%） |
| 444,632,296 | 424.0 | `Hold.<CalculateJudgeTGrid>d__28.MoveNext()` | `DrawHitObjectEffectHelper.Draw` |
| 399,434,440 | 380.9 | `Caliburn.Micro.PropertyChangedBase.NotifyOfPropertyChange(string)` | `GridBase.NormalizeSelf` |
| 181,542,080 | 173.1 | `DrawPlayableAreaHelper_new.ConvertToLimitParam(...)` | `DrawPlayField` |
| 179,731,856 | 171.4 | `DrawPlayableAreaHelper_new.BuildAreaSample(...)` | `DrawPlayField` |
| 82,089,864 | 78.3 | `TaskReplicator+Replica.Execute()` | `Parallel.ForEach` 工作项（线程池派发） |
| 59,270,736 | 56.5 | `HashSet<T>.Resize` | `AutomationPeer.UpdateChildrenInternal` |
| 59,270,448 | 56.5 | `TapDrawingTarget.DrawBatch(...)` | `HoldTapDrawingTarget.Draw` |
| 35,817,864 | 34.2 | `UIElementAutomationPeer.CalculateVisibleBoundingRect` | UI Automation |
| 32,513,000 | 31.0 | `IntervalTree<T>.get_Values()` | `ProjectileBatchDrawTargetBase`（预览模式） |

### 4.4 根因链路（含源码定位）

**A. 通知闭包链（≈ 54.9% 的分配量）**

1. `GridBase.Unit` / `Grid` 的 setter 每次赋值都调用 `NotifyOfPropertyChange(nameof(...))`：
   `OngekiFumenEditor/Base/GridBase.cs:39-59`
2. `NormalizeSelf()` 一次会写 2~6 次属性（`Grid`、`Unit` 交替）：
   `OngekiFumenEditor/Base/GridBase.cs:61-76`
3. Caliburn.Micro（仓库内 vendored 源码）在「有订阅者 + 要求在 UI 线程通知」时分配闭包与事件参数：
   `Dependences/gemini/Dependences/Caliburn.Micro/src/Caliburn.Micro.Core/PropertyChangedBase.cs:46-59`，UI 线程标志来自 `XamlPlatformProvider.cs:58`
4. **订阅者是真实存在的**：`OngekiTimelineObjectBase.TGrid`（`:18`）与 `OngekiMovableObjectBase.XGrid`（`:16`）在网格赋值时注册转发处理器
   `OngekiFumenEditor/Utils/PropertyChangedBaseExtensionMethod.cs:31-37`，形如 `(a,b) => t.NotifyOfPropertyChange(b.PropertyName)`——即对象持有的网格一有变化，就会向上转发通知，从而触发步骤 3 的分配。
5. 现场残留：活跃堆里 `PropertyChangedBase+<>c__DisplayClass9_0` 64,235 个（1.96 MiB）、`PropertyChangedEventHandler` 67,084 个（4.09 MiB），说明这套闭包/委托不只是瞬时的，还有可观存量。

**这条链不能删**（核对结论）：转发上去的 `Unit`/`Grid` 名字确有消费者——`Base/Collections/SoflanList.cs:60-70` 的 `OnSoflanPropChanged` 显式 `case nameof(TGrid.Grid): case nameof(TGrid.Unit):`（使 soflan 位置缓存失效），`Base/Collections/BpmList.cs:51-56` 的注释更把「经转发链上来的 `TGrid.Unit/Grid` 子属性」写进了内容令牌（contentVersion）设计；`BpmList.OnBpmPropChanged` 对任何属性名都会 bump 版本。因此只能**降低这条链的触发次数与单次成本**，不能取消订阅。

**优化方向**（三处独立可落地，细节与代码见报告讨论记录）：

1. `NormalizeSelf()` 改为「直接写字段 + 值真变了才通知」：规范化保持总量不变，只是表示形式归一，常见情况下两次写回值相同 → 通知次数由 2~6 次降到 0~2 次；
2. `Grid`/`Unit` setter 增加变更检测（`if (grid == value) return;`）：`Unit = (int)Unit`、`Grid = Grid % GridRadix` 这类写回同值的调用不再通知。等价的仓库惯用写法是 `if (Set(ref grid, value)) RecalculateTotalValues();`（Caliburn 的 `Set` = 比较-相等即返回 / 写字段 / 通知），但注意 `Set` 会**先发通知再返回 true**，于是 `RecalculateTotalValues()` 落在通知之后——即回调期间 `TotalGrid/TotalUnit` 仍是旧值（现状是重算在前）。当前所有按名字过滤的消费者（`SoflanList`/`BpmList` 只 bump 版本，索引类的 `rebuildProperties` 只含对象级名字）都不读总量，故风险低，但这条「通知时派生字段已一致」的隐式不变量会被打破，采用时应在代码里注明；
3. 在 `GridBase` 覆写 `NotifyOfPropertyChange(string)`：用两个静态缓存的 `PropertyChangedEventArgs`，UI 线程上直接 `OnPropertyChanged(args)`，非 UI 线程回退 `base.NotifyOfPropertyChange(...)` 以保留 Caliburn 的派发语义 → 单次通知从「闭包 + delegate + EventArgs」三个分配降到零分配。

> 三条都只改 `OngekiFumenEditor/Base/GridBase.cs` 一个文件，语义与消费者契约不变；**不要**去改 `Dependences/gemini` 下的 vendored Caliburn（子模块）。

> **落地记录（2026-09-29）**：阶段 1 已实现——新增 `OngekiFumenEditor/Utils/CommonPropertyChangedBase.cs`（按名复用 args + 无闭包发布路径）并接入 `GridBase`（变更检测 + `NormalizeSelf` 单次通知）。benchmark 实测单笔通知分配 **120 B → 0 B**、`NormalizeSelf` 已规范化路径 480 B → 0 B、真实规范化 600 B → 0 B；设计与数据见 `docs/common-propertychanged-base-design.md`。附带的实现经验：**热路径方法体内不能含捕获型 lambda**（实测每调用 ~32 B，含 Caliburn 自身方法），派发闭包须拆到 `NoInlining` 冷方法。

**B. 网格算术分配（≈ 35%）**

- `TGrid.op_Addition` `new TGrid(...)`：`Base/TGrid.cs:60-89`（≈2.06 GiB / 75 s）
- `XGrid` 同类：`Base/XGrid.cs:24-49`；`FromTotalGrid/FromTotalUnit` 里也是「构造 + `NormalizeSelf`」
- `ConnectableChildObjectBase.CalulateXGrid` 每次 `new XGrid` + `NormalizeSelf`：`Base/OngekiObjects/ConnectableObject/ConnectableChildObjectBase.cs:320-328`，调用方是 `VisibleLineVerticesQuery.QueryVisibleLineVertices` 的「每子对象 × 每 soflan 采样点」双层循环（`:69-88`）
- `ConnectableStartObject.GetChildObjectsFromTGrid` 用 `children.GetRange(...)`/`new List` 产出临时列表：`ConnectableStartObject.cs:358-381`——**同一文件已有零分配版本 `TryGetValidPathChildRange`（`:387-433`）未被该路径使用**
- `Hold.CalculateJudgeTGrid` 迭代器每次产出一个 `GridOffset`：`Base/OngekiObjects/Hold.cs:164,170,180,194`，由 `DrawHitObjectEffectHelper.Draw` 逐个可见 Hold 调用

**C. Playable Area 采样（≈ 12%）**

- `DrawPlayableAreaHelper_new.DrawPlayField`（`:215-262`）→ 采样循环（`:267-303`、`:425-476`）→
  `BuildAreaSample`（`:481`）里 `TGrid.FromTotalGrid`（`:483`）、`QueryBoundaryXGridUnit`（`:494`）、`ConvertToLimitParam`（`:640`）
- 这些 `TGrid/XGrid` 都是「算完即弃」的中间量，天然适合实例复用或改结构体。

**D. 其他**

- `TapDrawingTarget.DrawBatch`（`:192-210`）经 `Kernel/Graphics/DrawCommandListBuilderTextureExtensions.cs:25-28` 的 `EnumerateTextureInstances` 迭代器分配 58 MB/75 s。
- 预览模式 `ProjectileBatchDrawTargetBase` 的 `Parallel.ForEach`（`:363-373`）除了 `Replica<RangeWorker>`（61 MB/75 s），还伴随线程池派发（`LowLevelLifoSemaphore.ReleaseCore` 在 CPU 表中排第 5）。
- 每帧 `EnumerateAllDisplayableObjects`（`Drawing.cs:459/812`）与 `RecalculateMagaticXGridLines`（`:548/952`）虽使用池化列表，但其中查询产出的 `RangeValuePair[]`（30 MB/75 s）与 `Int32[]`（28 MB/75 s）仍会分配。

### 4.5 UI Automation 相关分配（≈ 1.3%，但可避免）

`Entry<AutomationPeer>[]` 56.6 MB + `AutomationPeer[]` 29.6 MB + `UIElementAutomationPeer.CalculateVisibleBoundingRect` 34.2 MB，全部来自 WPF Automation peer 树维护（`AutomationPeer.UpdateChildrenInternal`、`WindowAutomationPeer.GetNameCore` 出现在 UI 线程样本里）。这与 `EnableMcpServerInGUIMode=true`（MCP/UIA 集成）等外部自动化客户端的存在相符；若无人使用自动化接口，关闭相应集成即可省掉这部分分配与 UI 线程工作。

---

## 5. GC 情况

### 5.1 频次 / 代际 / 原因（75 s 窗口）

| 指标 | 值 |
|---|---|
| GC 总数 | **546 次 / 75 s ≈ 7.3 次/s** |
| 分代 | gen0 526、gen1 19、gen2 1 |
| 类型 | 非并发 545、后台（BackgroundGC）1 |
| 原因 | **100% `AllocSmall`**（分配压力），0 次 `Induced`（即无显式 `GC.Collect`） |
| 稳态基线（150 s）对照 | gen0 732、gen1 14、gen2 2；暂停合计 0.83 s（0.55%） |

### 5.2 暂停（阻塞式 GC 的 GCStart→GCStop 窗口）

| 指标 | 值 |
|---|---|
| 阻塞式 GC 暂停总时长 | **442.3 ms / 75 s = 0.59%** |
| p50 / p90 / p95 / p99 / max | 0.79 / 0.91 / 0.97 / 1.08 / **1.87 ms** |
| EE 挂起窗口（SuspendEE→RestartEE，仅计入 GC） | 545 次，合计 459.4 ms，p50 0.83 ms，max 1.99 ms |
| 后台 GC | 1 次，其 Start→Stop 窗口 45.1 ms（含并发标记，非停顿） |

### 5.3 堆大小稳态（GCHeapStats，546 个采样点）

| 代 | min | 中位 | max |
|---|---|---|---|
| 总计 | 508.3 | 508.5 | 512.5 MB |
| gen2 | 98.2 | 98.2 | 98.2 MB |
| LOH | 409.9 | 409.9 | 409.9 MB |
| POH | 0.1 | 0.1 | 0.1 MB |

整个窗口内 LOH/gen2 完全不变，说明**内存处于稳态、没有泄漏**，但也说明 **LOH 中 410 MB 的空间被长期占据且不回收**（见 §6）。

### 5.4 结论

- **GC 停顿不是当前性能问题**（0.59%，最大 1.87 ms）。
- 真正的成本在于 **7 次/s 的 gen0 GC** 及其背后的 112 MB/s 分配：每次 gen0 都要执行一次「暂停 + 晋升扫描」，由 §4 的分配模式直接驱动。
- 若把分配量削减 50–70%（§4.4 的 A、B 两条），gen0 频率预计从 ~7 次/s 降到 2–3 次/s，同时释放线程池/分配器上的内核态 CPU。

---

## 6. 托管堆组成、LOH 与碎片

### 6.1 SOS 段视图（`eeheap -gc`）

| 区域 | 内容 | 数量 | 已分配（allocated） |
|---|---|---|---|
| SOH gen0 | 1 段 | 1 | 3.62 MiB |
| SOH gen1 | 1 段 | 1 | 0.26 MiB |
| SOH gen2 | 分段（每段 4 MiB） | **26** | **98.73 MiB** |
| **LOH** | 大对象段（14–51 MiB/段） | **13** | **409.93 MiB** |
| POH | 固定对象堆 | 1 | 0.12 MiB |
| NonGC heap | 运行时段 | 1 | 2.79 MiB |
| **合计（GC Allocated Heap Size）** | | | **540,487,040 B = 515.4 MiB** |
| GC Committed Heap Size | | | 550,027,264 B = 524.6 MiB |

> 各代的 allocated 值与同窗口 `GCHeapStats` 读数一致（gen2 98.7 / LOH 409.9 / POH 0.1 MiB），也与计数器 `dotnet.gc.last_collection.heap.size` 完全吻合。

### 6.2 活跃 vs 空闲（关键）

| 指标 | 值 | 来源 |
|---|---|---|
| 对象总数 | 1,562,661 | `dumpheap -stat` |
| 对象占用合计 | 539,817,742 B = 514.8 MiB | 同上 |
| 其中 `Free` 对象 | **343,886,016 B = 327.9 MiB（63.7%）** | 同上 |
| **活跃对象合计** | **195,931,726 B = 186.9 MiB** | 相减 |
| 交叉验证 | GCDump「GC Heap bytes」= 191,989,269 B = 183.1 MiB | `heap-report.txt` |

即：**GC 堆里近三分之二是空闲/碎片空间**，其中 LOH 碎片约占 308 MB（计数器 `dotnet.gc.last_collection.heap.fragmentation.size[loh]` 中位 309.4 MB），gen2 碎片 18.3 MB。

### 6.3 活跃对象类型分布（GCDump + SOS 合并）

| 类型 | 活跃大小 | 数量 | 说明 |
|---|---|---|---|
| `System.Byte[]` | 86.4 MB | 5,035 | 单个最大 **50.7 MiB**（独占一个 LOH 段）；另有 30 个 > 1 MB |
| `System.Single[]` | 17.2 MB | **138,613** | 与 `PeakPoint` 数量几乎一致 → 波形峰值缓存 |
| `System.String` | 13.0 MB | 237,379 | — |
| `System.Xml.Linq.XElement` | 11.5 MB | **189,102** | 谱面 XML DOM 残留 |
| `System.Xml.Linq.XText` | 5.7 MB | **125,020** | 同上 |
| `OngekiFumenEditor.Kernel.Audio.PeakPoint` | 4.2 MB | 138,560 | 波形峰值 |
| `PropertyChangedEventHandler` | 4.1 MB | 67,084 | §4.4-A 的订阅者 |
| `ValueTuple<Vector2,Vector2,Single,Vector4>[]` | 2.5 MB | 275 | 纹理批次顶点 |
| `Caliburn.Micro.PropertyChangedBase+<>c__DisplayClass9_0` | 2.0 MB | 64,235 | §4.4-A 的闭包残留 |
| `Bullet` | 1.4 MB | 13,758 | 弹幕对象 |
| `TGrid` / `XGrid` | 1.4 / 1.2 MB | 25,537 / 22,683 | 存活网格 |
| `PeakPoint[]` | 1.1 MB | 2 | — |

### 6.4 LOH 与碎片解读

- 窗口内 **LOH 分配 tick 为 0**，说明这 410 MiB 不是当前帧循环新造的，而是**更早阶段（谱面/音频/纹理/顶点缓冲初始化）分配后留下的段**；大对象一旦不再使用，LOH 不会移动存活对象去填洞（除非开启 LOH 压缩）。
- 已知的一次性大缓冲来源（Scout 源码定位）：OpenGL 固定顶点缓冲（`DefaultPolygonDrawing.cs:15-17` `float[300000*6]` ≈ 7.2 MB、`DefaultInstancedLineDrawing.cs:56`、`DefaultBatchTextureDrawing.cs:49` 等）、字形图集 `byte[AtlasSize*AtlasSize*4]` = 4 MB（`SkiaGlyphAtlas.cs:234`，满了会整体重建）、整曲音频解码/峰值缓冲（`Kernel/Audio/Utils/MethodExtensions.cs:32,62`、`DefaultSamplePeak.cs:31`）、波形烘焙离屏表面（`AudioPlayerToolViewerViewModel.WaveformBlocks.cs:270-275`）。
- 处置建议（按性价比）：
  1. 大缓冲统一走 `ArrayPool<byte>/<float>`（仓库已有 `ObjectPool` 设施）或复用纹理/顶点缓冲，避免「释放旧段 → 新段再申请」造成的洞；
  2. **已落地（2026-09-29）**：加载流程末尾新增步骤 `EditorLoadingStep.CollectingMemory`（对话框显示「回收内存…」+「6 / 6」），在首帧渲染完成后执行一次
     `GCSettings.LargeObjectHeapCompactionMode = CompactOnce; GC.Collect();`（落点：`FumenVisualEditorViewModel.LoadInternalAsync` → `EditorLoadingSession.CollectLoadingGarbageAsync`）。
     **仅在加载路径**，绝不要放进渲染循环。
     效果口径修正（隔离进程小样实测：100 MB 死对象在 LOH 留下 96 MB 碎片）：
     - **碎片会被压到 0**（96 MB → 0 MB）：之后的分配复用空洞，不再为此长堆；
     - 但**已提交内存只返还「整段腾空」的那部分**（同一次实测 `TotalCommittedBytes` 仅 198.7 → 177.6 MB，−21 MB）。
     本进程 LOH 是 13 个 14–51 MiB 的段、存活仅 ~100 MB，压完预计能空出多段，实际工作集下降量需重启后复测（上限为实测空闲量 ~328 MiB，验收指标见 §9.1 的计数器方法：`heap.fragmentation.size[loh]` 应 < 20 MB）；
  3. 查明那个 50.7 MiB 的 `Byte[]` 是否仍在被需要（疑似整曲解码缓冲或音频镜像）。

---

## 7. 其他测量发现

| 项目 | 实测 | 备注 |
|---|---|---|
| 锁竞争 | 87 次/s（150 s 共 12,998），UI 线程 `Monitor.Enter_Slowpath` 占其样本 4.08% | 与线程池派发、波形/音频同步相关 |
| 线程池工作项 | 1,770 个/s（150 s 共 265,332），队列长度中位 0 | 主要为 `Parallel.ForEach` 预览批次与音频回调；`LowLevelLifoSemaphore.ReleaseCore` 是 CPU 表第 5 位 |
| 异常 | **0 次**（`ExceptionStart`=0） | 无异常驱动控制流问题 |
| JIT | 计数器中位 0 方法/s，峰值 66 方法/s | 预热基本完成 |
| 程序集数 | 421 | — |
| 工作集/私有内存 | 约 1.0–1.05 GiB / 约 872 MiB（窗口内几乎不变） | 与 GC 堆 515 MiB 的差额来自原生（WPF/GL/Skia/音频）与 JIT |

---

## 8. 优化建议（按投入产出排序）

> **实施进度（2026-09-29）**：
> - ✅ P1-①「`GridBase` 属性通知零分配」→ 单笔通知 120 B → 0 B（`docs/common-propertychanged-base-design.md`），另已把 `OngekiObjectBase`（全部物件）接入；
> - ✅ P1-②「边界求值零分配」→ 新增 `TryGetChildObjectFromTGrid`/`TryCalulateXGridTotalUnit` 并切换 3 处热路径，benchmark per 边界点：边界求值 56 B → 0 B、子物件定位 64 B → 0 B；
> - ✅ P3「加载末尾 LOH 压缩」→ 加载流程新增步骤 `CollectingMemory`，碎片 → ~0（工作集返还量待真机复测）；
> - ✅ P2「Hold 判定刻度枚举零分配」→ `GridOffset` 改 `readonly record struct` + `TGrid.AddOffset` 原地推进（迭代用私有累计实例，`Hold.TGrid` 不被别名；yield 点才复制 TGrid）。benchmark `HoldJudgeTickEnumerationBenchmarks`（6 窗口 × 3 ProgJudgeBpm 位级等价对拍）：预览窗口单次枚举 **32,528 B → 456 B**（−98.6%）、**12.37 µs → 6.80 µs**；全量枚举 89,728 B → 62,840 B。**本次仅落 WPF 树**；
> - ✅ P2「可击打区域采样链路零分配」→ `DrawPlayableAreaHelper_new` 用私有 `sampleTGrid`（IsNotifying=false）替代每采样 `TGrid.FromTotalGrid`（`BuildAreaSample`/`ConvertToLimitParam`/`AddScreenDistanceSamples` 三处）；有效路径区间查找失败时的兜底与无效路径求值改 `TryCalulateXGridTotalUnit` + `ChildCount`/`GetChildObjectAt` 索引扫描（不再 `GetChildObjectsFromTGrid`/`CalulateXGrid`）。benchmark `PlayfieldAreaSamplingAllocationBenchmarks`（位级对拍 + 反射直调已落地私有方法）：采样链 **112 B/采样 → 0 B**、相邻对 **112 B → 0 B**、无效路径 **208 B/采样 → 0 B**。另注：`total / (double)TGrid.DEFAULT_RES_T` 与 `TotalUnit` 经 300 万取样对拍**不位级等价**（3346 处差异），故未采用 `ConvertToViewRelativeY` 的 double 重载。**本次仅落 WPF 树**；
> - ✅ P2「预览模式子弹/Bell 分桶查询去 LINQ」→ `OnEditorRender` 每帧把「当前时间之后」的子弹/Bell 按 soflan group 可见性过滤后分桶：旧实现是 `BinaryFindRange(yield).Where(...)` 惰性管道 + 每 target 重新枚举，改为 `BinaryFindRangeIndex` + 索引循环（谓词内联）直写目标桶（新私有 `AddVisibleTGridRangeObjectsInto`），并顺带清掉块内恒真内层 `if (IsPreviewMode)` 与死 `AsEnumerable()` 初始化。benchmark `BulletBellQueryBenchmarks`（真实 `TGridSortList`/`GlobalCacheSoflanGroupRecorder`(SetCache+Freeze)/`IndividualSoflanAreaListMap`/`ObjectPool`，含逐元素等价校验）：真实 target 数 T=1 下 **0.58–0.80×**（10k 可视子弹 258 → 150 µs/帧），每帧分配 **208–264 B → 0**。另测两种加强形态均未采纳：一次扫描 + 帧内池化列表复用（T=1 与已落地形态持平，T≥2 才到 0.32×）、再加 group 可见性帧内缓存（实测更慢）。**本次仅落 WPF 树，且不移植到 Avalonia（2026-09-29 决定）**；
> - ⚠️ **回归修复（2026-09-29）「通知快速路径丢失订阅者短路 → 与渲染期 `Parallel.ForEach` 死锁」**：`CommonPropertyChangedBase` 去掉了 Caliburn 的 `PropertyChanged != null` 短路，导致**无订阅者**的跨线程通知也会同步 `Dispatcher.Invoke`；预览模式下每颗子弹在 `ProjectileBatchDrawTargetBase.DrawPreviewMode` 的 `Parallel.ForEach`（UI 线程调用并阻塞等待）里新建 `XGrid` → worker 等 UI 线程、UI 线程等 worker → 进程假死（音频线程独立仍在播放）。现场证据：主线程 `Task.InternalWait`/`Parallel.ForEach`、worker `XGrid..ctor → DispatchToUIThread → Dispatcher.Invoke`（`F:\perf-artifacts\ongeki-20260929\hang-stacks.txt`）。修复 `d34773d2`：跨线程分支改走基类实现（自带订阅者短路），UI 线程仍走零分配缓存 args 路径；
> - ⏳ P0（WASAPI 忙轮询，约 1 个核）与其余 P2/P3 项待做；
> - ⏳ 所有已落地项的**真机 trace 复测**（重启应用后按 §9.1）与视觉回归仍待执行。

| 优先级 | 措施 | 预期收益 | 验证方式 |
|---|---|---|---|
| **P0** | 修复 WASAPI latency=0（`NAudioManager.cs:115`）：传非 0 latency，或改用事件同步正确实现/升级 NAudio | 释放 ≈ 1 个物理核（进程 CPU −45%） | 复测该线程 CPU 应 ≈0；进程 CPU 降到 ~1 核 |
| **P1** | 消除 `GridBase` 属性通知分配（`NormalizeSelf` 静默写 + 变更检测 + 无闭包通知实现；转发链本身保留）——**已落地（阶段 1）** | 实测单笔通知 120 B → 0 B、`NormalizeSelf` 480/600 B → 0 B；预期 gen0 GC 7 次/s → 2–3 次/s（待重启后 trace 复测） | `docs/common-propertychanged-base-design.md` §5 的 benchmark 表；运行时复测见该文 §6.4 |
| **P1** | `VisibleLineVerticesQuery`/`CalulateXGrid` 零分配：新增 `TryGetChildObjectFromTGrid`（复用 `TryGetValidPathChildRange`/`GetChildObjectAt`）、`(Child\|Start).TryCalulateXGridTotalUnit`（返回值、不构造 XGrid），热路径 3 处（`VisibleLineVerticesQuery`、`DrawHitObjectEffectHelper`、`DrawPlayableAreaHelper_new`）已切换；`IsPathVaild()` 去 LINQ、`Children` 循环改索引 —— **已落地** | benchmark（`LaneBoundaryXGridQueryBenchmarks`，per 边界点）：边界求值 **56 B → 0 B**、子物件定位 **64 B → 0 B**（耗时 −19%~−58%）；对应 App 侧站点（1,236 MB + 660 MB / 75 s ≈ 总分配 21.8%）预期归零，待重启后 trace 复测 | benchmark `--filter *LaneBoundaryXGridQueryBenchmarks*`（含逐点等价性校验，发现并复刻了旧 `(int)totalGrid` 截断语义，故数值逐位一致） |
| **P2** | `DrawPlayableAreaHelper_new` 采样循环复用 `TGrid/XGrid` 实例（或结构体化） —— **已落地（2026-09-29，见上方进度；仅 WPF 树）** | 原估 −1 GiB/75 s 含 P1② 已修的边界求值（683.88 MiB）；本次实得 TGrid 物化两 leaf（171.41+173.13 MiB/75 s）+ 无效路径回退（实测 208 B/采样 → 0） | benchmark `PlayfieldAreaSamplingAllocationBenchmarks`；真机 trace 复测待执行 |
| **P2** | `OnEditorRender` 中对 Bullets 的 LINQ `Where` 改直写循环 —— **已落地（2026-09-29，见上方进度；仅 WPF 树）**；帧内列表复用与 group 可见性缓存两形态经实测未采纳 | 实测该块降到 **0.58–0.80×**（10k 可视子弹 258 → 150 µs/帧、1k 子弹 17.7 → 14.2 µs），每帧分配 **208–264 B → 0**；原估「UI 线程时间 −4.35%」应修正为「该块自身约 −20~40%，折算 UI 线程 ≈ −1~2%」 | benchmark `BulletBellQueryBenchmarks`（`--filter *BulletBellQueryBenchmarks*`，含逐元素等价校验）；真机 trace 复测待执行 |
| **P2** | `Hold.CalculateJudgeTGrid` 的 `GridOffset` 改为 `struct` 或复用 —— **已落地（2026-09-29，见上方进度；仅 WPF 树）** | 分配 −424 MB/75 s（benchmark 实测预览窗口单次枚举 32,528 B → 456 B） | benchmark `HoldJudgeTickEnumerationBenchmarks`；真机 trace 复测待执行 |
| **P3** | LOH：大缓冲池化 / **加载后一次性 LOH 压缩（已落地：加载步骤 `CollectingMemory`）** | 碎片 300 MB → ~0；工作集返还量取决于整段腾空（实测小样：96 MB 碎片 → 0，committed −21 MB），需重启后复测 | `dotnet.gc.last_collection.heap.fragmentation.size[loh]` 应 < 20 MB；`eeheap -gc` 中 LOH 段数与空闲量下降 |
| **P3** | 排查 XML DOM（189k `XElement`）是否可在解析后释放 | 活跃堆 −17 MB | GCDump 类型表 |
| **P3** | 确认 UI Automation 客户端是否必要（MCP/UIA 集成） | 分配 −85 MB/75 s，减少 UI 线程工作 | 采样里 `AutomationPeer*` 消失 |
| **P3** | 排查 87 次/s 的锁竞争与每帧 `Parallel.ForEach` 调度开销（预览模式） | 减少线程池/内核态 CPU；**另：该 `Parallel.ForEach` 由 UI 线程调用并阻塞等待，任何在其中触发的跨线程同步派发都会与之互锁**（2026-09-29 已修一例：`CommonPropertyChangedBase` 缺订阅者短路；若将来有*有订阅者*的对象在该并行体内被通知，仍会死锁） | 计数器 + `LowLevelLifoSemaphore.*` 样本下降 |

---

## 9. 复现步骤与原始数据清单

### 9.1 复现

```powershell
# 0) 打开编辑器并加载谱面，让其保持稳态渲染
# 1) 干净基线（无采样器）
dotnet-counters collect -p <PID> --counters System.Runtime --format csv -o counters-runtime.csv --refresh-interval 1 --duration 00:00:02:30
# 2) 线程时间采样 + GC + 分配点（EventPipe，无需提权）
dotnet-trace collect -p <PID> -o render.nettrace --duration 00:00:01:15 `
  --providers Microsoft-DotNETCore-SampleProfiler:0x0000F00000000000:4,Microsoft-Windows-DotNETRuntime:0x41008019:5
# 3) 托管堆对象图（会触发一次 GC）
dotnet-gcdump collect -p <PID> -o heap.gcdump -t 120
# 4) 堆段/代/存活（会短暂暂停进程）
dotnet-dump collect -p <PID> --type Heap -o heap.dmp
dotnet-dump analyze heap.dmp -c "eeheap -gc" -c "clrthreads" -c "dumpheap -stat" -c "exit"
# 5) 分析
PerfToolkit analyze render.nettrace --pid <PID> --top 30 --threads <热点TID列表> --md render-report.md
```

说明：
- 提供者掩码 `0x41008019` = `GC | Loader | Jit | Exception | GCHeapAndTypeNames | Stack`，级别 `Verbose(5)` 才能收到 `GCAllocationTick` 与类型名。
- 内核 ETW CPU 采样需要管理员权限，本机未使用；若在提权环境测量，可另加 `/ThreadTime` 做 CPU/墙钟区分。

### 9.2 原始数据（`F:\perf-artifacts\ongeki-20260929\`）

| 文件 | 说明 |
|---|---|
| `counters-runtime.csv` | 150 s System.Runtime 计数器（基线） |
| `render.nettrace` | 75 s EventPipe 追踪（采样 + GC + 分配） |
| `render-report.md` / `.txt` | 完整分析报告（CPU/分配/GC/逐线程明细） |
| `heap.gcdump` / `heap-report.txt` | 托管堆对象图与类型表 |
| `heap.dmp`（1.03 GB）/ `sos-gc.txt` / `sos-dumpheap.txt` | 堆段、代、存活直方图（可删，需要时可重采） |
| `threads-t0.txt` / `threads-t1.txt` | 追踪窗口前后的逐线程 CPU 快照 |
| `sample-cpu.ps1` / `sample-threads.ps1` | 采集脚本（进程/线程 CPU、内存、状态快照） |
| `toolkit/` | 本次使用的 TraceEvent 分析器源码（一次性工具，未纳入仓库） |
| `bench-boundary.txt` | `LaneBoundaryXGridQueryBenchmarks` 输出（边界求值/子物件定位，56 B/64 B → 0 B） |
| `bench-phase1-short.txt` | `GridNotificationAllocationBenchmarks` 输出（通知 120 B → 0 B） |
| `notify-sites.md` | 按分配类型过滤的通知站点明细（`--alloc-type DisplayClass9_0`） |
| `bench-bulletbell-repo.txt` | `BulletBellQueryBenchmarks` 输出（out-of-process，仓库工具链，24 案例：旧 LINQ 管道 vs 已落地直写循环 vs 两种未采纳形态） |
| `bench-bulletquery-fine.txt` | 同 benchmark 的 12 迭代复测（InProcessEmit，用于压低噪声；含 L1/L2 对照） |
| `bullet-query-bench-2026-09-29.md` | 该优化项的评估记录（保真度说明、结果表、L1/L2 取舍理由） |
| `allocprobe/` | 隔离进程探针（物件通知、表达式重载、对话框步骤联动、LOH 压缩效果的一次性验证） |

> 复现提示：`heap.dmp` 体积较大，如无后续深入分析需求可删除；其余文件合计 < 100 MB。

---

## 10. 与 Avalonia 树的关系（已核对的有限结论）

依据 `Avalonia/WPF-MIGRATION-SYNC.md`（同步点 `8b2940785`）核对，本报告的热点在两棵树的**可移植性不同**，不要整段照搬：

| 发现 | 在 `Avalonia/src/OngekiFumenEditor.Avalonia/` 的现状 | 可移植性 |
|---|---|---|
| NAudio `WasapiOut` 忙轮询 | 树内 **无 NAudio/WasapiOut 用法** | 仅 WPF 侧问题 |
| `GridBase.Unit/Grid` setter 每次 `NotifyOfPropertyChange` → Caliburn 闭包 | `Base/GridBase.cs` 同样每次 set 通知，但基类是 **CommunityToolkit.Mvvm `ObservableObject`**（`OnPropertyChanged()`，无 UI 线程闭包派发） | 通知频率问题相同；「闭包占 54.9%」是 WPF/Caliburn 特有 |
| `TGrid/XGrid.op_Addition`、`CalulateXGrid`、`GetChildObjectsFromTGrid` 分配 | 同名文件均在（`Base/TGrid.cs`、`Base/XGrid.cs`、`ConnectableChildObjectBase.cs`、`ConnectableStartObject.cs`） | 同一套代码，可直接受益 |
| `VisibleLineVerticesQuery` 里的每子对象 `CalulateXGrid` 调用 | `Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/VisibleLineVerticesQuery.cs` 存在 | 可直接受益 |
| `DrawPlayableAreaHelper_new` 采样分配 | 该文件在 Avalonia 树**不存在** | 仅 WPF 侧（或属待同步内容） |

> 本节的跨树判断只覆盖上述文件的存在性与基类差异，未在 Avalonia 侧做运行时测量；如需结论，应在 Avalonia 版本上重复 §9.1 的流程。

---

## 11. 未决问题（需进一步验证）

1. **音频线程忙轮询的最终修法**：需确认把 latency 改为 100–200 ms 后既不爆音、也不再有 0 超时自旋（NAudio 2.3.0 在部分共享模式设备上仍可能从 `StreamLatency` 取到 0）。
2. **`PollGCWorker` 的真实占比**：其样本量被采样器自身的挂起机制放大，建议在无采样器的窗口用计数器 `dotnet.gc.pause.time` 与线程状态采样复核。
3. **原生渲染线程（40004/48984/38996）内部构成**：需内核 ETW + 微软符号服务器才有符号，未在本轮范围内。
4. **`IntervalTree.get_Values()` 与 `RangeValuePair[]`**：仅在预览模式批量弹幕路径出现，正式模式未复现，需按使用模式分别测量。
5. **LOH 碎片形成时点**：本轮窗口内 LOH 零分配，碎片应形成于「打开谱面/加载音频/烘焙波形」阶段，需在加载路径上再做一次短追踪确认。
6. **测量工具自身的残留影响**：取 `gcdump` / `heap.dmp` 之后，进程工作集由 ~1.03 GiB 升至 ~1.19 GiB（07:08 观测）且未即时回落。若要在同一会话上做前后对比，建议剔除 dump 类操作，或取完 dump 后重启应用再复测。
