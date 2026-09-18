# OngekiFumenEditor.Benchmark

测量 OngekiFumenEditor 中与 UI 无关的性能热点。基于 BenchmarkDotNet,通过 `new App(false) + new AppBootstrapper(false)` 在不打开窗口的前提下激活 MEF/IoC 容器,然后调用主项目的纯计算入口。

## 测量类

| 类 | 覆盖范围 |
| --- | --- |
| `ParsingBenchmarks` | OGKR / Nyageki / 全部样本的反序列化 |
| `DisplayableEnumerationBenchmarks` | `OngekiFumen.GetAllDisplayableObjects()`、范围内枚举、`ConnectableStartObject.GetDisplayableObjects()` |
| `CollectionQueryBenchmarks` | `BinaryFindRange` / `GetVisibleStartObjects` / Soflan 区间查询 |
| `DrawPlayableAreaHelperNewP1BoundaryBenchmarks` | new playfield helper P1: boundary 查询重复、候选墙轨缓存、LINQ/数组分配 |
| `DrawPlayableAreaHelperNewP1SampleCollectionBenchmarks` | new playfield helper P1: 墙轨节点采样收集的全谱扫描与索引查询 |

样本数据(`Data/FumenSamples/*.ogkr|*.nyageki`)以嵌入资源形式打进程序集,运行时不依赖磁盘上的样本目录。

## 运行

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark
```

程序启动后会列出所有 Benchmark 类,要求用户输入编号选择:

```
请选择要测量的 Benchmark 类:
  [0] 全部
  [1] CollectionQueryBenchmarks
  [2] DisplayableEnumerationBenchmarks
  [3] ParsingBenchmarks
输入编号(多个用逗号分隔,直接回车 = 全部):
```

也可以传 `--filter` 走 BenchmarkSwitcher CLI 路径跳过交互菜单:

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --filter *ParsingBenchmarks*
```

加速试跑(`Dry` job 单次迭代,适合冒烟):

```powershell
dotnet run -c Release --project .\OngekiFumenEditor.Benchmark -- --job dry
```

支持的 `--job` 预设:`dry` / `short` / `medium` / `long` / `verylong` / `default`。

## 基线对比

每跑完一组 benchmark,程序会:

1. 从 `BenchmarkDotNet.Artifacts/Baselines/{ClassFullName}.json` 加载上次保存的基线
2. 与当次结果做差异对比,输出 Mean / Allocated / Δ% 表格,状态 `OK` / `REGRESSION` / `IMPROVED` / `NEW` / `GONE`
3. 询问 `Save current results as new baseline(s)? [y/N]`
   - `y`/`yes`:把当前结果写回同一个 JSON 文件(覆写)
   - 其它:跳过保存
   - stdin 被重定向(管道、CI)时自动跳过保存

默认判定阈值:Mean ±5%,Allocated bytes ±1%。

文件名严格按 plan.md 第 5 条要求,以**类全名**为基础,例如:

```
BenchmarkDotNet.Artifacts/
└── Baselines/
    ├── OngekiFumenEditor.Benchmark.Benchmarks.ParsingBenchmarks.json
    ├── OngekiFumenEditor.Benchmark.Benchmarks.DisplayableEnumerationBenchmarks.json
    └── OngekiFumenEditor.Benchmark.Benchmarks.CollectionQueryBenchmarks.json
```

颜色输出走 ANSI 转义,stdout 被重定向或环境变量 `NO_COLOR` 非空时自动关闭。

## 构建

```powershell
dotnet build .\OngekiFumenEditor.Benchmark\OngekiFumenEditor.Benchmark.csproj -c Release
```

注意:`csproj` 引用主项目时显式 `AdditionalProperties="DisableFody=true"`,绕开 Fody Costura 与 BenchmarkDotNet 默认工具链的冲突;同时 `Program.cs` 用 `InProcessEmitToolchain` 在当前进程内 emit IL 跑 benchmark,跳过 wrapper 项目构建。
