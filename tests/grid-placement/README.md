# Grid 放置回归

无需启动编辑器窗口。先构建 WPF 编辑器，再运行：

```powershell
dotnet run --project tests/grid-placement/GridPlacement.csproj -c Release
```

使用其他编辑器构建输出时，传入 `-p:EditorAssemblyDirectory=输出目录`。默认目录是仓库内的 `OngekiFumenEditor/bin/Release/net10.0-windows`，测试未加入解决方案。

覆盖原始 Grid 间距、跨 BPM/拍号的参考点吸附、负 X、长时间轴精度、整组负时间与溢出拒绝、Hold 头尾约束、未选节点保持不动，以及快照恢复与重放。
