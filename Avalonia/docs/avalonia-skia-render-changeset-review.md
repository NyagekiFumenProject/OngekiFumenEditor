# Avalonia.Skia 渲染改造代码复查报告

- **检查日期**：2026-07-31
- **后续修复日期**：2026-08-06
- **复查对象**：渲染实现固定为 Avalonia.Skia + SkiaSharp 的工作区改动（未提交），即 [WPF → Avalonia 迁移状态报告](wpf-to-avalonia-migration-status.md) 中“谱面渲染：已接入、待运行验证”对应的改动集
- **复查性质**：最初为静态代码审查；2026-08-06 已补充运行测试和完整构建验证
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

改动在编译层面是自洽的：2026-08-06 对当前工作区执行完整非增量构建，结果为 0 个错误、81 个警告；矩阵拼接、逻辑像素口径、`Save`/`Restore` 配对、接口转换和设置项清理均核对无误。

后续运行核对修正了原报告对 `SKCanvas.Clear` 的判断：`Clear` 使用 `Src` 混合填充当前 clip，并非无条件清除整个画布。真正需要消除的是代码对 Avalonia lease 隐式 clip 的依赖，以及 custom draw operation 使用物理像素 Bounds 的坐标错误。2026-08-06 已完成显式帧裁剪、逻辑 Bounds 和画布状态恢复，并加入共享表面像素回归测试。

## 严重问题

### S1. 已修复：显式限制 leased canvas 的绘制边界

位置：[`DefaultSkiaRenderContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs) `CleanRender` 方法。

Skia 的 `Clear` 会以 `Src` 混合填充当前 clip。旧实现的问题是直接相信 Avalonia lease 已经提供正确的控件 clip，同时 `drawOperation.Bounds` 还错误地乘以 `RenderScaling`，导致逻辑坐标与物理像素口径混用。共享合成表面上的绘制边界因此缺少由本控件负责的硬保证。

当前修复在 `RenderFrame` 外层使用 `Save`、控件逻辑 Bounds 的 `ClipRect` 和 `RestoreToCount` 包住整个订阅回调；`CleanRender` 使用显式 `SKBlendMode.Src` 的 `DrawColor`。`SkiaRenderControl_CleanFrame_DoesNotOverwriteSiblingControl` 会同时断言编辑器区域被清屏、相邻 Avalonia 控件像素保持不变。

### S2. 渲染帧内异常直接打穿 Avalonia 渲染线程

位置：[`DefaultSkiaRenderContext.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaRenderContext.cs) `RenderFrame` 方法。

`RenderFrame` 只有 try/finally 没有 catch。任何绘制异常都会从 `ICustomDrawOperation.Render` 冒泡到合成器渲染线程，可能直接终止进程；且 `AvaloniaSkiaRenderControl.Render` 在 `IsRendering` 期间每帧投递 `InvalidateVisual`，会形成“每帧抛异常”的死循环。

当前最大的隐患源是 `UnsupportedSkiaSvgDrawing`（[`DefaultSkiaDrawingManagerImpl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/DefaultSkiaDrawingManagerImpl.cs)）主动抛出 `NotSupportedException`。该类型当前不可达（`EditorSvgObjectControlProvider` 模块被 `Compile Remove` 排除），但一旦该模块重新启用，任何含 SVG 对象的谱面都会触发上述崩溃循环。

此外，异常路径下 `BeforeRender` 和各绘制类 `OnBegin` 的 `canvas.Save()` 没有配对的 `Restore`，状态栈泄漏会累积到后续帧（lease 复用同一底层画布）。

修复方向：`RenderFrame` 内 catch 异常并记录日志；`UnsupportedSkiaSvgDrawing` 改为“记录一次并跳过”，与 S2 的防护形成双层保险。

## 中等问题

### M1. 已修复：渲染控件可接收指针输入

位置：[`AvaloniaSkiaRenderControl.cs`](../src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/AvaloniaSkiaRenderControl.cs) `SkiaDrawOperation.HitTest`。

`HitTest` 已改为 `Bounds.Contains(p)`，渲染控件同时设为可聚焦；既有 `PointerPressed`、`PointerMoved`、`PointerReleased` 和 `PointerWheelChanged` 接线现在能够收到 Avalonia 路由输入。`SkiaRenderControl_PointerInput_ReachesRenderSurface` 使用 Headless 平台向真实窗口坐标投递点击和滚轮事件，验证两类事件都到达渲染控件。

编辑器时间轴范围也已按原 WPF 公式恢复，并在音频时长、BPM/拍号、显示缩放及控件尺寸变化时重算，避免输入恢复后仍被零高度范围截断。

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
- `AvaloniaSkiaRenderControl.Render` 的 `drawOperation.Bounds` 已改为控件逻辑 Bounds；Avalonia 负责把逻辑坐标映射到目标 DPI，不再由 custom operation 重复乘以 `RenderScaling`。
- `CommonSkiaDrawingBase.OnBegin` 使用 `canvas.Concat` 在 lease 已有矩阵（含控件偏移、clip、DPI 变换）上拼接编辑器矩阵，方向正确；`ViewWidth`/`ViewHeight` 使用逻辑像素（`Bounds`）与 lease 的 DPI 变换配合正确。
- `DefaultSkiaDrawingManagerImpl` 中 `initTaskSource` 由 `InitializeRenderControl` 置位、`WaitForInitializationIsDone` 等待，调用链完整。
- 改动文件编码均为 UTF-8；3 个带 BOM 的文件为原有文件，按“修改已有文件保持原编码”约定未改动。

## 建议处理顺序

1. S1 已完成；S2 的异常记录与 SVG 绘制降级仍需单独处理。当前外层 `RestoreToCount` 已保证异常路径不会把 Skia 状态栈遗留到下一帧。
2. M1 与时间轴范围初始化已完成；M2 的渲染线程属性通知风险仍需单独处理（对应迁移状态报告 P2/P3 阶段）。
3. 后续修复继续执行完整构建和渲染回归测试；本轮基线为完整构建 0 个错误、81 个警告，Core 测试 282/282、Desktop 测试 106/106 通过。
