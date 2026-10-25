# NAudio 2.3 → 3.3 升级评估

日期：2026-10-25
范围：WPF 树 `OngekiFumenEditor`（`Kernel/Audio/NAudioImpl` 及相关调用点）
结论：**值得升级，但属于中等规模迁移；本轮不升级，先保留 2.3.0。**

## 1. 结论摘要

- NAudio 3 是 major 版本：包拆分、最低目标框架 `net9.0`、`Read` API 改为 `Span<T>`、WASAPI 新旧 API 并存。
- 本项目满足 `net10.0-windows` 框架要求，主要成本在自定义 Provider 的 Span 迁移与音频后端回归，不在框架本身。
- 升级收益集中在 WASAPI 生命周期修复、Media Foundation/MP3 seek 精度、`WasapiPlayer` 新能力与 `IWaveLatency`，对本项目音频稳定性有实际价值。
- NAudio 3 **不会自动修复** 本项目 `DefaultMusicPlayer_new` 的“设备位置回退被误判为 32 位回绕”问题；该补偿逻辑仍需保留。
- 现有自动化测试不覆盖音频输出路径；升级必须补充 WASAPI / WaveOut / ASIO 实机验证，不能只依赖 `dotnet build` 与 MCP e2e。

## 2. NAudio 3 的主要变动

### 2.1 包与平台

| 项目 | NAudio 3 行为 |
| --- | --- |
| 最低目标框架 | `net9.0`；不再支持 .NET Framework / .NET Standard 2.0 |
| 程序集拆分 | `NAudio.Core` / `NAudio.Midi` / `NAudio.WinMM` / `NAudio.Wasapi` / `NAudio.Asio` / `NAudio.Dmo` / `NAudio.WinForms` 等 |
| `NAudio` 元包（3.3） | 包含 Core / Midi / WinMM / Wasapi；**不包含 Asio / Dmo / WinForms** |
| Native AOT | Core、WinMM、WASAPI 等标记 AOT 兼容；WPF 构建不使用 AOT，收益有限 |

本项目使用了 `AsioOut`，升级时必须显式增加 `NAudio.Asio` 包。

### 2.2 破坏性 API 变化

1. **Provider `Read` 改为 Span**
   - `IWaveProvider.Read(byte[] buffer, int offset, int count)` → `Read(Span<byte> buffer)`
   - `ISampleProvider.Read(float[] buffer, int offset, int count)` → `Read(Span<float> buffer)`
   - 调用方需要 `source.Read(buffer.AsSpan(offset, count))`。
2. **`WaveOutEvent` → `WaveOut`**
   - 旧名保留为 `[Obsolete]` 子类；本项目已使用 `WaveOut`，只涉及延迟参数。
3. **`DesiredLatency` 删除，改为 `BufferMilliseconds`**
   - 语义变化：旧值是所有缓冲的总延迟，新值是单个缓冲的延迟。
   - 默认 `NumberOfBuffers = 2` 时，`DesiredLatency = 100` 应对应 `BufferMilliseconds = 50`；否则默认会变成约 200ms 总延迟。
4. **WASAPI**
   - `WasapiOut` / `WasapiCapture` / `WasapiLoopbackCapture` 标记 `[Obsolete]`，推荐 `WasapiPlayerBuilder` / `WasapiRecorderBuilder`；旧类型仍可用。
   - `WasapiPlayer` 实现 `IWavePosition`，新增低延迟共享模式、MMCSS、raw mode、默认设备自动路由、`IAsyncDisposable`。
   - `WasapiOut` 独占模式的内部 DMO 重采样移除；共享模式仍自动转换。
   - `MMDevice.AudioClient` 过时，改用 `MMDevice.CreateAudioClient()`。
   - 原始 Core Audio COM 接口转 internal；`AudioClient` 构造函数转 internal。
   - `WasapiPlayer.Volume` 是会话音量（Windows 音量混合器中的应用滑块），不是设备端点音量。
5. **Media Foundation**
   - 大量原始 COM 接口与 `Mf*` 包装转 internal；使用高层 `MediaFoundationReader` / `MediaFoundationResampler` / `MediaType`。
   - `MediaType` 实现 `IDisposable`；`MediaFoundationTransform` / `MediaFoundationEncoder` 移除终结器，必须显式 `Dispose()`。
   - 异常改为 `MediaFoundationException`（仍继承 `COMException`）。
6. **WaveOut / WaveIn**
   - `WaveOut` / `WaveIn` 默认事件驱动；窗口回调版本移入 `NAudio.WinForms` 的 `WaveOutWindow` / `WaveInWindow`。
   - `WaveCallbackInfo` / `WaveCallbackStrategy` 移除。
   - `WaveIn` 默认录音格式从 8kHz mono 改为 44.1kHz stereo。
7. **WaveFormat / AOT**
   - `WaveFormat` 及其子类不再带 `[StructLayout]`；使用 `Marshal.SizeOf` / `PtrToStructure` 的代码会失败，改用 `ToWaveFormatExBytes` / `MarshalToPtr` / `MarshalFromPtr`。
   - `AudioClient.IsFormatSupported` 的 `out` 参数由 `WaveFormatExtensible` 改为 `WaveFormat`。
8. **Nullable**
   - 3.3 起各包启用 nullable 注解；源码级变化，会产生新警告，部分原先 `NullReferenceException` 改为 `ObjectDisposedException` / `InvalidOperationException`。

### 2.3 相关稳定性修复

| 版本 | 修复 |
| --- | --- |
| 3.0 | `AudioClient.Dispose` 幂等且并发安全；设备通知回调对象被回收导致的访问冲突；WASAPI 捕获/录音生命周期；Media Foundation/DMO COM 泄漏 |
| 3.1 | MP3 Xing/Info 头 seek 从头播放与偏移错误；`WdlResamplingSampleProvider` 欠载丢样本；WaveFormat AOT 编组 |
| 3.2 | `WasapiPlayer` / `WasapiOut` 在源 `Read` 抛异常后卡在 Playing；`DirectSoundOut` 全部播放失败；旧 API 标记过时 |
| 3.3 | 元包在 `netX.0-windows` 下恢复完整 Windows 栈；`MediaFoundationReader` seek/Position 按帧对齐；`WasapiRecorder`/`WasapiCapture` 重启修复；全面 nullable |

> 注：3.3 之后的未发布提交还包含 `WasapiOut` 缓冲区越界/释放后使用修复、`WdlResamplingSampleProvider` 默认 sinc 重采样等；这些不属于 3.3.0，不应计入本次升级收益。

## 3. 本项目使用点映射

| 文件 / 位置 | 当前用法 | NAudio 3 影响 |
| --- | --- | --- |
| `NAudioManager.cs:115` | `new AsioOut() { AutoStop = false }` | 需要新增 `NAudio.Asio` 包；`AsioOut` 保留，但可评估 `AsioDevice` |
| `NAudioManager.cs:116` | `new WasapiOut(AudioClientShareMode.Shared, 0)` | 可继续使用（仅 Obsolete 警告），或迁移到 `WasapiPlayerBuilder` |
| `NAudioManager.cs:117` | `new WaveOut() { DesiredLatency = 100 }` | 改为 `BufferMilliseconds = 50`（保持默认 2 缓冲下总延迟 100ms） |
| `NAudioManager.cs:199` | `device.AudioClient` | 改用 `device.CreateAudioClient()` |
| `NAudioManager.cs:357` | `new Mp3FileReader(filePath)` | 元包已包含；受益于 Xing seek 修复 |
| `BufferWaveStream` | 同时实现 `WaveStream` / `IWaveProvider` / `ISampleProvider`，使用 offset/count | 改为 `Read(Span<byte>)` / `Read(Span<float>)` |
| `LoopableProvider` | `ISampleProvider.Read(float[], int, int)` | 同上 |
| `FinishedListenerProvider` | `ISampleProvider.Read(float[], int, int)` | 同上 |
| `NonStopSampleProvider` | `IWaveProvider` / sample provider | 同上 |
| `CachedSoundSampleProvider` / `CachedSoundWrappedSampleProvider` | `ISampleProvider.Read` | 同上 |
| `VarispeedSampleProvider` | `ISampleProvider.Read` | 同上 |
| `AudioCompatibilizer` / `MethodExtensions` | 调用 `Read(buffer, offset, count)` | 改为 `Read(buffer.AsSpan(offset, count))` |
| `DefaultMusicPlayer_new` | 仅接受实现 `IWavePosition` 的后端；注释提到 `WasapiOut` / `WaveOut` / `WaveOutEvent` | `WasapiPlayer` 与 `WasapiOut` 都实现 `IWavePosition`；回绕补偿逻辑仍需保留 |
| `IndividualSoflanAreaDrawingTarget` / `DurationSoflanDrawingTarget` / `FumenVisualEditorViewModel.Drawing` | `using NAudio.Gui;` | 实际未使用，直接删除；无需 `NAudio.WinForms` |
| `AudioFileReader` | `AudioCompatibilizer` 的回退路径 | 元包已包含；3.x 对 `WAVE_FORMAT_EXTENSIBLE` 支持更好 |
| `WaveFormatExtraData` / `Marshal.SizeOf(WaveFormat)` / `IsFormatSupported` | 未使用 | 无迁移工作 |

## 4. 升级收益

1. **WASAPI 稳定性**：状态卡死、`AudioClient` 释放竞态、设备通知崩溃等修复，直接覆盖本项目 WASAPI 播放/切换设备路径。
2. **`WasapiPlayer` 新能力**：低延迟共享模式、MMCSS、raw mode（绕过系统音效处理）、默认设备自动路由、异步释放；可替代当前手工 `WasapiOut` 管理。
3. **时间/位置精度**：Media Foundation 按帧对齐 seek/Position、MP3 Xing seek 修复，减少 `CurrentTime` / `Seek` 偏差，有利于本项目设备位置播放器与音频时间轴。
4. **`IWaveLatency`**：提供 `AverageLatency` / `CurrentLatency`，可作为现有 `baseOffset + elapsed` 推算的补充，用于 A/V 同步与漂移检测。
5. **维护性**：2.3 已停止功能演进；3.x 仍在修复音频后端问题。

## 5. 升级步骤建议

1. 更新 `OngekiFumenEditor.csproj`：
   - `NAudio` → `3.3.0`
   - 新增 `NAudio.Asio` → `3.3.0`
2. 迁移所有自定义 Provider 与调用点到 `Span<T>` `Read`。
3. `WaveOut` 延迟：`DesiredLatency = 100` → `BufferMilliseconds = 50`，并在实机确认总延迟与卡顿表现。
4. `device.AudioClient` → `device.CreateAudioClient()`。
5. 删除未使用的 `using NAudio.Gui;`。
6. 可选：将 `WasapiOut` 迁移到 `WasapiPlayerBuilder().WithLatency(...).Build()`，并利用 `IWavePosition` 保持现有时间源。
7. 可选：引入 `IWaveLatency` 辅助同步/漂移诊断。
8. 完成音频回归后再考虑 `AsioOut` → `AsioDevice`。

## 6. 测试计划

自动化部分：

1. `dotnet build OngekiFumenEditor.sln -c Debug` 0 error。
2. `tests/bullet-placement`、`tests/grid-placement` 保持通过（不覆盖音频，但防止非音频回归）。
3. 自构建实例 + `tests/mcp-e2e/run.mjs`（MCP、脚本编译、文档生命周期）。

音频实机部分（必须）：

| 后端 | 场景 |
| --- | --- |
| WASAPI Shared | 加载 WAV/MP3/ACB；播放、暂停、恢复、Seek、播放结束；设备采样率探测；循环音效/Beam |
| WaveOut | 同上，重点确认 `BufferMilliseconds` 后的延迟与卡顿 |
| ASIO | 同上（需要硬件）；确认 `NAudio.Asio` 包解析与驱动初始化 |

时间与状态断言：

1. `CurrentTime` 与真实播放时间同步，不出现跳到音频末尾。
2. 暂停/恢复不累积误差；Seek 到末尾附近、播放结束行为正常。
3. `DefaultMusicPlayer_new` 的 WASAPI 位置回退补偿仍生效。
4. 切换默认音频设备/WASAPI 状态变化不崩溃、不卡死。

## 7. 风险与暂缓理由

1. **Span 迁移面较广**：约 8 个自定义 Provider 和多个调用点，容易出现 off-by-offset 错误；需要逐段音频验证。
2. **延迟语义变化**：`DesiredLatency` → `BufferMilliseconds` 若按同数值替换，实际总延迟翻倍。
3. **ASIO 包拆分**：漏加 `NAudio.Asio` 会直接编译失败。
4. **`WasapiPlayer` 音量语义**：`Volume` 是会话音量，不是设备端点音量；若迁移需确认现有音量 UI 的预期。
5. **独占模式重采样移除**：项目当前使用共享模式，直接影响较小；若未来使用独占模式需上游重采样。
6. **Nullable 警告**：3.3 会引入一批新警告，需要与现有代码风格协调。
7. **现有自动化测试不覆盖音频输出**：没有真实设备回归就不能认为升级完成。

## 8. 最终决策

- 本轮依赖审计保留 `NAudio 2.3.0`，不随其他低风险包一起升级。
- 将 NAudio 3.3 作为独立 PR 处理：先完成 Span 迁移与编译，再按第 6 节做三后端实机验证。
- 若升级目标仅是“减少 WASAPI 异常”，可优先评估 `WasapiPlayer` 的稳定性收益；若目标是“修复播放跳末尾”，应继续沿用现有项目侧补偿，不应期待 NAudio 升级解决该问题。

## 9. 参考

- NAudio release notes: https://github.com/naudio/NAudio/blob/main/RELEASE_NOTES.md
- Migrating from NAudio 2 to NAudio 3: https://github.com/naudio/NAudio/blob/main/Docs/MigratingFromNAudio2.md
- WasapiPlayer: https://github.com/naudio/NAudio/blob/main/Docs/WasapiPlayer.md
