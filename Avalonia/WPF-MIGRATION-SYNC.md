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
