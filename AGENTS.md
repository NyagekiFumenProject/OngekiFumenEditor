# AGENTS.md — 仓库级

本仓库同时装两棵树：`OngekiFumenEditor/`（WPF 原项目，`net10.0-windows`）与 `Avalonia/`（Avalonia 移植）。
二者同处一个 git 仓库（真实 gitdir 在仓库根），`master` 自从 2026-09-29 起同时包含两棵树。

## 跨树差异分析前必读

`Avalonia/WPF-MIGRATION-SYNC.md` 是「Avalonia 同步 WPF 到哪个 commit」的**唯一权威记录**。
任何 WPF ↔ Avalonia 的功能/内容差异、迁移缺口分析，先读它取基线；完成后必须回写该文件。

该文件里两个基线**不得混用**：

- 分叉基线 `a00e90ab4`（2026-01-06，Avalonia 树由该状态复制）；
- 同步点 `8b2940785`（2026-09-19，最后一次把 WPF `origin/master` 整体合入 Avalonia 线）。

Avalonia 侧的工程约定见 `Avalonia/AGENTS.md`；迁移报告与审计见 `Avalonia/docs/`。

## 改动范围约定（默认仅 WPF，Avalonia 不动）

除非用户**明确要求**，否则本文件夹（仓库根）的工作区改动与工作**默认不考虑 Avalonia 那边**：

- 只改 `OngekiFumenEditor/`（WPF 树）；**不要**顺手修改 `Avalonia/` 下的代码、资源或配置——即使那边存在同构问题；
- 工作完成后**仅**回写 `Avalonia/WPF-MIGRATION-SYNC.md`：记录本次 WPF 侧改动、注明 Avalonia 目前尚未移植或本次未处理，并声明不改变同步点；
- 只有用户明确点名 Avalonia 时，才处理 `Avalonia/` 树，且两棵树的同步状态按该文件协议一并回写。

## 解决方案约定

`OngekiFumenEditor.sln` **只装生产工程，禁止加入测试类项目**（`*Tests`、测试/回归 harness 等）。
这类工程按 csproj 单独调用，例如 `dotnet run --project OngekiFumenEditor.FumenCheckerTests -c Release`
（与 `Avalonia/AGENTS.md` 中「解决方案只承载生产工程，tests/benchmarks 按 csproj 单独调用」同一约定）。

## 测试启动约定

自行启动编辑器（或任何带窗口的程序）做测试时，**必须固定在后台运行，禁止把它带到前台干扰用户工作**：
先 `hub start`（`pty: false`）启动，就绪后**立刻**最小化其主窗口（`SW_MINIMIZE = 6`）。最小化一行：

```powershell
$s='[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);'; Add-Type -MemberDefinition $s -Name W -Namespace T; Get-Process OngekiFumenEditor | ? MainWindowHandle -ne 0 | % { [T.W]::ShowWindow($_.MainWindowHandle, 6) }
```

不要用会激活/置顶窗口的方式启动；测试结束用 `hub stop <name>` 收尾。

**只操作自己构建的实例**：最小化/杀进程一律按可执行文件路径过滤（`F:\Source\OngekiFumenEditor\*`），
用户桌面部署的那份（`C:\Users\mikir\Desktop\OngekiFumenEditor\`）绝不能碰：

```powershell
$s='[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n); [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);'; Add-Type -MemberDefinition $s -Name W -Namespace T; Get-Process OngekiFumenEditor | ? { $_.Path -like 'F:\Source\OngekiFumenEditor\*' -and $_.MainWindowHandle -ne 0 -and -not [T.W]::IsIconic($_.MainWindowHandle) } | % { [T.W]::ShowWindow($_.MainWindowHandle, 6) }
```

要在用户实例在跑的同时起自己的实例：把 `OngekiFumenEditor/bin/Debug/net10.0-windows/config.json` 的
`EnableMultiInstances` 临时置 `true`（MCP 端口会退到 39282），测完改回 `false`。

用编辑器 GUI/MCP 驱动测试时**只打开 `tests/mcp-e2e/.tmp/` 里带音频的完整副本**（runner 自己生成），
不要直接 open 裸 `.ogkr`（例如 `OngekiFumenEditor.Benchmark/Data/FumenSamples/` 下的样例）——
缺音频会弹「等待手动选择音频文件」模态框，既阻塞脚本又会打扰用户。
