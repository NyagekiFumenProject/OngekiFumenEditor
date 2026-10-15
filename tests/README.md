# 四项 WPF 编辑功能的独立回归

本轮覆盖以下行为：

- 复制粘贴与混选拖动以共同参考点进行 TGrid/XGrid 平移，跨 BPM／拍号仍保留相对 Grid，合法横坐标不钳制到 ±24；持续物件的头尾和附轨恢复、撤销、镜像及曲线控制点应保持一致。
- 子弹／Bell 模板快速放置支持预览、键盘调整、连续放置、取消互斥及输入控件保护。
- ISA 通过两个对角点创建；必须选择谱面已有的有效组，属性浏览器从已有组下拉选择，混选时只修改适用物件。新组名称可空，默认显示编号；被引用组可转移引用、删除全部引用或取消。设计模式混入 15% 组色并在几何／组归属变化后刷新缓存。
- 判定范围可视化可以切换，轨道断点保留；不改变谱面判定数据。

## 运行条件

需要 Windows 与 .NET 10 SDK，以及已成功构建、包含对应修改的 WPF 编辑器程序集和同目录运行依赖。三套项目不加入生产解决方案，不创建窗口、不启动编辑器，不加载音频，不依赖机器绝对路径。`bullet-placement` 包含 WPF 控件及反射状态检查，但不展示控件或窗口；这些程序不能替代真实交互和画面验收。

先构建编辑器，再从仓库根运行以下 PowerShell 命令。若使用其他配置或隔离输出，只修改 `$editorAssemblyDirectory` 指向实际完整输出目录：

```powershell
$editorAssemblyDirectory = Join-Path (Get-Location) 'OngekiFumenEditor/bin/Release/net10.0-windows'
dotnet run --project tests/grid-placement/GridPlacement.csproj -c Release "-p:EditorAssemblyDirectory=$editorAssemblyDirectory"
dotnet run --project tests/bullet-placement/BulletPlacement.csproj -c Release "-p:EditorAssemblyDirectory=$editorAssemblyDirectory"
dotnet run --project tests/isa-placement/IsaPlacement.csproj -c Release "-p:EditorAssemblyDirectory=$editorAssemblyDirectory"
```

`grid-placement` 检查相对 Grid、复制隔离、镜像、控制点和混选拖动边界；`bullet-placement` 检查快速放置状态／输入保护及判定带几何；`isa-placement` 检查两点状态、有效组编号、删除／替代及撤销顺序、组色和缓存事件失效。

## 本轮验证记录

使用 .NET SDK 10.0.401 与固定提交 `77aba07f` 的 Gemini 源码真实编译后，Grid 71 项、ISA 29 项及 Bullet 无窗口回归均通过。

上游原始 `DefaultSkiaBeamDrawing.cs` 的 `SetMatrix(matrix)` 在当前编译器与项目指定的 SkiaSharp 3.119.2 组合下报 CS0619。为执行验证，仅在仓库外部副本将调用改为 `SetMatrix(in matrix)`；本 PR 未修改该文件。

原生 SDK 11 构建及真实 GUI 交互／画面尚未验证。
