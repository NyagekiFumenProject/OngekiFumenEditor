# WPF ↔ Avalonia 迁移同步基线（唯一权威记录）

> 本文件只回答一个问题：**Avalonia 项目当前同步到 WPF 项目的哪个 commit？**
>
> 任何「WPF 与 Avalonia 的功能/内容差异、迁移缺口」分析，动手前先读本文件取基线；
> 做完一次同步或一次对比之后，必须回写本文件。基线记错 = 整份差异分析结论失真。

- 更新日期：2026-09-29
- 仓库：`F:/Source/OngekiFumenEditor`（WPF 与 Avalonia 同处一个 git 仓库，真实 gitdir 在仓库根）
- 两棵树：`OngekiFumenEditor/`（WPF 原项目）、`Avalonia/`（移植项目）

## 1. 当前基线

| 字段 | 值 |
| --- | --- |
| **WPF 最后已同步 commit** | `8b2940785e9b16b450e70fe248cfd16fa1d89040` — "Update Readme.md" |
| 同步日期 | 2026-09-19 |
| 同步方式 / 合入提交 | `b26b79118f6c664ab6d113fdf5ccac9a3fba71ab` — "Merge remote-tracking branch 'origin/master' into avalonia12"（第二父即 `8b2940785`） |
| 分叉基线（初始移植来源） | `a00e90ab4980079875df3532fcbdc7d5a8a16373` — "fix crash"，初始移植提交 `6d452ce16` 的父提交（2026-01-06） |
| 上一次对比分析报告 | `Avalonia/docs/wpf-master-to-avalonia-migration-review-2026-09-19.html`（报告内三元组：本地 master `a00e90ab4` / origin/master `8b2940785` / HEAD `1b63794eb`） |
| 最近一次反向分析报告（Avalonia → WPF 回移候选） | `Avalonia/docs/avalonia-to-wpf-editor-render-backport-review-2026-09-29.md`（2026-09-29；分类计数 **A 可直接回移 12 / B 需适配 3 / C 不适用 9 / D Avalonia 侧未落地 4**；落地记录见该报告 §9：**A1–A4、A6–A12、B1、B2 已回移，A5、B3 不做**） |
| 更早的对照报告 | `Avalonia/docs/wpf-master-origin-master-comparison-2026-09-03.html`（合并基点 `a00e90ab4980`，264 提交） |
| 待同步 | 同步点之后 WPF 树有 84 个路径的差异，其中 16 个提交只改 WPF 树、Avalonia 未跟进（见 §3） |

> 注意区分：**分叉基线 `a00e90ab4`** 是移植起点（2026-01-06，Avalonia 树由此复制），
> **同步点 `8b2940785`** 是最后一次把 WPF `origin/master` 整体合入 Avalonia 线的位置（2026-09-19）。
> 两者相差 264/265 个上游提交，不要混用。

## 2. 分支现状（2026-09-29）

- 本地 `master` == `origin/master` == `c9703670`；2026-09-29 02:10 `master` 被 fast-forward 到 Avalonia 线
  （`git reflog show master`：`93bb119d master@{...}: merge avalonia: Fast-forward`）。
  因此现在**一个分支里同时装 WPF 与 Avalonia 两棵树**，上游 `8b2940785` 当时还没有 `Avalonia/` 目录
  （`Avalonia/` 由 `6d452ce16` 引入）。
- 历史分支：`origin/avalonia` = `93bb119d`，`origin/avalonia12` = `fad7319db`，均已并入 master 线。

## 3. 待同步清单（`8b294078` 之后只改 WPF 树、`Avalonia/` 命中 0，共 16 个提交）

按提交时间从旧到新：

| commit | 主题 |
| --- | --- |
| `f20d4766` | refactor(graphics): abstract Skia canvas access behind ISkiaRenderContext |
| `bde6f0eb` | feat(graphics): add backend-independent offscreen rendering |
| `b7c76dc3` | fix(exit): terminate render backends before any await in OnExit |
| `8276152f` | refactor: align file names with declared types (core naming mismatches) |
| `ea56b1c0` | refactor: rename files copied from FumenMetaInfoBrowser to match their types |
| `3d265914` | refactor: align remaining file names (casing, plurality, module entry files) |
| `44ae8e6b` | refactor: fix MenuDefintions -> MenuDefinitions typo in OptionGeneratorTools |
| `158dc00a` | refactor: rename AudioAdjustWindow module entry file to match its class |
| `0703634c` | docs(graphics): rewrite the new offscreen comments and messages in plain English |
| `2fbb15c8` | refactor(graphics): drop vestigial [Serializable] from DefaultOpenGLTexture |
| `9023f9a6` | refactor: name the module menu holders MenuDefinitions and restore their file names |
| `efa82af9` | refactor(waveform): extract the waveform polyline geometry into WaveformGeometry |
| `b836e990` | feat(waveform): add a drawWaveform flag to IWaveformDrawing.Draw |
| `b733016b` | feat(waveform): pre-render the waveform into offscreen blocks and reuse them while panning |
| `79cf276f` | fix(graphics): keep offscreen waveform blocks opaque and accumulate alpha linearly |
| `9290c4d8` | refactor(graphics): render OpenGL text with a Skia rasterized glyph atlas |

另有两个提交同时动了两棵树，不构成待同步内容：`535fbc630`（删 `Polyline2D.CSharp` 子模块）、
`fe73136f`（文档整理，含 `Avalonia/docs/`）。

范围依据（供复核）：

- `docs/offscreen-render-context-design.md` 首行：「目标工程：`OngekiFumenEditor`（WPF）——**Avalonia 侧不在本次范围内**」。
- `docs/naming-audit.md` §三「Avalonia 侧（待处理）」：A 17 + B 6 + C 4 共 27 处；
  抽查证实 WPF 已是 `Kernel/Graphics/IImage.cs`、`Utils/Attributes/MapToViewAttribute.cs`，
  Avalonia 仍是 `ITexture.cs`、`MapToViewAttubute.cs`。

## 4. 复核命令（怀疑本文件过期时先跑这些）

```bash
cd F:/Source/OngekiFumenEditor
git ls-remote origin refs/heads/master                    # 上游 master 是否已前进
git log --oneline <lastSyncCommit>..HEAD -- OngekiFumenEditor/     # 同步点之后 WPF 树上的提交
git diff --name-status <lastSyncCommit>..HEAD -- OngekiFumenEditor | wc -l   # 路径数（2026-09-29 时为 84）
git show --name-only --format= <commit> | grep -c '^Avalonia/'     # 该提交是否已同步到 Avalonia 侧
git show -s --format='%h %ad %s%n%p' --date=iso <mergeCommit>      # 同步 merge 的第二父 = 同步点
```

## 5. 更新协议

1. **触发时机**：完成一次 WPF → Avalonia 内容同步（整分支合并或逐条移植），或完成一次差异/缺口分析之后。
2. **必改字段**：§1 的「WPF 最后已同步 commit / 同步日期 / 同步方式 / 上一次对比分析报告」。
3. **必须追加**：在 §6 历史记录末尾加一行；**不得删除旧行**（历史基线是排查旧结论的依据）。
4. **同步点推进后**：§3 待同步清单按新同步点重算（重新列出仍待同步的提交），不是直接删掉。
5. 本文件是**跟踪文件**，必须提交进仓库；不要放进 `.workbuddy/` 等会被外部清理进程删除的目录。
6. `AGENTS.md` 在本仓库被 `.gitignore:284` 忽略（本地文件、不入库）：所以读写纪律写在**本文件**里，
   本文件才是唯一入库的权威记录；`Avalonia/AGENTS.md` 只是本地的工作提示。

## 6. 历史记录

| 日期 | 动作 | WPF 侧 commit | 合入 / 报告 | 说明 |
| --- | --- | --- | --- | --- |
| 2026-01-06 | 初始移植（分叉） | `a00e90ab4980` | `6d452ce16` startup huge porting from WPF to Avalonia | Avalonia 树由此状态复制 |
| 2026-09-03 | 对比分析 | `a00e90ab4980`..`8b2940785` | `Avalonia/docs/wpf-master-origin-master-comparison-2026-09-03.html` | 264 提交，合并基点 `a00e90ab4980` |
| 2026-09-19 | 对比分析 | `a00e90ab4980`..`8b2940785` | `Avalonia/docs/wpf-master-to-avalonia-migration-review-2026-09-19.html` | 265 提交，逐项核对是否已迁到 Avalonia |
| 2026-09-19 | 同步（合并） | `8b2940785` | `b26b79118` | WPF `origin/master` 并入 `avalonia12`；当前同步点 |
| 2026-09-29 | 建立本文件 | `8b2940785` | — | 记录同步点、分叉基线与 84 路径待同步清单 |
| 2026-09-29 | 反向差异分析（Avalonia → WPF 回移候选） | `8b2940785` | `Avalonia/docs/avalonia-to-wpf-editor-render-backport-review-2026-09-29.md` | 逐提交核对 Avalonia 侧编辑器渲染改动与 WPF 现状：A 可直接回移 12、B 需适配 3、C 不适用/已等价 9、D Avalonia 侧未落地 4；推荐顺序与可复用基准已列出 |
| 2026-09-29 | 反向回移落地（Avalonia → WPF，逐项签入） | `8b2940785` | 报告 §9（15 个 WPF 提交，`b785a155`…`45afda72`） | 已回移 **A1–A4、A6–A12、B1、B2**；**A5、B3 不做**；每项独立提交 + WPF 解决方案构建，关键项附临时差分冒烟；A1–A4 经独立只读评审判定 faithful（1 处有意偏差：A4 剪除未用组后重建帧内区间缓存）。**注意方向：本行是 Avalonia → WPF，不改变 §3 的 WPF → Avalonia 待同步清单。** |
| 2026-09-29 | 性能修复（WPF 侧，非同步） | `18c372e6` | `docs/render-performance-profile-2026-09-29.md` §8 进度 + 行「`OnEditorRender` Bullets LINQ 改直写循环」 | 预览模式子弹/Bell 分桶查询去 LINQ：`BinaryFindRange(yield)` + `Where` 逐 target 重枚举 → `BinaryFindRangeIndex` + 索引循环（谓词内联）直写目标桶（新增 `AddVisibleTGridRangeObjectsInto`），并清掉块内恒真内层 `if (IsPreviewMode)` 与死 `AsEnumerable()` 初始化。基准 `OngekiFumenEditor.Benchmark/Benchmarks/BulletBellQueryBenchmarks.cs`：T=1 下 0.58–0.80×、每帧分配 208–264 B → 0。**Avalonia 树尚未移植**：`Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs:547-600` 仍是旧形态（含恒真内层判断、死初始化、`GetCurrentTGrid()` 取时刻）。不改变同步点。 |
| 2026-09-29 | 性能剖析（WPF 侧运行时测量，非同步） | `7f0f2953ea`（测量时 WPF 树 HEAD == 桌面部署版本） | `docs/render-performance-profile-2026-09-29.md` | EventPipe 采样/分配/GC + 计数器 + GCDump + SOS 堆转储，测量运行中的 `OngekiFumenEditor.exe`（PID 29936，稳态渲染谱面）。**不改变同步点。** 附带跨树核对（仅文件存在性与 MVVM 基类）：`GridBase` 通知模式两树相同但 Avalonia 用 CommunityToolkit（无 Caliburn 闭包）；`TGrid/XGrid` 算术分配、`VisibleLineVerticesQuery`、`GetChildObjectsFromTGrid` 等热点文件两树皆有，Avalonia **无 NAudio/WasapiOut**、**无 `DrawPlayableAreaHelper_new.cs`**。 |
| 2026-09-29 | 性能修复（WPF 侧，非同步） | `e67c3e1a`（含子模块 `Dependences/gemini` `3f6d5b2`） | — | 日志输出链路去 UI 阻塞：`OutputViewModel.Append` 由「每行 `StringBuilder.ToString()` + 全文替换 TextBox」改为「增量 `IOutputView.AppendText` + 合并刷新」；`FileLogOutput` 改为一次 drain 批量写文件（原先每行开关一次文件）；`Log` 队列把「入队 + 判断是否唤醒」与「空队列 + 停止」放在同一把锁下，顺带让 `WaitForAllLogWriteDone()` 成为真屏障。实测（真实窗口 + 真实 TextBox，5739 行/1.21 MiB 真实会话日志）：输出面板整批灌入 461986 ms → 202 ms、视图更新 5739 次 → 2 次、150 行/秒突发下输入延迟 p50 9452 ms → 1.4 ms（max 19918 ms → 64 ms）；文件日志 5739 行 1755 ms → 14 ms；前后视图中文本逐字符一致（1264680 chars）。**Avalonia 树没有对应输出面板模块（其日志只走 console/file），不改变同步点。** |
| 2026-09-29 | 回归工具（WPF 侧，非同步） | `dd97d9ad`（工具在 `OngekiFumenEditor.OutputPaneCheck/`） | — | 把上述输出面板的验收从临时脚手架固化成仓库常驻检查：真实窗口 + 真实 Dispatcher + 真实 `OutputView`/`TextBox`（窗口在屏外），覆盖自动滑动（开=跟尾并精确钉在末尾、关=保持读者位置 top/middle/bottom 且不被新日志拽走、切回=下一次刷新重新钉尾）、清理（视图清空 + 之后内容无丢失/重复）、以及吞吐与输入延迟测量。`dotnet run --project OngekiFumenEditor.OutputPaneCheck -c Release`，检查失败退出码 1；已登记进 `OngekiFumenEditor.sln`。旧实现同样通过全部行为检查（最终状态逐字符一致），差异只在成本：同场景 555 行 1889 ms → 26 ms。 |
| 2026-10-07 | 修复（WPF 侧，非同步） | `3c94592e` | 回归用例在 `tests/mcp-e2e/suites/70-metainfo-check.mjs` | `WrongLocation` 规则接受水平轨道段：整段 T 相同、只有 X 变化（如同一时刻的 WLS/WLE 瞬时墙）的段在该时刻覆盖一段 X 区间，物件落在区间内（含 1 单位容差）即算贴合轨道，不再因零时长段插值只取起点键而误报。新增 MCP e2e 回归用例（建墙 + 零时长段 + 3 个 tap：终点键/区间内不报、区间外仍报）。实测：真实谱面（0836_03）8 条误报清零、其余结论不变；e2e 全套 1224/1224 通过。**Avalonia 树尚未移植**（`Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/DockableObjectWrongLocationCheckRule.cs` 仍是旧形态）。不改变同步点。 |
| 2026-10-07 | 修复（WPF 侧，非同步） | `6968f757` | 回归用例在 `tests/mcp-e2e/suites/70-metainfo-check.mjs` | `ObjectTimelineNotAligned` 改为格点判据：偏移量与拍长的最大公因数 ≥ 10 网格（4/4 下即 1/48 拍 = 1/192 音符；短拍号放宽到 ≥ 1/48 拍；分子为 0 或不整除 ResT 的拍号退化为整 TGrid 单位）→ 全整数运算。既消除旧浮点判等在拍序号变大后把 1/3 算成 3.0000000000000426 的误报，也覆盖 3/8、5/8、7/12 等被旧 `{1/n,(n-1)/n}` 允许集漏掉的 k/n 位置。语料实测（4871 张谱面 / 1,569,151 个 tap）：旧逻辑 52,780 条报告 → 新 95 条（消除 52,728；新增 43 条均为 1–5 网格级真离格）。真实谱面 0836_03（9 条）与 1192_03（19 条）全部清零；e2e 全套 1228/1228。**Avalonia 树尚未移植**（`Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/CommonObjectTimelineNotAlignedCheckRule.cs` 仍是旧形态）。不改变同步点。 |
| 2026-10-07 | 新增检查规则（WPF 侧，非同步） | `ea74d3c5` | 回归工具 `OngekiFumenEditor.FumenCheckerTests/`（按 csproj 单独调用，不入 sln）+ `tests/mcp-e2e/suites/70-metainfo-check.mjs`（ruleCount=22） | 依据游戏本体（SDDT 1.60，`D:\sddt160`）的 Reader/运行时代码，新增 7 条「游戏侧会崩溃/卡死」规则：`BulletPalleteDuplicateId`（BPL id 重复 → 字典 Add 抛异常）、`SoflanPatternMissingForArea`（ISF 组无对应 soflan 且区域内有 Hold → `lane.format()` 字典索引崩溃）、`OrphanLaneRecord`（延伸段无起点 → recID=-1 残缺轨道 / 第二条则字典重复键）、`ColorfulLaneRecordColumns` 与 `BeamRecordColumns`（列数不足 → 按列索引越界）、`HoldProgressJudgeLoop`（PROGJUDGE_BPM 相对该段 BPM 过大 → 判定点步长 0、读谱死循环）、`BpmOutOfRange`（BPM ≤0 → 时间轴换算被跳过）。为此引入解析期缺陷通道 `OngekiFumen.ParseIssues`（解析器上报重复 BPL id / 孤立延伸段 / 列数不足），lane/beam 解析器补齐列数守卫；`EnemySetCheckRule` 扩为 WAVE1 与 BOSS 任一缺失都报 Error（游戏侧 `if (!flag2 || !flag)` 会丢弃整组 EST，旧规则只查 BOSS 且仅 Suggest），`ObjectTimelineNotAligned` 在 BPM 非法时跳过（原先会从 `TimeSpan` 抛异常打崩整批检查）。验收：新 harness 66/66（golden 零告警基线、逐规则正反用例、kitchen-sink、67 张样例谱面 sweep），e2e 1228/1228；官方谱面 0836_03 / 1192_03 在 22 条规则下仍零告警。**Avalonia 树尚未移植**（其 `Modules/FumenCheckerListViewer/` 仍只有旧 15 条规则，也没有解析期缺陷通道）。不改变同步点。 |
| 2026-10-08 | 新增检查规则（WPF 侧，非同步） | `22586507` | 回归用例在 `OngekiFumenEditor.FumenCheckerTests/`（按 csproj 单独调用，不入 sln） | 新增 `DefaultSoflanLastSpeedNonPositive`（警告档 `problem`）：取默认变速组（组 0）内 TGrid 最大的一条变速记录，其 Speed ≤ 0（非正速）即报——非正倍率会让游戏侧的时间轴映射停滞/倒退；既有 `Soflan` 规则只查「组末尾累积倍率是否回到 1x」，覆盖不到最后一条变速取值本身。描述走本地化资源（en/zh-Hans/ja + Designer）。验收：harness 75/75（新增 3 用例：默认组末条 0x→命中、0.5x→不报、非默认组 0x→不报；kitchen-sink 9 条同谱各命中一次），样例谱面 sweep 67 张仅 1 条真实命中（`22863_04` 以 `SFL 65 960 120 0x` 收尾且其后仍有音符，已登记白名单），e2e 全套 1228/1228（ruleCount=23），官方谱面 0836_03 / 1192_03 在 23 条规则下零告警。**Avalonia 树尚未移植**（其 `Modules/FumenCheckerListViewer/` 仍只有旧 15 条规则）。不改变同步点。 |
| 2026-10-08 | 修复（WPF 侧，非同步） | `9aeb073c` | 验证用 headless 探针（脚本未入库） | 音频加载不再经 NAudio `AudioFileReader` 的 ACM 路径：`WAVE_FORMAT_EXTENSIBLE`（如 24bit PCM 的 wav）在 `AudioFileReader` 里会走 `acmFormatSuggest` 转码，缺 ACM 驱动的机器上直接 `NoDriver calling acmFormatSuggest` 失败，`DefaultMusicPlayer.Load` 抛错后采样为空——播放与 AudioPlayerToolViewer 波形/频谱都没有内容。改为 `AudioCompatibilizer.OpenSampleProvider`：优先 `WaveFileReader` + 按位深/SubFormat 的托管转换器（PCM 8/16/24/32、float32），托管覆盖不到的（ADPCM/µ-law/mp3/extensible-float）才回退 `AudioFileReader`；音乐与音效两处调用点统一，并顺带释放底层 reader。验证：同机同文件 A/B 探针（旧路径复现 ACM 失败；新路径解出 129.94s / 48kHz / 2ch / 12,474,370 采样、峰值 1.0）；编辑器构建 0 error；checker harness 75/75。**Avalonia 树尚未移植**（其 `Kernel/Audio/NAudioImpl/` 走 `INAudioFileReaderFactory` + `ToSampleProvider()`，结构不同、本次未改，是否需要对应处理未核实）。不改变同步点。 |
| 2026-10-08 | 性能审查（WPF 侧，非同步） | `5548703e` | `docs/fumen-load-performance-audit-2026-10-08.md`（26 条编号清单 + 实测基线，**尚未实施**） | 对「点开工程 → 编辑器可用」整链做只读审查：解析文本热路径（每行按目标类型重复整行切分、数值转换全走 LINQ 而 Span 快速路径成死代码、按 RecordId 找 lane 触发 IntervalTree 整表快照拷贝）、对象进模型（`IntervalTree.Add` 线性去重、排序集合逐条 `List.Insert` 且解析期未用批处理、每物件多路 PropertyChanged 订阅、TGrid setter 闭包+全局并发字典）、加载编排（同一音频整曲解码两次、四段重活严格串行、就绪前阻塞式 LOH CompactOnce、WASAPI latency=0 忙轮询、Music.xml 解析两次）共 26 条，逐条给 file:line、机制、收益与改动风险，并含建议实施顺序与「已做到位、不要重复建议」清单。实测基线取自编辑器自身分步日志（0836_03 解析 0.61 s / 全程 2.61 s；1192_03 2.08 s / 5.38 s）；仓库 BDN 因本机杀软杀子进程未能给出语料级数据。**Avalonia 树未覆盖**（其音频/渲染结构不同）。不改变同步点。 |
| 2026-10-08 | 性能修复（WPF 侧，非同步） | `1a71946a` | 审查清单第 18 条 + `docs/fumen-load-performance-audit-2026-10-08.md` | 打开工程不再为「取音频时长」整曲解码：`IAudioManager` 新增 `GetAudioDurationAsync`（wav 读 RIFF 头、mp3 走 `Mp3FileReader` 帧索引、acb 先走转换落盘缓存再读 wav 头；取不到回退 `LoadAudioAsync`），6 处调用点（快速打开含手工选择音频分支、列表浏览器、新建工程对话框、MCP 两处、命令行）全部切换，旧 `CalcAudioDuration` 删除；时长与旧路径同源同值（同为对应 reader 的 `TotalTime`）。实测 0836_03（音频为 acb）：解析前段 1.58 s → ~0.31 s（其中新增的时长读取 9 ms）；构建 0 error、harness 75/75。**Avalonia 树尚未移植**（其 `IAudioManager` 需补同名成员；Avalonia 音频走 `INAudioFileReaderFactory`，实现方式可自定）。不改变同步点。 |
| 2026-10-08 | 新增功能（WPF 侧，非同步） | 工作区改动（未提交） | 校验工程 `OngekiFumenEditor.RhythmAnalysisCheck/`（按 csproj 单独调用，不入 sln）+ `docs/rhythm-spectrum-design.md` | 新增音频频谱节奏曲线（「看得见节奏」）：`Kernel/Audio/Rhythm/`（1024 点 Hann STFT，5ms/帧 → 对数幅度 → 每 bin 150ms 背景窗口抑制持续音 → 低/中/高三频段谱通量 → 分频段局部自适应归一化 → 加权合成一条 0–1 曲线），并在 AudioPlayerToolViewer 叠加这一条镜像曲线（`RhythmGeometry.BuildCurve`，按像素列降采样，默认开、开关 `ShowRhythmCurve`、颜色 `WaveformRhythmCurveColor`），显示侧再做一次色调映射 `RhythmCurveTone`（局部均值强调 λ + γ 压缩，均在原生帧率、降采样之前施加；强度由 `RhythmCurveIntensity` 下拉档位给出：Default=原样、Enhanced=1.5/0.7、Strong=2.0/1.3）。分析侧只产出曲线：起音事件/音频节拍线/面板 BPM 读数与速度估计按作者要求**整体移除**（不是默认关闭）。验收：合成用例 14/14（150BPM 鼓组 p90/mean=2.85、150/150 击打为局部凸起、击打均值/整体均值=2.81；纯踩镲 75/75 凸起、比值 7.13；持续和弦 p90/mean=1.25 保持平坦；静音全零；16bit→null；几何 6 项）；真实歌曲 3 首 216–275s 分析 0.9–1.2s、p90/mean 1.82–1.94；实机（Debug、真实窗口后台最小化）日志 `[Rhythm] 分析完成 3647ms, 曲线帧数=33033`、整会话 0 条 ERROR。**Avalonia 树尚未移植**（其 `Modules/AudioPlayerToolViewer/` 只有包络波形、`Kernel/Audio/` 无 `Rhythm/`；移植点：新增 `Kernel/Audio/Rhythm/` 与 `RhythmGeometry`、MVVM 侧曲线接入、对应设置项与三语资源）。不改变同步点。 |
