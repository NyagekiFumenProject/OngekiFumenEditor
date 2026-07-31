# Avalonia.Skia 渲染改造代码复查报告

- **检查日期**：2026-07-31
- **复查对象**：渲染实现固定为 Avalonia.Skia + SkiaSharp 的工作区改动（未提交），即 [WPF → Avalonia 迁移状态报告](wpf-to-avalonia-migration-status.md) 中“谱面渲染：已接入、待运行验证”对应的改动集
- **复查性质**：静态代码审查；应用尚被 5 个迁移范围外编译错误阻断，所有结论未经运行时验证
- **涉及文件**：
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/AvaloniaSkiaRenderControl.cs`（新增）
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs`
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs`
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/DefaultRenderManager.cs`
  - `src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/Drawing/`（CommonSkiaDrawingBase、Beam、Circle、Line、Texture）
  - `src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs`
  - `src/OngekiFumenEditor.Avalonia/Models/Settings/ProgramSetting.cs`、`Kernel/SettingPages/Program/Views/ProgramSettingView.axaml`
  - `src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj`、`src/Directory.Packages.props`

## 结论

改动在编译层面是自洽的：完整重建维持 5 个迁移范围外错误、45 个警告的基线，渲染改动没有引入新错误；矩阵拼接、逻辑像素口径、`Save`/`Restore` 配对、接口转换和设置项清理均核对无误。

但运行时层面存在 2 个必然暴露的严重问题（共享表面被整屏清空、渲染线程异常崩溃）、2 个中等问题和若干观察项，需要在运行验证前修复严重项。

## 严重问题

### S1. `CleanRender` 使用 `SKCanvas.Clear`，会抹掉整个窗口

位置：[`DefaultSkiaRenderContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs) `CleanRender` 方法。

`SKCanvas.Clear` 忽略当前 clip 和 matrix，清空整个渲染目标。WPF 原版同样调用 `Canvas.Clear`，但当时 Skia 绘制在控件独占的 surface（`SkiaRenderControlBase.CurrentRenderSurface`）上，行为正确；迁移到 Avalonia 后，lease 得到的画布是整个窗口共享的合成表面，编辑器只是其中一个 custom draw op。每帧 `Clear` 会把 z-order 位于编辑器之前的所有 UI（窗口背景、侧面板等）抹成清屏色。

修复方向：改用 `canvas.DrawPaint(paint)`（尊重 `ClipToBounds` 产生的 clip，只填充控件区域），或绘制一个覆盖控件 Bounds 的矩形。

### S2. 渲染帧内异常直接打穿 Avalonia 渲染线程

位置：[`DefaultSkiaRenderContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs) `RenderFrame` 方法。

`RenderFrame` 只有 try/finally 没有 catch。任何绘制异常都会从 `ICustomDrawOperation.Render` 冒泡到合成器渲染线程，可能直接终止进程；且 `AvaloniaSkiaRenderControl.Render` 在 `IsRendering` 期间每帧投递 `InvalidateVisual`，会形成“每帧抛异常”的死循环。

当前最大的隐患源是 `UnsupportedSkiaSvgDrawing`（[`DefaultSkiaDrawingManagerImpl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs)）主动抛出 `NotSupportedException`。该类型当前不可达（`EditorSvgObjectControlProvider` 模块被 `Compile Remove` 排除），但一旦该模块重新启用，任何含 SVG 对象的谱面都会触发上述崩溃循环。

此外，异常路径下 `BeforeRender` 和各绘制类 `OnBegin` 的 `canvas.Save()` 没有配对的 `Restore`，状态栈泄漏会累积到后续帧（lease 复用同一底层画布）。

修复方向：`RenderFrame` 内 catch 异常并记录日志；`UnsupportedSkiaSvgDrawing` 改为“记录一次并跳过”，与 S2 的防护形成双层保险。

## 中等问题

### M1. 渲染控件对命中测试透明

位置：[`AvaloniaSkiaRenderControl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/AvaloniaSkiaRenderControl.cs) `SkiaDrawOperation.HitTest` 恒返回 `false`，控件也没有设置 `Background`。

指针事件无法命中该控件。改动前 `CreateRenderControl` 返回无背景的 `Panel`，同样无法命中，因此不算回归；且输入接线本身仍是 WPF 事件名（`FumenVisualEditorViewModel.Drawing.cs:921` 的 `Message.SetAttach` 使用 `PreviewMouseDown`/`MouseMove`），属于迁移状态报告中已登记的欠账。但既然现在有了专用控件类，建议在接入 Pointer 事件时一并修复：`HitTest` 返回 `Bounds.Contains(p)`，或为控件设置透明背景。

### M2. `OnRender` 的线程上下文发生变化

原来的 `OnRender` 在独立 `Task.Run` 循环中触发，现在改在 Avalonia 渲染线程上触发（`RenderFrame` 内调用）。订阅链中 ViewModel 的 `Render` 方法存在以下跨线程访问：

- `ObjectPool` 复用（原来也是后台线程，不算新回归）；
- 读写被 UI 线程 `RenderControl_SizeChanged` 同时写入的 `ViewWidth`/`ViewHeight`（良性竞争，float 读写）；
- 经 `PerfomenceMonitor` 间接触发 `PropertyChanged`，从渲染线程抛出对 Avalonia 绑定有跨线程风险。

新影响：渲染耗时会直接阻塞整个窗口的合成帧（原来只占用独立线程）。

## 轻微问题与观察项

- `DefaultSkiaBeamDrawing` 与 WPF 原版有两处差异，均为改进：
  - WPF 原版计算了 `fixedColor.W *= alpha` 却错误地把 `color` 传给 `CreateSolidColorMatrix`（`fixedColor` 未使用，透明度渐变实际不生效）；新版使用了 `fixedColor`，等于修复了原版 bug，beam 淡入淡出行为会与 WPF 版不同。
  - 旋转实现从 `SetMatrix`（会覆盖 `OnBegin` 拼好的编辑器矩阵）改为 `RotateDegrees`（在现有矩阵上拼接），更正确。
- `(IStaticVBODrawing)SimpleLineDrawing` 的强转是安全的：`ISimpleLineDrawing : IStaticVBODrawing`（`ISimpleLineDrawing.cs:9`），运行时不会 `InvalidCastException`。
- `AvaloniaSkiaRenderControl.Render` 中 `drawOperation.Bounds` 乘以 `RenderScaling`（物理像素），与用户指定的参考实现（ReOsuStoryboardPlayer.Avalonia `StoryboardPlayer.axaml.cs`）一致；即使该 API 期望逻辑坐标，后果也只是高 DPI 下脏区域偏大，无害。
- `CommonSkiaDrawingBase.OnBegin` 使用 `canvas.Concat` 在 lease 已有矩阵（含控件偏移、clip、DPI 变换）上拼接编辑器矩阵，方向正确；`ViewWidth`/`ViewHeight` 使用逻辑像素（`Bounds`）与 lease 的 DPI 变换配合正确。
- `DefaultSkiaDrawingManagerImpl` 中 `initTaskSource` 由 `InitializeRenderControl` 置位、`WaitForInitializationIsDone` 等待，调用链完整。
- 改动文件编码均为 UTF-8；3 个带 BOM 的文件为原有文件，按“修改已有文件保持原编码”约定未改动。

## 建议处理顺序

1. 修复 S1（清屏方式）和 S2（异常防护 + SVG 绘制降级），这两个问题在首次运行验证时必然暴露。
2. M1、M2 连同输入接线和绑定的线程迁移一起做（对应迁移状态报告 P2/P3 阶段）。
3. 修复后重新执行 `dotnet build OngekiFumenEditor.Avalonia.sln --no-restore -t:Rebuild -m:1 -v:minimal`，确认仍维持 5 错误 / 45 警告基线，并同步更新 [迁移状态报告](wpf-to-avalonia-migration-status.md)。
