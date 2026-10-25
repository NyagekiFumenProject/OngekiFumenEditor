# WPF ↔ Avalonia 迁移同步基线（唯一权威记录）

> 本文件只回答一个问题：**Avalonia 项目当前同步到 WPF 项目的哪个 commit？**
>
> 任何「WPF 与 Avalonia 的功能/内容差异、迁移缺口」分析，动手前先读本文件取基线；
> 做完一次同步或一次对比之后，必须回写本文件。基线记错 = 整份差异分析结论失真。

- 更新日期：2026-10-21
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
| 2026-10-08 | 新增功能（WPF 侧，非同步） | 工作区改动（未提交） | 校验工程 `OngekiFumenEditor.RhythmAnalysisCheck/`（按 csproj 单独调用，不入 sln）+ `docs/rhythm-spectrum-design.md` | 新增音频频谱节奏曲线（「看得见节奏」）：`Kernel/Audio/Rhythm/`（1024 点 Hann STFT，5ms/帧 → 对数幅度 → 每 bin 150ms 背景窗口抑制持续音 → 低/中/高三频段谱通量 → 分频段局部自适应归一化 → 加权合成一条 0–1 曲线），并在 AudioPlayerToolViewer 叠加这一条镜像曲线（`RhythmGeometry.BuildCurve`，按像素列降采样，默认开、开关 `ShowRhythmCurve`、颜色 `WaveformRhythmCurveColor`），显示侧再做一次色调映射 `RhythmCurveTone`（局部均值强调 λ + γ 压缩，均在原生帧率、降采样之前施加，读取范围外扩模糊核半径、曲线首尾之外补 0 以保证窗口边缘与歌曲末尾都不产生伪影；强度由 `RhythmCurveIntensity` 下拉档位给出：Default=原样、Enhanced=1.5/0.7、Strong=2.0/1.3）。分析侧只产出曲线：起音事件/音频节拍线/面板 BPM 读数与速度估计按作者要求**整体移除**（不是默认关闭）。验收：合成用例 14/14（150BPM 鼓组 p90/mean=2.85、150/150 击打为局部凸起、击打均值/整体均值=2.81；纯踩镲 75/75 凸起、比值 7.13；持续和弦 p90/mean=1.25 保持平坦；静音全零；16bit→null；几何 6 项）；真实歌曲 3 首 216–275s 分析 0.9–1.2s、p90/mean 1.82–1.94；实机（Debug、真实窗口后台最小化）日志 `[Rhythm] 分析完成 3647ms, 曲线帧数=33033`、整会话 0 条 ERROR。**Avalonia 树尚未移植**（其 `Modules/AudioPlayerToolViewer/` 只有包络波形、`Kernel/Audio/` 无 `Rhythm/`；移植点：新增 `Kernel/Audio/Rhythm/` 与 `RhythmGeometry`、MVVM 侧曲线接入、对应设置项与三语资源）。不改变同步点。 |
| 2026-10-09 | 节奏曲线算法切换（WPF 侧，非同步） | 工作区改动（未提交） | `docs/rhythm-spectrum-design.md` §3、§7 + 独立校验工程 `OngekiFumenEditor.RhythmAnalysisCheck/`、`OngekiFumenEditor.RhythmDiagnostics/`（均不入 sln） | `DefaultRhythmAnalyzer` 默认切换到已验证的敏感方案：1024 点周期 Hann、200fps、15ms 滞后谱沿频率轴取 ±1 bin 最大值、正差分减 0.03、σ=7.5ms 高斯平滑、分频段 3s 局部归一化（尺度下限 `max(0.1, 原始通量 P95×0.25)`）、1/1/0.6 合成；不加相干能量门控、BPM 依赖或曲目特判。旧 150ms 背景均值与 50ms 平滑从生产删除，仅作诊断研究对照；诊断生产基线随之迁移。IRhythmAnalyzer 契约、MEF、曲线数据格式与 UI/强度档映射均不变。验收：常驻行为校验 **34/34**；250BPM 内部击打 58/58、120ms 长尾后弱同音色击打 2/2，恒幅音/小幅纯音高颤音/和弦+极小噪声均无内部可见假峰；music0964 全曲 27939 帧逐帧诊断误差 0，93.010s 弱二次峰高度 0.1705（Enhanced 后 0.1053），生产几何 PNG 中仍独立可见；14 组冻结 holdout 的逐帧误差均为 0。敏感方案仍会在宽带噪声与长尾上产生额外峰，未宣称准确鼓点检测。**Avalonia 树尚未移植**，本次未改 Avalonia 代码，不改变同步点。 |
| 2026-10-09 | 清理（WPF/Avalonia 侧，非同步） | 工作区改动（未提交） | — | 静态核对确认 WPF 主渲染路径使用 `DrawPlayableAreaHelper_new`、旧 `DrawPlayableAreaHelper.cs` 无实例化；Avalonia 同名 helper 仍由主视图与测试使用。移除 WPF 旧 helper、Earcut 子模块及两棵树中的项目引用、解决方案条目和 CI 初始化项；同步点不变。 |
| 2026-10-11 | 修复（WPF 侧，非同步） | `b439ef5f` | 真实 GUI：用 Debug 构建快速打开无音频的裸 ogkr，音频选择框可见可选（选定后加载到 `Ready`）；另有未入库的最小 WPF 复现工程对照（旧顺序必现冻结，新顺序正常） | 修「加载对话框卡在等待手动选择音频文件、音频选择框永不显示」：`EditorLoadingSession.Begin` 改为 `BeginAsync`，等加载对话框真正显示（`EditorLoadingDialogViewModel.ViewShownTask`，视图 Loaded / 提前结束 / 显示失败都会完成）后才把 session 交还调用方。原先流程在第一个真正异步点之前全是同步代码（裸 ogkr 的音频定位没有 await），会先一步进入音频选择框自己的模态循环；排队中的「显示加载对话框」操作随后只能在该循环里执行——两个模态循环嵌套顺序倒置，选择框窗口已创建但永不显示、整条加载流程死锁且取消按钮也失效（只能杀进程）。5 个调用点（`DocumentOpenHelper` ×2、`FumenVisualEditorViewModel.DoNew/DoLoad`、`OgkiFumenListBrowserViewModel.LoadFumen`）改用 `await BeginAsync`。Avalonia 树没有加载对话框设计（其 FastOpen 直接弹选择框），不受影响，不改变同步点。 |
| 2026-10-11 | 新增功能（WPF 侧，非同步） | `514249b6` | 一次性 MCP 探针验证（脚本未入库，位于 `tests/mcp-e2e/.tmp/`） | 拖动物件时水平位置强制整数 XGrid 单位：新设置 `ForceXGridIntegerUnit`（默认开；设置窗口「文档→编辑器」页「用户交互」组；en/zh-Hans/ja 三语 + tooltip）开启时 `DefaultObjectInteractiveAction.OnMoveCanvas` 把 XGrid 取整到最近整数 unit（`XGridCalculator.RoundToIntegerUnit`，半值远离零，Grid 置 0）；已吸附轨道的 Tap/Hold/HoldEnd 拖动当帧由轨道接管 X 时保持精确贴合、不取整（`IsXGridSnappedToLaneThisMove` 帧标记，Dockable/HoldEnd 两处均置位；HoldEnd 贴合分支由 `laneX ?? relativePoint.X` 显式化，行为等价）。实测（自建 X=1.5 轨道）：自由物件 1.7→2 且 Grid=0、吸附 Tap 与吸附 HoldEnd 均保持 1.5、关闭开关恢复 1.7；mcp-e2e 全套 1228/1228。**Avalonia 树尚未移植**（其 `DefaultObjectInteractiveAction.cs` 无对应取整与切换开关），不改变同步点。 |
| 2026-10-11 | 清理与守卫（WPF 侧，非同步） | 工作区改动（未提交） | 一次性 MCP 探针验证（脚本未入库，位于 `tests/mcp-e2e/.tmp/`） | 移除 `IsLocked` 体系：删除 `IsLocked`、`EditorLockedVisibility`、`LockAllUserInteraction`、`UnlockAllUserInteraction` 及全部关联（7 处鼠标/拖放交互的锁定检查、剪贴板 2 处、InterpolateAll/StandardizeFormat 的锁定调用、`Resources.EditorLock`/`EditorUnlock` 三语文案），`IsDesignMode` 简化为 `!IsUserRequestHideEditorObject`。并为物件增删改查快捷键命令补编辑模式前置检查（预览模式下 Toast 复用 `Resources.EditorMustBeDesignMode`「请先将编辑器切换到编辑模式」）：Delete/Ctrl+A/Esc、快速放置 Tap/Hold、5× 放置到各轨道、换可停靠轨道类型、critical/曲线控制点、Flick 方向、选整条轨道/关联曲线、菜单全选/反选/镜像 XGrid；批量模式三入口（Alt+B 进入、子模式切换键、批量鼠标放置/删除）；`HideOrShow`（切模式本身）按指示不加检查。实测：预览模式下各命令被拦且谱面与选择状态不变、切回编辑模式恢复（探针 9/9）；mcp-e2e 全套 1228/1228。**Avalonia 树暂未处理**（按指示），不改变同步点。 |
| 2026-10-11 | 文案排版（WPF 侧，非同步） | `e91c969a` | — | 设置窗口 35 条「勾选／取消勾选」结构的 tooltip 改为多行显示：首行上下文（如有）、勾选行、取消勾选行、补充说明行（如有），换行以 `&#xA;` 写入 .resx 值；三语（en/zh-Hans/ja）各 35 条；非该结构的提示文案不变。实测：构建 0 error；运行时探针读取编译后资源为 3 行（4/4）。**Avalonia 树暂未处理**（按约定），不改变同步点。 |
| 2026-10-11 | Tooltip 补全（WPF 侧 + gemini 子模块，非同步） | `ca3410d4` | 一次性 MCP 探针验证（脚本未入库，位于 `tests/mcp-e2e/.tmp/`） | 三处 UI 补 tooltip：①「编辑器设置」工具窗 15 项（勾选／取消勾选多行，沿用 `SettingsToolTipEditor*` 命名）；②子弹管理面板 14 处（新建／删除／过滤按钮、过滤框、10 个列头）；③工具箱 24 个物件列表项（新增「物件作用」描述）。工具箱 tooltip 经 gemini 子模块实现：`ToolboxItemAttribute.Description` + 可选 `IToolboxItemLocalizer` MEF 钩子（主仓库 `ToolboxItemLocalizer` 用资源解析、缺失时原样回退；子模块提交 `77aba07`）；三语（en/zh-Hans/ja）共新增 53 条资源。实测：构建 0 error（含子模块）；运行时探针 8/8（本地化解析与回退、生成器 attribute 数据链、ViewModel 转发、设置／子弹／工具箱资源抽样）。**Avalonia 树暂未处理**（按约定），不改变同步点。 |
| 2026-10-11 | Tooltip 补全（WPF 侧，非同步） | `bd04fc19` | 一次性 MCP 探针验证（脚本未入库，位于 `tests/mcp-e2e/.tmp/`） | 三处补 tooltip：①「变速组列表」11 处（建组输入/按钮、两个列表共 8 个列头、变速点列表开关）；②属性浏览器操作视图 6 处（Beam 两条拖拽条、Wall 两条拖拽条、合并轨道、原地生成轨道）；③「音频延迟调整」窗 12 处（两个输入来源、两个浏览按钮、重算物件复选（多行勾选／取消勾选）、按秒／按 TGrid 模式及 Unit/Grid/BPM 字段、处理按钮）；三语（en/zh-Hans/ja）各新增 29 条资源。实测：独立输出目录完整构建 0 error；独立实例运行时探针 5/5（抽样资源解析、多行格式）。**Avalonia 树暂未处理**（按约定），不改变同步点。 |
| 2026-10-11 | 修复（WPF 侧，非同步） | `2c8a6a3d` | 独立验证：Python 复算脚本 + 临时 WPF 验证工程（均未入库，位于 `%TEMP%`） | 修正 OGKR `T_*`（尤其 T_TOTAL/T_HOLD/T_SHOLD）的 Hold 判定公式，对齐游戏本体：① `HoldTickStepCalculator.Calculate` 改为逐位复刻游戏 `MU3.Reader.BPM.getProgressJudgeGrid()`——BPM/PROGJUDGE 以 float32 参与、BPM 先按 `Mathf.Max(bpm, 3.75f)` 抬到 3.75 以上，唯一有意偏离是结果夹在 `[1, int.MaxValue]`（游戏在极端输入下会把步长算成 0 或溢出为负，其判定点循环会失去推进性/死循环，编辑器不复刻该缺陷）；② `FumenStatisticsCalculator.CalculateHold` 删除行内重复实现、统一调用 `HoldTickStepCalculator`，基准由 `MetaInfo.TRESOLUTION >> 2` 改为 `DefaultStandardBeatLen`（=1920/4，与 `Hold.CalculateJudgeTGrid` 判定点绘制一致），闭环 Avalonia 文档记录的「两处分辨率基准不一致」；③ PROGJUDGE_BPM 合法阈值改为与游戏 Header.decode 一致（`<= 1.00001 → 240`；原实现接受 `>= 1`，会把游戏实际读作 240 的值写进文件并按其统计）；④ 删除不再使用的 `FumenStatisticsInvalidHoldTiming` 资源（en/zh-Hans/ja + Designer）。核对结论：`T_TOTAL = T_TAP + T_HOLD + T_SIDE + T_SHOLD + T_FLICK` 的分类与游戏 `NotesManager._totalNotes` 一致（Hold 起点计 Tap/SideTap、judgeList 每点计 Hold/SideHold 且恒含终点、起点单独一次、Bell 不入 total）——即 Hold 的 combo 贡献 = 起点 1 + 含终点在内的全部判定点。验证：49 张真实 ogkr 独立复算（游戏算法对照）修复前后均与文件 T_* 一致（样本无 BPM<3.75，输出不变）；40 万随机 + 2 的幂边界对照，新算法与游戏 float32 算法逐位一致（0 差异）；临时 WPF 验证工程 43/43（含 3.0 BPM 场景 T_HOLD 549 对旧公式 1280、跨 BPM 150→480 为 20、240→3.0 为 557；wall/CR/tap/flick/bell 分类与 T_TOTAL 自洽；Coerce/TryParse 阈值）；WPF Debug 构建 0 error。**Avalonia 树尚未移植**：其 `HoldTickStepCalculator.cs` 仍是 log2 闭式（无 float32/3.75 下限、阈值 1f），`FumenStatisticsCalculator.cs` 仍传 `timeResolution_ >> 2`，测试 `HoldTickStepTests.cs` 固化了旧行为（移植时需同步更新）。不改变同步点。 |
| 2026-10-11 | 新增功能（WPF 侧，非同步） | `2c8a6a3d`（与上一行同一提交；本轮改动随该提交一并入库） | 真实 WPF 渲染探针（临时工程，未入库，位于 `%TEMP%`）：屏外窗口 + 真实 BAML/DataGrid/绑定 | 谱面统计对话框「T_* 命令」页新增两项派生指标：① **白金分数满分**（行「白金分数」/Platinum score/プラチナスコア）= `T_TOTAL × 2`，依据游戏 `Counters.addPlatinumScore`（每判定单元最多 2 分）与 `updatePlatinumScore` / `DataManager` 的 `platinumScoreMax = totalNotesNum × 2`（已核对游戏 `volumeNotesTech` 8 槽权重全为 1，故与加权口径等价）；② **HoldTick 贡献占比**（行「HoldTick 占比」）= `(T_HOLD + T_SHOLD) / T_TOTAL`，以一位小数百分比显示，描述列展开分子分母（如 `66.7%` 对应 `16 / 24`）。实现：`FumenStatisticsResult` 新增 `PlatinumScoreMax/HoldTickObjects/HoldTickShare`（由 `FumenStatisticsCalculator` 统一计算，可供脚本/导出复用）；`StatisticsRow` 新增可选 `ValueText`（仅命令页值列改绑 `CountText`，百分比等非整数指标可显示，对象页不受影响）；三语资源 en/zh-Hans/ja + Designer 各新增 4 条（`FumenStatisticsPlatinumScore[Description]`、`FumenStatisticsHoldTickShare[Description]`）。实测：因本机有运行中的 `OngekiFumenEditor.exe` 与 VS 占用原输出目录，完整构建的复制阶段不可用（编译阶段 0 错误）；改在隔离沙箱（复制现有 Debug 输出并用 `obj` 新编译主程序集覆盖）运行验收探针 **ALL PASS**——数据层（`PlatinumScoreMax=48`、`HoldTickObjects=16`、`Share=0.666667`；空谱面 0/0）、ViewModel 行（9 行、`CountText="48"` 与 `"66.7%"`、描述含 `16`/`24`）、真实窗口渲染（TabControl/命令页 DataGrid 定位成功、值列绑定路径 = `CountText`、渲染后单元格实际文本 `48` 与 `66.7%`、描述列非空）。**Avalonia 树尚未移植**（其 `FumenStatisticsResult`/`FumenStatisticsDialogViewModel`/`FumenStatisticsDialogView` 均无对应字段与行），不改变同步点。 |
| 2026-10-20 | 实施方案文档（WPF 侧，非同步） | 本次仅文档改动，未修改功能代码 | [docs/pr45-bullet-placement-implementation-plan.md](../docs/pr45-bullet-placement-implementation-plan.md) | 基于 PR #45 功能需求，静态核对当前 WPF 管理器、鼠标／拖放、吸附、撤销及快捷键入口，整理连续放置、取消保护、伤害循环的六阶段实施流程与验收清单；Q-1～Q-4 给出推荐口径但未改写为已确认需求。功能开发与 GUI 验收尚未执行。**Avalonia 树本次未处理，不改变同步点 `8b2940785`。** |
| 2026-10-20 | 界面约束确认（WPF 文档，非同步） | 本次仅文档改动，未修改 XAML 或功能代码 | [功能需求](../docs/pr45-bullet-placement-functional-requirements.md) U 节、[实施方案](../docs/pr45-bullet-placement-implementation-plan.md) F-2 / V-12 | 按用户要求以 PR #45 指定的 `FumenBulletPalleteListViewerView.xaml` diff 为硬约束，核对原始补丁（视图 blob `30f7bf18c → 271be5a41`）：新增宽 110 的“连续放置”列、两个 CheckBox 及原事件绑定，保留右侧原 Bullet／Bell 创建和克隆入口、操作列宽 240。撤销先前缩短标题、删除旧入口和收紧布局的建议，并同步调整拖放口径与验收；未进行 GUI 验收。**Avalonia 树本次未处理，不改变同步点 `8b2940785`。** |
| 2026-10-20 | MVVM 约束修订（WPF 文档，非同步） | 本次仅文档改动，未修改 XAML 或功能代码 | [功能需求](../docs/pr45-bullet-placement-functional-requirements.md) U 节、[实施方案](../docs/pr45-bullet-placement-implementation-plan.md) F-1 / F-2 / V-1 | 按用户要求移除 U-3 原事件契约，覆盖上一条记录中“保留原事件绑定”对新增复选框的要求：改用状态绑定与命令，由唯一放置会话驱动勾选；ViewModel 不持有控件、不直接修改控件属性、不依赖加载或数据上下文事件同步状态。其余 U 编号、界面外观布局、右侧既有入口和 A／B／C 功能基线保持不变；未进行 GUI 验收。**Avalonia 树本次未处理，不改变同步点 `8b2940785`。** |
| 2026-10-20 | 预览绘制落点确认（WPF 文档，非同步） | 本次仅文档改动，未新增 helper 或修改功能代码 | [实施方案](../docs/pr45-bullet-placement-implementation-plan.md) R-8 / F-3 / V-3 / V-11 | 按用户指定，在方案中将 A-3 的绘制落点确定为 `FumenVisualEditor/Graphics/Drawing/Editors/DrawProjectilePlacementPreviewHelper`：复用现有绘制上下文与投射物绘制资源，读取与正式放置共享的吸附后候选位置；不另算吸附规则、不注册物件命中、不修改谱面。明确静止指针下视口／吸附设置变化时的预览刷新及 helper 生命周期接入。尚未实施或进行 GUI 验收。**Avalonia 树本次未处理，不改变同步点 `8b2940785`。** |
| 2026-10-20 | 预览请求接口设计（WPF 文档，非同步） | 本次仅文档改动，未修改生产接口 | [实施方案](../docs/pr45-bullet-placement-implementation-plan.md) R-9 / I-1 / F-1 / F-3 | 按用户要求规划 `IFumenBulletPalleteListViewer.IsRequestPlaceBatchProjectilePreview` 与 `RequestBatchPlaceProjectileInfo`，新增信息契约 `IPlaceBatchProjectileInfo` 描述模板和 Bullet／Bell 类型；方案建议以只读 `Pallete`、`IsBell` 表达内容，请求标志由信息是否存在派生。管理器呈现唯一放置会话的请求，绘制 helper 通过接口消费，不复制维护状态；补充空值、通知、实例复用及跨谱面预览保护约定。接口和 helper 尚未实施，未进行 GUI 验收。**Avalonia 树本次未处理，不改变同步点 `8b2940785`。** |
| 2026-10-21 | 功能实现与回归（WPF 侧，非同步） | `48c08772`（`WIP` 检查点）；后续解耦改动未提交 | [实施记录 §6](../docs/pr45-bullet-placement-implementation-plan.md#6-实现与验证记录2026-10-21)、[独立回归工程](../tests/bullet-placement/BulletPlacementCheck.csproj) | 完成 PR #45 子弹管理器持续放置与伤害快捷键：新增只读 `IPlaceBatchProjectileInfo`／管理器请求属性及 MVVM 互斥勾选，保留指定 diff 外观和旧创建／克隆入口；编辑器持有唯一会话，复用吸附与提交链，点击／拖动释放一次一个并保持会话；新增 `DrawProjectilePlacementPreviewHelper`，复用贴图和锚点、无谱面／命中副作用；B-1 右键取消由绘图区原有鼠标事件完成，B-2 由 `IEditorDocumentManager.OnActivateEditorChanged` 判断，B-5 由编辑器原有 `Grid_Drop` 在 Drop 时取消，未额外订阅窗口 `ContextMenuOpening`／`QueryContinueDrag`；未注册任何窗口鼠标事件，左键手势清理在原有 `OnMouseDown`／`OnMouseUp` 内完成。输入接线在 `FumenVisualEditorViewModel` 构造函数订阅 `IEditorDocumentManager.OnActivateEditorChanged` 并在 `Dispose` 解除；`FumenVisualEditorView.xaml.cs` 保持原版，不再依赖视图生命周期注册输入事件。采用方案 D-1～D-4，已回写需求 Q 节。验证：WPF Debug 构建成功；独立行为／真实 XAML 路由／Skia CPU 光栅回归 212 项通过；既有 MCP 全套 1228/1228；后台真实实例完成放置、F13 配置加载与路由、Undo／Redo 和预览截图。物理输入、浮动窗口、原生 OLE 拖拽及其他渲染后端未逐项交互验收，边界见实施记录。测试实例停止，临时配置还原。**Avalonia 功能代码本轮未处理，不改变同步点 `8b2940785`。** |

| 2026-10-18 | 修复（WPF 侧，非同步） | `f47b823f`、`85799db1`、`0a7e4251`、`2e2c8a87` | 一次性验证工程 `tests/mcp-e2e/.tmp/undo-check`（未入库；40 项断言全过）+ `dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug` 0 error + 后台启动冒烟（自建实例启动到 MCP ready 无异常后停止） | 为四项此前不进 Undo/Redo 的编辑器操作补封装：① TGrid 计算器「赋给选中物件」改为 `LambdaUndoAction`（`TGridCalculatorToolViewerViewModel.CreateApplyTGridAction`）；②「重算编辑器滚动高度」改为命令内联 `LambdaUndoAction`（撤销恢复旧 `AudioDuration` 并重算）；③ 变速分组新建与拖拽移动改为 `LambdaUndoAction` / `FumenSoflanGroupListViewerViewModel.CreateMoveAction`（`SoflanGroupWrapItemGroup.Insert` 取代无调用者的 `InsertBefore`，新增三语资源 `SoflanGroupMove`；拖到空目标不再丢项）；④ 辅助菜单「调整可停靠物件水平位置」改为单个批量 `LambdaUndoAction`。**Avalonia 树尚未移植以上四项，本次不改变同步点。** |

## 本次 WPF 独立改动（2026-10-16）

- WPF：子弹和激光判定范围显示。
- Avalonia：本次未移植。
- 不改变现有同步点。

## 2026-10-18：PR 排版整理

- 按官方 `.editorconfig` 及相邻 WPF 代码整理缩进、初始化器和回调换行，功能不变。
- Avalonia 代码本次未处理，不改变同步点。

## 2026-10-21：PR #44 判定范围重写（WPF）

- WPF：弹幕落点与命中范围共用目标解析规则，包含玩家历史位置、速度安全边界及确定性随机包络；实际落点实体带状绘制，随机范围以虚线包络绘制。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-21：PR #44 后续修复（WPF）

- WPF：Beam 判定带宽度改用 `WidthId.WidthDraw`，对齐游戏 `BeamNote.setup` 的 widthID 1..5 → 2/3/4/12/20 映射（`ed0de203`）。
- WPF：投射物 0 速取命中帧玩家位置、负速保留反向轨迹与命中后落点，复刻游戏 `calcNoteFrameAimBulletCreate`（`a45e222f`）。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-21：判定范围开关迁移到全局设置（WPF）

- WPF：`ShowBulletHitRanges`/`ShowBeamHitRanges` 从 `FumenVisualEditorViewModel` 迁移到 `EditorGlobalSetting`（默认开启，设置窗口「文档→编辑器→渲染」页提供开关并随 Apply 保存）；移除编辑器右键上下文菜单中的两个开关。
- 测试：`tests/hit-ranges` 覆盖全局设置默认值与设置页绑定探针；构建 0 error、回归通过。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-21：判定范围绘制目标缓存开关（WPF）

- WPF：`BulletHitRangeDrawingTarget`/`BeamHitRangeDrawingTarget` 在构造函数读取一次全局开关并订阅 `EditorGlobalSetting.Default.PropertyChanged` 更新字段，渲染只读字段；实现 `IDisposable` 取消订阅。
- 测试：`tests/hit-ranges` 增加关闭开关后不生成绘制命令的断言。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-21：判定范围与玩家位置查询分配优化（WPF）

- WPF：`BulletHitRangeDrawingTarget`/`ProjectileBatchDrawTargetBase` 每帧缓存玩家位置委托，消除每个子弹/铃铛 64 B 的委托分配；`BeamHalfWidthPixels` 改为直接调用 `ConvertXGridToX`，去掉每个 Beam 的 lambda/闭包；`PlayerLocationRecorder.GetLocationXUnit` 改用 `SortableCollection` 索引器，消除 `ElementAtOrDefault` 的 80 B/op 枚举器分配。
- 基准（Release，2,000,000 次）：`Resolve`（FixField/Player）、`GetLocationXUnit`、Beam 半宽均为 **0 B/op**；优化前分别为 64、144（含 recorder 80）、80、0 B/op（Beam 的闭包被 JIT 栈分配，但委托调用未内联，耗时约 110 ns/op）。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-22：判定范围轮廓参数命名（WPF）

- WPF：`HitRangeGeometry.AddOutline` 的 `relative` 参数改为 `dashed`，调用点使用命名参数，表达实际语义（随机包络虚线 / 固定落点实线）。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-22：变速分组查看器功能审查（WPF，仅文档）

- WPF：新增 `docs/soflan-group-viewer-audit-2026-10-22.md`，审查 `FumenSoflanGroupListViewer` 及组模型/SGWI 持久化/组色链路，记录 SG-1～SG-18 问题；本次未修改功能代码。
- 说明：PR #46 的合并已按用户要求回滚（`af5d65ee` → `4c9bff35`），审查文档作为功能现状记录保留，供后续重新合并/修复时参考。
- Avalonia：本次未处理，不改变现有同步点。

## 2026-10-22：ISF 变速分组树状显示重构审查（WPF，仅文档）

- WPF：新增 `docs/soflan-group-tree-display-refactor-2026-10-22.md`，基于回滚后的 `master` 审查 `SoflanGroupWrapItemGroup`/ListView 伪树、选中/拖拽/SGWI 持久化链路，记录 TR-1～TR-14；方案对比扩展为 A～I（组即节点、Category+ID 徽标、组实体+可选 Category、DataGrid 主从表、自绘 TreeListView、两级主从、单层列表），并给出迁移/验收清单；本次未修改功能代码。
- Avalonia：本次未处理，不改变现有同步点。

## 2026-10-22：ISF 变速分组树状显示重构（WPF）

- WPF：按方案 E 实施：新增 `SoflanGroupTree`（组文件夹根级、不可嵌套，只含 Group）；组树布局改由项目文件 `SoflanGroupTree` 节点持有，谱面文件不再写组布局，旧 `[SGC]`/`[SGF]`/`[SGWI]` 仅保留读取迁移；查看器改为 TreeView + 节点 VM，保留 Design/Preview/Draw（D/P/S 标签、固定列并跨层级对齐，开关 ToolTip 随勾选/选中状态动态说明）、右侧明细（仅显示选中组，标题带组号，组文件夹不显示，移除 Group 列）、通用默认变速组名称/说明 ToolTip/禁止拖动、新建/重命名/删除/拖拽/撤销与稳定组色；统一属性编辑器/明细/设置等数值显示格式，消除 float 噪声；删除旧 `SoflanGroupWrapItem*` 双层模型。
- WPF：查看器新建按钮改为弹窗输入名称（空名使用「新的变速组」/「新的组文件夹」默认名），移除工具栏左侧输入框；五个工具栏按钮全部补 ToolTip，按钮文案区分为「新建变速组」/「新建组文件夹」；组色改为黄金角高饱和/高亮度配色；无活跃编辑器或无谱面时查看器整体禁用。
- 测试：`tests/soflan-group-tree` 241 项通过；Debug/Release 构建 0 error；后台 GUI 冒烟覆盖建组/删除撤销/重命名/空名默认命名/无编辑器禁用。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：PR #46 功能按方案 E 落地（WPF）

- WPF：重新实现 PR #46 需求中可落地部分：ISA 两点放置（查看器复选框、右下角精简面板、可选连续放置、吸附/规范化/目标组锁定/撤销）、中断取消（右键/Esc/切文档/切模式/批量模式/拖放/快速创建/窗口失焦）、组属性选择对话框（`IPropertyUIGenerator` + `SoflanGroupTypeUIView` + 树状 `SoflanGroupSelectDialogView`，按查看器层级/组色/#组号排列，默认滚动到当前组，双击直接确认，底部按钮用默认名快速新建组且不弹命名框）、有引用组删除对话框（转移/连同引用删除/取消，整体撤销恢复对象与选中）、删除组编号保留（避免撤销恢复冲突）、组属性变更即时重建绘制归属缓存；默认组显示名统一为「默认变速组」；新增三语文案。
- 文档：新增 `docs/pr46-implementation-status-2026-10-23.md`，逐条记录 A/B/C/D/E/F/G/H/U/S/V 状态、冲突处理与未实现项；G 组 15% 物件染色暂不实现。
- 测试：`tests/soflan-group-tree` 290 项通过；Debug/Release 构建 0 error；后台 GUI 冒烟覆盖复选框/紧凑面板/连续放置/两点创建/撤销重做/树状组选择对话框（颜色、#组号、默认滚动）/删除对话框/目标组失效。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：新增 ISF 面积重叠检查规则（WPF）

- WPF：新增 `IndividualSoflanAreaOverlap` 检查规则（`Modules/FumenCheckerListViewer/Base/DefaultRulesImpl/IndividualSoflanAreaOverlapCheckRule.cs`），级别 `problem`。把每个 `IndividualSoflanArea` 视为 T×X 矩形（`TGrid`/`EndIndicator` 两端范围归正），按 T 起点排序后扫描，只有两个矩形的交集面积为正才报（首尾相接、左右并排等仅共享边界的布局不报）；每条重叠对输出一条结果，`LocationDescription` 给出共享 T/X 范围，导航指向较晚开始的区域（`NavigateToObjectBehavior`，MCP `editor.check` 直接回报 objectId/tGrid）。三语文案（en/zh-Hans/ja）与 `Resources.Designer.cs` 同步新增 `IndividualSoflanAreaOverlap`。
- 文档：`Resources/skills/ongeki-fumen-editor/references/05-runtime-automation-and-mcp.md` 规则目录更新为 24 条（core 20 条），补充新规则说明。
- 测试：`tests/mcp-e2e/suites/70-metainfo-check.mjs` 的 ruleCount 断言更新为 24，并新增 8 条回归断言（跨组重叠命中 1 条、级别 problem、导航对象/家族/共享 T/X 范围、两种贴边方向不报、清理后无残留）；`node tests/mcp-e2e/run.mjs` 全套 1236/1236 通过；`dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug` 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：ISA 角标拖动重构（WPF）

- WPF：新增 `IndividualSoflanAreaInteractiveAction`（`Modules/FumenVisualEditor/ViewModels/Interactives/Impls/`）并注册到 `ObjectInteractiveManager`：ISA 本体与 End 的三角角标拖动时，被拖顶点直接跟手，允许暂时越过另一顶点（`EndIndicator.SetTGridUnclamped` 绕过 `End >= Start` 的常规夹取）；松手时 `IndividualSoflanArea.NormalizeCorners()` 把 X/T 最小值写回本体、最大值写回 End，保证「左下=本体、右上=End」。拖动结束写入一个 `LambdaUndoAction`，撤销/重做同时恢复两个顶点。
- WPF：`IndividualSoflanAreaDrawingTarget` 的矩形上下边改用 min/max，两个角标改为按各自对象坐标绘制并注册命中（原先固定注册在 bbox 左下/右上）；End 拖到另一侧后三角仍贴在指针下，不再出现角标与对象错位。
- 验证：脚本直接驱动交互 action（关闭磁吸以取精确坐标）覆盖 End 拖到本体左下、T 越过本体下方、本体拖过 End、本体+End 多选整体平移，结果分别为 `(-3,8)..(0,10)`、`(0,6)..(5,10)`、`(0,10)..(5,16)`、平移 `(+3,+2)`；MCP undo/redo 恢复正确；`node tests/mcp-e2e/run.mjs` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：修复 ISA 复制粘贴崩溃与端点丢失（WPF）

- WPF：`IndividualSoflanAreaEndIndicator.TGrid` setter 在 `RefIndividualSoflanArea` 暂时为 null（`CopyNew` 构造期）时不再抛 `NullReferenceException`。
- WPF：`IndividualSoflanArea.Copy` 补齐 `SoflanGroup` 与 End 的 X/T 复制；剪贴板来源过滤排除 `IndividualSoflanAreaEndIndicator`（与 Soflan/LaneBlockArea 的 End 一致），避免 End 被单独复制。
- WPF：粘贴时为 ISA End 应用与本体相同的水平/时间偏移（移动与镜像模式），粘贴完成后调用 `NormalizeCorners()` 保持「左下=本体、右上=End」。
- 验证：实际 GUI 实例中选中本体+End 后执行复制/粘贴，新区域组号、宽度、长度与源一致，剪贴板不再崩溃；MCP undo/redo 正常；`node tests/mcp-e2e/run.mjs` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：ISA 放置光标改为数据绑定（WPF）

- WPF：`FumenVisualEditorViewModel` 新增 `CurrentCursor`（ISA 放置期间为 `Cursors.Cross`，否则 null），随 `IsIsaPlacementActive` 一起通知；`OnRenderControlHostLoaded` 对动态创建的渲染控件用 `SetBinding(FrameworkElement.CursorProperty, ...)` 建立单向绑定，移除 `renderControlElement` 字段、`PropertyChanged` 手工订阅与 `UpdateIsaPlacementCursor`。
- 验证：GUI 实例脚本检查渲染控件的 `Cursor` 在开始 ISA 放置后为 `Cursors.Cross`、取消后恢复 null；`node tests/mcp-e2e/run.mjs` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：复制粘贴以 ISA 矩形中点为重心（WPF）

- WPF：剪贴板范围/重心计算新增 `GetMeasureCanvasPositions`：ISA 贡献 Start 与 End 两个顶点，`CalculateRangeCenter`、`CalculateXMirror`、`CalculateYMirror` 都按包围盒计算；单块 ISA 的中心即 Start-End 直线中点，粘贴时目标点落在矩形中心。
- 验证：GUI 脚本复制 Start(-3,10)-End(3,14) 的 ISA，以 (500, 源中心 Y+1000) 为目标粘贴，新区域中点 `(500.000009, 11285.714)` 与目标一致；`node tests/mcp-e2e/run.mjs` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：ISA 终点默认值改用可空背字段（WPF）

- WPF：移除 `IndividualSoflanAreaEndIndicator` 的 `tGridHasSet` 标志，改用 `TGrid?` 背字段：`null` 表示终点尚未显式设置，首次读取时复制起点并固化；显式赋值后终点独立。赋值统一走 `SetTGridCore`，保留 `End >= Start` 约束、子属性变化订阅与 `SetTGridUnclamped`。
- 验证：未设置终点的 ISA 读到 `End=Start`、`GridLength=0`；设置 `End=14` 后把 `Start` 移到 12，`End` 仍为 14；`node tests/mcp-e2e/run.mjs` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：合并 PR #43（WPF）

- 合并 `as2er:codex/grid-copy-paste-movement`（head `44417889`）到 `pr43` 分支，保留本树此前的 ISA/光标/剪贴板改动。
- PR 自带记录摘要：
  - 2026-10-18：Grid PR 补充修复——Start 横移与坐标 Unit/Grid 修改时先失效相邻路径缓存，再通知绘制与贴轨监听者；时间范围不变时不重建区间索引。
  - 2026-10-18：Tap/Hold 邻近换线——8 像素内换线，Hold 以头决定轨道，单拖头保持尾时间、同拖头尾保持时长；仅拖尾不换线。
  - 2026-10-21：WPF Grid 交互边界重构——选择拖动事务归入 ObjectInteractiveManager，剪贴板只调度纯 Grid 平移与交互吸附；LaneGeometry / LaneSwitchResolver 不引用 VM。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：快捷键配置键统一 kbd_editor_*（WPF）

- WPF：`BatchModeToggle` 配置键直接改名为 `kbd_editor_ToggleBatchMode`，新增三语资源（复用原「切换批量模式」文案）；不保留旧键兼容或迁移。
- 验证：隔离实例读回 `ConfigKey=kbd_editor_ToggleBatchMode`；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：修复 Playtime 的 Stopwatch tick 单位（WPF）

- WPF：`DefaultMusicPlayer.GetTime` 中 `TimeSpan.FromTicks(sw.ElapsedTicks)` 改为 `sw.Elapsed`。前者把 Stopwatch 原始 tick 当成 TimeSpan tick（1e7/s）使用，在 QPC 频率非 10MHz 的机器上 Playtime 会按 `Frequency/1e7` 缩放（页面报告的 2,533,209Hz 机器为 0.2533x，慢约 3.95 倍）。
- 验证：本机 `Stopwatch.Frequency=10,000,000`，修复前后均约 1.0x；模拟 2,533,209Hz 时错误写法 2 秒只得到 506.6ms，修复公式得到 2000ms；修复后实例播放 2 秒 `CurrentTime=2001.4ms`（ratio=1.00003）。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：Duration 使用重采样后的流时长（WPF）

- WPF：`DefaultMusicPlayer.Load` 最终 `Duration` 改为 `loadedReader.TotalTime`，与 `Seek`/`CurrentTime` 共用重采样后的流基准；原始 `processedProvider.Duration` 仅作为加载期临时值。
- 验证：30s fixture 上 `Duration=30000ms`，`Seek(Duration)` 后 `CurrentTime=30000ms`；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：暂停时以音频流位置校准时间（WPF）

- WPF：`DefaultMusicPlayer.Pause` 的 `pauseTime` 从 `GetTime()`（Stopwatch 推算）改为 `audioFileReader.CurrentTime`，与 `Seek` 的校准口径一致，避免多次暂停/继续累积误差。
- 验证：6 轮 500ms 播放/暂停，修复后前 4 轮 `CurrentTime` 精确为 500/1000/1500/2000ms；暂停按流位置校准后不再基于 Stopwatch 累积（输出缓冲预读可能带来一次约 10ms 前跳）。Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：Grid 拖动事务命名对齐实现（WPF）

- WPF：取消入口 `CancelPlacementTools` 改名 `AbortSelectionDrag`；`EndSelectionGridDrag(bool cancel)` 拆为 `CommitSelectionDrag()` / `AbortSelectionDrag()`；`MoveSelectionGridDrag` 改名 `UpdateSelectionDrag`，调用点与 `tests/grid-placement` 反射入口同步。
- WPF：拖动状态按实际语义重命名：`isSelectRangeDragging`→`isObjectDragStarted`、`currentDraggingActionId`→`autoScrollRedispatchToken`、`placementInputView`→`editorView`、`selectionGridDrag`→`activeSelectionDrag`、`selectionWasDirty`→`isDirtyBeforeDrag`、`selectionUndoActions/Count`→`undoStackBeforeDrag/undoCountBeforeDrag`、`selectionHasExternalChanges`→`hasExternalChangesDuringDrag`、`applyingSelection`→`isApplyingDragSnapshots`、`observedFumen`→`dragFumen`。
- 验证：`dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug/Release` 均 0 error；`dotnet run --project tests/grid-placement/GridPlacement.csproj -c Release -p:EditorAssemblyDirectory=<隔离 Release 输出>` 228 项通过。本机有运行中的编辑器锁定默认 `bin`，构建输出重定向到未被版本控制的 `artifacts/`。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：音频缓冲读取改为块复制（WPF）

- WPF：`BufferWaveStream.Read(byte[])` / `Read(float[])` 从逐元素循环改为 `Buffer.BlockCopy` / `Span.CopyTo`，保持 `Position` 前进量与返回值语义；末尾不足和越界读取仍返回实际数量。
- 验证：临时实例反射构造缓冲流，byte/float 读取内容、Position 前进与末尾读取均正确；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：Seek 按 BlockAlign 对齐（WPF）

- WPF：`DefaultMusicPlayer.Seek` 的字节偏移向下对齐到 `WaveFormat.BlockAlign`，避免 `ConvertTGridToAudioTime` 的 ticks 精度落到半帧/半样本导致左右声道错位。
- 验证：Seek 到 123.456ms 后位置为 47400 字节（8 的倍数），`CurrentTime=123.4375ms`（误差小于一个样本帧）；继续播放正常。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：新增基于设备播放位置的 DefaultMusicPlayer_new（WPF）

- WPF：新增 `DefaultMusicPlayer_new`（未接入 `NAudioManager`，生产仍用旧实现）。时间源完全改为 `IWavePosition.GetPosition()` / `OutputWaveFormat`，不使用 Stopwatch 或旧逻辑回退；`Play` / `Pause` / `Seek` 以设备字节位置重建基准，并处理 WaveOut 32 位位置回绕；`NAudioManager` 增加 internal `TryGetOutputPosition`。
- 验证：临时切换新实现后，6 轮 500ms 播放/暂停的 `CurrentTime` 与真实时间差约 2ms 且不累积；Seek 123.456ms 对齐到 123.4375ms（8 字节）；继续播放正常；Debug 构建 0 error。生产已恢复旧播放器。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-23：启用设备位置播放器，ASIO 回退旧实现（WPF）

- WPF：`NAudioManager.LoadAudioAsync` 在非 ASIO 且输出设备实现 `IWavePosition` 时使用 `DefaultMusicPlayer_new`；ASIO 继续使用旧 `DefaultMusicPlayer`。新增播放器实现选择日志。
- 验证：Wasapi 实例日志显示 `Music player implementation: DefaultMusicPlayer_new`；播放 1 秒 `CurrentTime=1001.8ms`（ratio 0.9987），Seek 123.456ms 对齐到 123.4375ms；`node tests/mcp-e2e/run.mjs --url ...` 1236/1236 通过；Debug 构建 0 error。ASIO 分支因本机无驱动未实机验证，按类型显式回退。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：输入中断监听移出 View（WPF）

- WPF：`FumenVisualEditorView.xaml.cs` 恢复为无事件代码，View 不再新增或修改交互逻辑。原由 View 转发的窗口失焦、工具箱拖入、卸载/DataContext 变化改由 ViewModel 处理：
  - `HookWindowFocus` 监听 `Window.Deactivated` 与 `DragDrop.PreviewDragEnterEvent`（工具箱数据格式），触发 `AbortSelectionDrag`；
  - `OnPlacementActiveEditorChanged` 在编辑器切走时触发 `AbortSelectionDrag`；
  - `Dispose` / `OnDeactivateAsync` 原有取消路径保持不变。
- 验证：View 与 master 完全一致；`node tests/mcp-e2e/run.mjs --url ...` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：DragEnter 注册迁入渲染控件生命周期（WPF）

- WPF：`FumenVisualEditorView` 保持无事件代码；工具箱拖入监听从窗口 `PreviewDragEnterEvent` 改为渲染控件 `DragDrop.DragEnterEvent`。
- `OnRenderControlHostLoaded` 注册 `renderControl.AddHandler(DragDrop.DragEnterEvent, ...)` 并保存控件与委托；`Dispose` 中注销。窗口失焦仍由 `HookWindowFocus` 的 `Window.Deactivated` 处理。
- 验证：View 与 master 一致；`node tests/mcp-e2e/run.mjs --url ...` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：DragEnter 改用 Message.SetAttach 绑定（WPF）

- WPF：渲染控件的 `DragEnter` 不再手动 `AddHandler`/`RemoveHandler`，改为在既有 `Message.SetAttach(renderControl, ...)` 字符串中增加 `[Event DragEnter] = [Action OnToolboxDragEntered($executionContext)]`；`OnToolboxDragEntered` 改为 `ActionExecutionContext` 签名，仍检查 `ToolboxDragDrop.DataFormat` 后调用 `AbortSelectionDrag()`。
- 删除 `renderControlElement` / `toolboxDragEnterHandler` 字段及 `Dispose` 中的手动注销。
- 验证：View 与 master 一致；Caliburn attach 无解析错误；`node tests/mcp-e2e/run.mjs --url ...` 1236/1236 通过；Debug 构建 0 error。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：验证 OnDeactivateAsync 不能替代 Window.Deactivated（WPF）

- 实验：临时删除 `FumenVisualEditorViewModel.IsaPlacement.cs` 的 `focusedWindow` / `HookWindowFocus` / `UnhookWindowFocus` / `OnFocusedWindowDeactivated` 及三处调用，仅依赖 `OnDeactivateAsync(close:false)` 的现有清理；随后按精确反向编辑恢复。
- 新增 `tests/bullet-placement/DeactivationChecks.cs` 并在 `Program.cs` 注册，用隐藏 `ProbeWindow.OnDeactivated` 触发真实 `Window.Deactivated`，不显示/激活窗口。覆盖 ISA 两阶段取消与迟到取点、真实选择拖动事务回滚（T 坐标、`IsDirty`、撤销栈、自动滚动重派发）、批量放置不受影响、文档失活/文档切换/关闭路径、重复失活与重复激活。
- 实测结论：替换后 19 条窗口失焦断言失败——ISA 阶段/光标/目标组残留、迟到取点在 `WaitingSecondPoint` 下仍会创建 ISA 与撤销记录、拖动事务不中止不回滚、重新激活后不再监听窗口；文档切换与关闭场景全部通过。故 `OnDeactivateAsync` 只覆盖文档生命周期，不能等价取代窗口失焦处理，生产实现保留原样。
- 验证：`dotnet build OngekiFumenEditor.sln -c Debug` 0 error；`tests/bullet-placement` 282/282；`tests/grid-placement` 228/228；自构建隔离实例 `node tests/mcp-e2e/run.mjs --url http://127.0.0.1:39281/mcp` 1236/1236。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：补充 IDeactivate 生命周期事件契约测试（WPF）

- 新增 `tests/bullet-placement/LifecycleEventChecks.cs` 并在 `Program.cs` 注册，用真实编辑器固定 Caliburn `AttemptingDeactivation` / `Deactivated` 的行为：
  - 窗口失焦不触发任一事件，文档保持 active；ISA 仍由窗口失焦专用路径取消。
  - 文档切换/失活：`AttemptingDeactivation`（IsActive=true，ISA 仍在）→ 清理 → IsActive=false → `Deactivated`（WasClosed=false），各一次。
  - 关闭：两次事件均 WasClosed=true，Dispose 先于 Deactivated；重复关闭会再次触发事件（IsInitialized 仍为 true），但 Dispose 与销毁通知只发生一次。
  - `AttemptingDeactivation` 抛异常会阻止清理与 IsActive 转换；`Deactivated` 抛异常发生在清理之后，移除处理器后可重新激活。
  - `Deactivated` 支持异步 handler，`DeactivateAsync` 会等待其 Task 完成后才结束。
- 验证：`tests/bullet-placement` 320/320；生产代码未改动。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-24：Debug 构建转发 Caliburn.Micro 日志（WPF）

- WPF：新增 `Utils/CaliburnLogAdapter.cs`（`#if DEBUG`），实现 `Caliburn.Micro.ILog` 并把 `Info` / `Warn` / `Error` 转发到项目 `Utils.Log`；消息保留 Caliburn 类型名（`{类型全名}: {内容}`），日志调用者标签固定为 `CMLog`（输出 `<CMLog:行号>`），格式化失败或日志管道未就绪时退回 `Debug.WriteLine`，不影响 Caliburn 控制流。
- `AppBootstrapper.Configure` 在日志输出与 `CoreLog` 解析器初始化后设置 `LogManager.GetLog = type => new CaliburnLogAdapter(type)`，同样位于 `#if DEBUG`。
- 验证：Debug 实例启动日志出现 `<CMLog:41> Caliburn.Micro.NameTransformer/Screen/ViewModelBinder ...`；`tests/bullet-placement` 320/320；`dotnet build -c Release` 0 error，Release 产物不含 `CaliburnLogAdapter`（条件编译生效）。
- Avalonia：本次未移植，不改变现有同步点。

## 2026-10-25：修复 Playtime 跳末尾与音效线程占用（WPF）

- `DefaultMusicPlayer_new.GetExpandedOutputPosition`：只有接近 32 位边界的回退才按 `WaveOut` 计数器回绕补 `2^32`；其余回退视为 `WasapiOut` 的 64 位位置抖动，同步调整 `devicePositionBase`，避免 `CurrentTime` 瞬间跳约 3.4 小时、视图被 clamp 到音频末尾。
- `AudioPlayerToolViewerViewModel.OnPlaybackFinished`：删除播放完成后强制 `ScrollTo(Duration - 1s)`，避免把视图钉在末尾并通过自动保存写回工程。
- `FumenVisualEditorViewModel`：加载与保存 `RememberLastDisplayTime` 时都 clamp 到 `[0, AudioDuration - 100ms]`，防止越界值写入工程或再次打开时停在末尾。
- `DefaultFumenSoundPlayer`：`UpdateInternal` 返回距离下一个点音效/节拍器/循环音效开始或结束的毫秒数，`OnUpdate` 据此睡到触发前 5 ms（单次上限 50 ms，空闲 5 ms 轮询），消除常驻忙等且不延迟触发；`Prepare` 改用 `AbortAsync()`，不再在 UI 线程同步 `Wait()` 旧音效线程。
- 验证：Debug 构建 0 error；`UpdateInternal` 调度断言 6/6（点音效间隔、消费后空闲、循环音效开始/结束、越过区间）；`tests/bullet-placement` 320/320；`tests/grid-placement` 228/228；自构建隔离实例 `node tests/mcp-e2e/run.mjs --url http://127.0.0.1:39281/mcp` 1236/1236。WASAPI 位置回退修复依据排查报告的实测（切 WaveOut 即正常、机制确认），本轮未重复音频设备长播实测。
- Avalonia：本次未移植，不改变现有同步点。
