# 15B 测试实施计划

更新时间：2026-08-02 01:26 +08:00

## 总体结构

优先建立一个 `net10.0` 的 Avalonia Headless xUnit 项目：

- `tests/Directory.Packages.props`
- `tests/OngekiFumenEditor.Avalonia.Tests/OngekiFumenEditor.Avalonia.Tests.csproj`
- 项目引用 `src/OngekiFumenEditor.Avalonia/OngekiFumenEditor.Avalonia.csproj`
- 当前轮次不修改 `OngekiFumenEditor.Avalonia.sln`；测试项目先按独立项目路径验证
- 测试程序集禁用并行，并通过 `AvaloniaTestApplication` 启动测试应用
- 不默认配置 coverlet，不访问网络，不在普通单测中打开真实音频设备

平台项目不能由同一个普通 `net10.0` 测试项目直接执行：Desktop 是 Windows TFM，Browser 是 browser TFM。初版把平台工厂的公共接口、编译/DI 注册、上游播放器状态机和 AOT 发布分别作为证据；只有在增加可注入构造器或浏览器测试宿主后，才补真正的平台工厂运行时测试。

## 阶段 1：基础设施与 AXAML

计划文件：

- `TestAppBuilder.cs`
- `TestApplication.cs`
- `Properties/AssemblyInfo.cs`
- `UI/AxamlSmokeTests.cs`

计划测试：

- `ApplicationResources_Loads_AllRequiredThemeAndConverterResources`
- `AllParameterlessViews_ConstructAndApplyTemplatesWithoutBindingErrors`
- `ReorderDataGrids_DisableColumnSortingAndAttachTypedBehavior`

断言必须包含资源键查找、51 个视图/控件的具体类型名，以及模板/布局完成后没有绑定错误；不能只断言构造结果非 null。

成功条件：Headless 初始化一次、测试串行、51 个视图类型全部被 MemberData 枚举，遗漏类型时清单测试失败。

## 阶段 2：键位和顶层输入路由

计划文件：

- `Input/KeyBindingDefinitionTests.cs`
- `Input/MultiKeyGestureTests.cs`
- `Input/EditorKeyBindingRouterTests.cs`

计划测试：

- `FormatAndParse_SupportedModifiers_RoundTripsExactKeyAndModifier`
- `TryParseExpression_InvalidModifierOrKey_ReturnsFalse`
- `Matches_MultiStepSequence_HandlesIntermediateAndFinalEvents`
- `Matches_WrongKeyOrExpiredSequence_ResetsState`
- `DefinitionMap_AllThirtyFiveEditorDefinitionsHaveOneTypedAction`
- `KeyDown_FocusedTextEntryOrDataGrid_YieldsWithoutExecutingEditorAction`
- `KeyDown_ConflictingDefinitions_LeavesEventUnhandledAndExecutesNothing`
- `AttachCalledTwice_AndDetach_DoesNotDuplicateOrRetainHandler`

先覆盖不触发编辑器副作用的路由分支。若要验证某个真实编辑动作，需要一个显式的 action-dispatch seam；不要通过等待复杂 ViewModel 副作用来制造脆弱测试。

## 阶段 3：DataGrid 行顺序

计划文件：

- `UI/DataGridRowReorderOperationsTests.cs`

计划测试：

- `Reorder_MultipleItemsBeforeTarget_PreservesOriginalMovingOrder`
- `Reorder_MovingItemsAcrossTarget_AdjustsInsertionBoundaryAfterRemoval`
- `Reorder_InsideMissingTargetOrMissingMovingItems_ReturnsUnchangedCopy`
纯算法测试使用至少 5 个项目，避免单元素退化样例；专业化 ViewModel 重排与 undo 测试不在当前授权范围内，留作后续决定。

## 阶段 4：Skia 输出

计划文件：`Graphics/SkiaRenderSmokeTests.cs`

计划测试：

- `SkiaRenderControl_CleanFrame_ProducesExpectedNonTransparentPixels`

固定 96x64 控件，渲染不透明洋红色，捕获帧后断言：尺寸正确、至少一个目标色像素、非透明像素数大于 0。测试结束必须调用 `StopRendering()` 并关闭 Window。

阻塞规则：若 Headless 不提供 `ISkiaSharpApiLeaseFeature`，在状态文件记录准确异常，转成 Desktop 离屏集成测试；不得用独立 `SKSurface` 伪装成产品路径已验证。

## 阶段 5：谱面 fixture 与外部语料

计划文件：

- `Fixtures/minimal.nyageki`
- `Corpus/CorpusLocator.cs`
- `Corpus/FumenSemanticFingerprint.cs`
- `Corpus/FumenParserRoundTripTests.cs`
- `Corpus/ExternalFumenCorpusTests.cs`

计划测试：

- `BuiltInFixture_ParseFormatReparse_PreservesSupportedSemanticFingerprint`
- `CorpusLocator_EnvironmentVariableOverridesLocalDefault`
- `CorpusLocator_MissingDirectory_ReturnsCorpusMissingWithoutAttemptingParse`
- `ExternalCorpus_RamenProject_ResolvesExistingChartAudioAndImageReferences`
- `ExternalCorpus_RamenChart_ReportsOnlyDecisionExcludedSvgPrefabCommand`
- `ExternalCorpus_RamenChart_ParseFormatReparse_PreservesSupportedSemanticFingerprint`
- `ExternalCorpus_ParseException_ReportsCorpusParseFailedWithRelativePath`

外部测试以 `Category=ExternalCorpus` 标记。命令扫描允许清单仅包含 `SvgPrefab: 1`；任何额外未知命令必须失败。`.nyagekiScript`、`.wav`、`.png` 不传入 `IFumenParserManager`。

## 阶段 6：音频平台契约

计划文件：`Audio/NAudioWavePlayerFactoryContractTests.cs`

可立即实施的测试与证据：

- `FactoryContract_CreateDefaultWavePlayer_HasExactTaskOfIWavePlayerSignature`
- `dotnet build` Core/Desktop/Browser Debug 与 Release
- 运行 `NAudio.BrowserAudioWorklet.Tests`，保留通过数
- Native AOT publish，确认 AOT 图不包含 ASIO 包而包含 WASAPI

直接运行时测试的当前阻塞：

- Desktop 工厂真实创建设备，缺少可注入的 WASAPI/ASIO builder。
- Browser 工厂只能在 `net10.0-browser` 环境构造，普通 Headless 测试不可执行。

推荐后续解决：Desktop 工厂构造函数注入两个内部 creator 委托，并将“配置值到后端”的选择做成纯逻辑；Browser 增加 WebAssembly 测试宿主或把 profile 选择提取为可在 `net10.0` 测试的纯函数。完成这些 seam 后补：

- `DesktopFactory_WasapiAndLegacyWaveOut_SelectLowLatencyWasapi`
- `DesktopFactory_Asio_SelectsAsioOutsideNativeAot`
- `BrowserFactory_Default_SelectsInteractiveAudioWorkletProfile`

## 阶段 7：AOT 与最终验证

1. 等 Browser AOT 不再占用共享 `obj` 后，运行测试项目 Debug narrow build/test。
2. 运行内置 fixture 组。
3. 设置 `ONGEKI_FUMEN_TEST_CORPUS_ROOT` 并运行 ExternalCorpus 组。
4. 运行 BrowserAudioWorklet 上游 69 项测试。
5. 构建 Core/Desktop/Browser 的 Debug 和 Release。
6. 执行 `win-x64-aot` publish。
7. 对 publish 可执行文件做有限时启动烟测并清理本次进程。
8. 当前轮次不改 solution；后续得到授权加入 solution 后，再执行 solution 级发现与测试。
9. 执行 test-gap-analysis 和 assertion-quality；修复后把精确测试名、命令和通过摘要写入 `status.md`。

## 需求映射

| 要求 | 计划证据或阻塞 |
| --- | --- |
| AXAML/resource/view construction | `ApplicationResources_Loads_AllRequiredThemeAndConverterResources`; `AllParameterlessViews_ConstructAndApplyTemplatesWithoutBindingErrors` |
| key commands/input routes | `DefinitionMap_AllThirtyFiveEditorDefinitionsHaveOneTypedAction`; `KeyDown_FocusedTextEntryOrDataGrid_YieldsWithoutExecutingEditorAction` 等 |
| DataGrid sorting/reordering | `ReorderDataGrids_DisableColumnSortingAndAttachTypedBehavior`; 5 项顺序/undo 测试 |
| audio factory platform contract | 接口签名测试、三平台 build、上游播放器测试；工厂运行时选择受平台/硬件 seam 阻塞 |
| Skia nonblank | `SkiaRenderControl_CleanFrame_ProducesExpectedNonTransparentPixels` |
| Desktop Native AOT smoke | `dotnet publish ... -p:PublishProfile=win-x64-aot` 和有限时启动记录 |
| corpus discovery/parse/format | 7 项 Corpus 测试，含内置 fixture、外部 ramen、失败分类和 SvgPrefab 排除报告 |

## 完成条件

- 每项未阻塞要求都有具体测试名和强断言。
- narrow 与 solution 级测试命令退出码均为 0。
- solution 级发现能列出全部新增测试。
- Native AOT publish 退出码为 0，启动烟测无立即崩溃。
- `.testagent/status.md` 记录 test-gap-analysis、assertion-quality 结果及修复。
- 最终交付按用户原话给出 `Requirement | Evidence` 表，不用笼统模块列表代替。

## 执行结果（2026-08-02 02:22 +08:00）

计划已全部执行。实现阶段根据动态测试补充了 `LocalizeConverter` 行为测试和键位冲突日志分支断言；外部语料 round-trip 采用“语义指纹 + 完整序列化行多集合”，允许同一 TGrid 的无语义顺序变化但不允许字段丢失、重复或值变化。最终 solution 展开 101 例并全部通过；NAudio 3 覆盖配置的上游测试 91/91 通过；Core/Desktop/Browser 常规矩阵、Desktop Native AOT 启动和 Browser AOT 资源发布均完成。真实 Desktop 音频设备/ASIO 与真实 Browser AudioContext 保留为明确平台集成边界，不属于未完成单元测试。

## 第七轮增量实施计划（2026-08-02 03:31 +08:00）

### 阶段 A：Skia 波形

- 将旧波形采样算法与渲染宿主分离，接入现有 Avalonia Skia lease 生命周期。
- 测试峰值到路径/像素映射、空数据、缩放参数、非空帧和停止释放；不得以独立测试用 `SKSurface` 冒充产品控件路径。

### 阶段 B：跨平台 WAV 偏移

- 新增纯流式服务并由音频调整 ViewModel 注入；以块解析/写出保持未知但合法的 WAV chunk，按 `BlockAlign` 处理数据。
- 测试正、负、零、过量负偏移，PCM 16/24/32 与 IEEE float，奇数 chunk padding、无效头、失败时目标不变。

### 阶段 C：20B 双发行与能力 UI

- 新增共享强类型音频平台能力，Desktop AOT/JIT 与 Browser 分别注册；设置页和播放器使用编译绑定控制选项及变速可见性。
- 增加 `win-x64-jit` publish profile；验证 AOT profile 只含 WASAPI、JIT profile 含 ASIO，并对不可用旧配置做可观察回退。
- 单元测试能力矩阵与归一化；真实设备初始化保留为人工平台验收。

### 阶段 D：Svg* 完整迁移

- 从原项目/受版本控制历史恢复注释与领域语义，迁移 Nyageki/OGKR 解析写出、属性编辑、创建入口和 Skia 绘制，不恢复旧 OpenGL 文件。
- 测试字符串/文件 prefab 往返、路径/颜色/偏移/缩放等关键字段、Skia 非空绘制、视图构造，以及真实 ramen 中 `SvgPrefab` 的保真。

### 阶段 E：集成验证

1. 构建并运行产品测试项目的非 ExternalCorpus 集合。
2. 以 `C:\Users\mikir\Desktop\音寄谱\拉面` 运行 ExternalCorpus，要求 0 skip 且 SvgPrefab 不在排除列表。
3. 运行 solution 级发现和全量测试，确认新增测试可见。
4. 非增量构建完整 solution；分别发布 Windows AOT/WASAPI、JIT/ASIO 和 Browser AOT。
5. 运行 test-gap-analysis 与 assertion-quality，修复存活分支或弱断言，并把精确结果写入 `status.md`。

### 第七轮完成条件

- 四项用户原话均能映射到至少一个行为测试和一个产品构建/发布证据。
- 活动 AXAML 保持编译绑定，不新增 `ReflectionBinding`、字符串成员路径或反射事件接线。
- Native AOT 新路径不新增无法解释的裁剪/AOT 警告，不用警告抑制替代修复。
- 既有 101 项不回归；真实语料中原先被排除的 `SvgPrefab: 1` 改为成功解析与往返保留。
