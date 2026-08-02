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

## CommandLine 第一阶段实施计划（2026-08-02）

### A. 命令框架与宿主

- 在 CPM 加入 `System.CommandLine 2.0.0`，CLI 引用核心项目、Injectio 和必要的 DI/日志包。
- 实现 `ICommandExecutor`、`DefaultCommandExecutor`、`ICommandLineDefinition`、非泛型标记接口 `ICommandLineHandler` 和 `ICommandLineHandler<TOptions>`。
- `DefaultCommandExecutor` 只聚合 `IEnumerable<ICommandLineDefinition>`，检查重复命令名，并提供根帮助和递归 `--verbose/-v`。
- `Program.Main` 只创建 headless 服务容器、解析参数并返回执行码。

### B. convert 业务链

- `ConvertCommandLineDefinition` 显式声明 `--inputFile`、`--outputFile`、`--standardize`，将 `ParseResult` 映射为 `FumenConvertOption`。
- Definition 构造函数注入 `ICommandLineHandler<FumenConvertOption>`；`ConvertCommandLineHandler` 只做绝对路径校验、调用转换服务、输出错误和映射旧版 `-3/-4` 退出码。
- 将转换包装器改成可注入服务；解析器管理器、转换器和标准化规则均由构造函数传入。
- 输出先写同目录临时文件再原子替换；取消或失败清理临时文件，避免半写目标。
- OGKR `CommandArgs` 从 parser 显式获得值转换器，保证 `.ogkr` 输入也不访问 Avalonia 全局 `IoC`。

### C. 自动化测试

- 命令执行器：根帮助包含 `convert`、重复命令名拒绝、未知命令不进入 Handler、`--verbose/-v` 均可解析。
- Definition：三个参数正确绑定；缺失必填参数不调用 Handler。
- Handler：相对路径返回 `-3`；服务失败返回 `-4` 且写 stderr；成功返回 0。
- 集成：用 `Fixtures/minimal.nyageki` 在纯 DI 宿主转换到 OGKR，重新解析并断言关键语义；覆盖不支持格式和取消/失败不留临时输出。
- 运行全部现有测试，确保 144 项基线不回归。

### D. EXE 验收

- 发布 JIT 与 `win-x64-aot`，分别运行根帮助、版本、`convert --help`、未知命令、成功转换和错误转换。
- 记录每组退出码、stdout、stderr 和输出文件状态；成功产物需重新解析而不是只检查存在。
- 本轮不修改 CI，占位校验与新行为不一致的问题仅记录在统一跟踪文档。

## CommandLine 迁移到 Desktop Broad-scope 测试计划（2026-08-02）

### 阶段 0：测试项目重组

计划建立 `tests/OngekiFumenEditor.Avalonia.Desktop.Tests/OngekiFumenEditor.Avalonia.Desktop.Tests.csproj`：

- 目标 `net10.0-windows10.0.19041.0`，引用 Desktop，不直接引用 CommandLine。
- 沿用 `tests/Directory.Packages.props` 中的 xUnit/VSTest 版本；需要生命周期测试时加入与产品一致的 `Avalonia.Headless.XUnit`。
- 添加 `[assembly: CollectionBehavior(DisableTestParallelization = true)]`，避免 Avalonia 和进程环境相互污染。
- Desktop 通过 `InternalsVisibleTo("OngekiFumenEditor.Avalonia.Desktop.Tests")` 暴露 internal Definition/Handler/测试 seam。
- 将现有 `tests/...Avalonia.Tests/CommandLine` 四个文件迁到新项目；`Fixtures/minimal.nyageki` 保留单一源文件，由 Desktop.Tests 以 link + `CopyToOutputDirectory` 使用。
- Core 测试项目删除 CommandLine `ProjectReference`，最终只引用 Core；solution 同时发现 Core.Tests 和 Desktop.Tests。

项目结构测试/证据：

- `CoreTestProject_ReferencesOnlyCoreProductProject`（若使用 MSBuild/XML 审核测试）或在最终项目 diff 中直接证明。
- `DesktopTestProject_TargetsWindowsAndReferencesDesktop`（同上）。
- `dotnet test ...Desktop.Tests.csproj --list-tests` 必须仍列出迁移前 18 个展开用例。

### 阶段 1：生命周期、宿主与命令框架

计划文件：

- `Lifecycle/CommandLineApplicationLifecycleTests.cs`
- `Lifecycle/DesktopCommandLineHostProcessTests.cs`
- `CommandLine/DefaultCommandExecutorTests.cs`（迁移并扩展）
- `CommandLine/CommandLineRegistrationTests.cs`

计划测试：

- `CommandLineMode_FrameworkInitialization_LoadsXamlLanguageThemeCoreDesktopDiAndLogging`
- `CommandLineMode_FrameworkInitialization_DoesNotResolveMainViewCreateWindowOrInitializeStatusBar`
- `CommandLineMode_PrepareExit_DoesNotRestoreOrSaveWindowState`
- `CommandLineMode_CoreApp_DoesNotAttachKeyBindingsOrShowSplash`
- `CommandLineMode_DesktopApp_DoesNotAttachXamlMcpOrProcessGuiStartupArgs`
- `DesktopCommandLineHost_CommandCompletion_ShutsDownExplicitlyWithExecutorExitCode`
- `RootHelp_ListsConvertSvgJacketUpdaterAndOmitsAcb`
- `CommandHelp_EachRegisteredCommand_ListsItsOptionsWithoutInvokingHandler`
- `Constructor_DuplicateCommandNamesIgnoringCase_Throws`
- `ExecuteAsync_VerbosityAliasAfterSubcommand_InvokesCommand`（`--verbose`/`-v` 两行）
- `UnknownCommand_DoesNotInvokeAnyRegisteredCommand`
- `AddOngekiFumenEditorCommandLine_RegistersFourDefinitionHandlerPairsAsSingletons`
- `AddOngekiFumenEditorCommandLine_ResolvesOnlyDefaultFumenParserManager`

生命周期 fake 必须把 `IMainView`、`IStatusBar`、`IPlatformMainWindow`、窗口设置读写、快捷键路由、Splash 和 GUI 参数处理做成一旦解析/调用就记录或失败的依赖。正向断言同时检查 `Application.Current`、XAML 资源、`ILanguageManager`/`IThemeManager` 初始化、Core parser、Desktop command executor 和 `ILoggerFactory` 可用，避免只证明“没有窗口”。

`DesktopCommandLineHost` 的进程级测试使用一个返回非零哨兵码的测试命令或真实相对路径错误，断言 Classic Desktop lifetime 在超时内退出且 `Process.ExitCode` 与命令返回完全一致。`ShutdownMode.OnExplicitShutdown` 应有直接状态断言；子进程无挂起只作为第二证据。

### 阶段 2：convert 基线迁移

计划文件保持原四个类名：

- `CommandLine/ConvertCommandLineDefinitionTests.cs`
- `CommandLine/ConvertCommandLineHandlerTests.cs`
- `CommandLine/ConvertCommandIntegrationTests.cs`

保留全部现有测试名和 18 项展开基线。注册测试按四命令新现实改写，不能继续断言只有一个 Definition。额外检查：

- 相对 input 和相对 output 分别返回 `-3`，服务调用次数为 0。
- 失败结果和异常均返回 `-4`，错误文字包含业务原因。
- `ConvertFixture_ToOgkr_ProducesReparseableChartWithPreservedContent` 继续覆盖 `standardize=false/true`，比较语义指纹和临时文件清理。
- 取消仍保留既有目标，不能因代码搬家退化。

### 阶段 3：svg

计划文件：

- `CommandLine/SvgCommandLineDefinitionTests.cs`
- `CommandLine/SvgCommandLineHandlerTests.cs`
- `CommandLine/SvgCommandIntegrationTests.cs`
- `CommandLine/PngStructureAssertions.cs`

计划测试：

- `Invoke_AllOptions_BindsSvgOptionsAndCallsInjectedHandler`
- `Invoke_DefaultOptions_UsesLegacySvgDefaults`
- `Invoke_MissingEachRequiredSvgPath_DoesNotCallHandler`（三个数据行）
- `HandleAsync_AnyRelativeSvgPath_ReturnsMinusOneWithoutCallingDependencies`（input/output/audio 三个数据行）
- `HandleAsync_ExistingAudio_UsesExactAudioDuration`
- `HandleAsync_MissingAudio_UsesChartTailPlusFiveGrids`
- `HandleAsync_GeneratorOrRasterizerFailure_ReturnsMinusTwoAndWritesError`
- `SvgFixture_NoAudio_ProducesWellFormedSvgWithDeclaredPositiveDimensions`
- `SvgFixture_ExistingAudio_UsesAudioDurationRatherThanChartTail`
- `SvgFixture_Png_EndsAtIendAndImageSharpDecodesDeclaredDimensions`

默认值测试必须一次组合断言 `XGridDisplayMaxUnit=40`、`ViewWidth=800`、`VerticalScale=1`、`SoflanMode.Soflan`、`RenderAsPng=false`。无音频测试构造至少一个明确位于非零 TGrid 的 displayable timeline object，捕获传给 Core generator 的公开 `Duration`，并用 `TGridCalculator` 计算精确期望；不能只断言 duration 大于零。

PNG 测试同时断言 8 字节签名、IHDR 宽高、最后一个 chunk 为 IEND、IEND 后无字节，以及 `Image.Load` 后尺寸等于 SVG 根元素声明尺寸。这样可杀死旧版把 SVG bytes 追加到 PNG 的缺陷。

### 阶段 4：jacket

计划文件：

- `CommandLine/JacketCommandLineDefinitionTests.cs`
- `CommandLine/JacketCommandLineHandlerTests.cs`
- `CommandLine/JacketGenerateServiceIntegrationTests.cs`
- `CommandLine/AssetBytesAssertions.cs`

计划测试：

- `Invoke_DefaultOptions_Uses520And220JacketDimensions`
- `Invoke_DistinctSmallWidthAndHeight_BindsToCorrectProperties`
- `Invoke_MissingEachRequiredJacketOption_DoesNotCallHandler`
- `HandleAsync_AnyRelativeJacketPath_ReturnsMinusFiveWithoutCallingService`
- `HandleAsync_ServiceFailureOrException_ReturnsMinusSixAndWritesReason`
- `HandleAsync_ServiceSuccess_ReturnsZeroWithoutError`
- `Generate_RealTemplate_ProducesNormalAndSmallBundlesWithRequestedTextureDimensions`
- `Generate_UpdateAssetBytes_PreservesExistingRecordAndAppendsBothJacketNames`
- `DesktopOutput_ContainsJacketTemplateAndAllNativeDependencies`

反向绑定回归测试必须给 `--outputWidthSmall` 和 `--outputHeightSmall` 不同值，例如 321 和 123，并精确断言 `WidthSmall=321`、`HeightSmall=123`。真实集成使用临时 PNG、music id 666 和真实 `ui_jacket_0666`，解析两份 bundle 内纹理而不是只看文件大小；`assets.bytes` 先写入一个带 dependency 的既有记录，生成后断言 count、id/name/dependencies 原样保留并新增 `ui_jacket_0666`、`ui_jacket_0666_s`。

资源输出测试精确检查 `ui_jacket_0666`、`TexturePlugin.dll`、`TexToolWrap.dll`、`PVRTexLib.dll`、`ispc_texcomp.dll`，并在 JIT/AOT publish 目录各重复一次。若某原生库无法加载，测试失败并作为 AOT 阻塞报告，不能 Skip。

### 阶段 5：updater

计划文件：

- `CommandLine/UpdaterCommandLineDefinitionTests.cs`
- `CommandLine/UpdaterCommandLineHandlerTests.cs`
- `CommandLine/ProgramUpdateServiceTests.cs`
- `CommandLine/UpdaterExecutableSmokeTests.cs`

计划测试：

- `Invoke_AllRequiredUpdaterOptions_BindsAndCallsInjectedHandler`
- `Invoke_MissingEachRequiredUpdaterOption_DoesNotCallHandler`
- `HandleAsync_PropagatesProgramUpdateResultAndWritesOnlyFailures`
- `Update_Success_RecursivelyCopiesIncludedFilesExcludesThreeExtensionsAndRestartsDesktop`
- `Update_KillFails_ReturnsMinusOneBeforeAnyFileMutation`
- `Update_BackupFails_ReturnsMinusTwoAndRestoresEarlierBackups`
- `Update_CopyFails_ReturnsMinusThreeAndPreservesLegacyPartialRollbackState`
- `Update_BackupCleanupFails_StillReturnsSuccessAndRestartsDesktop`
- `Update_Success_KillsOnlyOtherDesktopProcessesAndUsesLegacyNotifyArguments`
- `UpdaterExecutable_TemporaryFoldersAndHarmlessDesktopStub_CompletesWithoutTouchingWorkspace`

成功 fixture 至少包含根文件、嵌套文件、三个被排除扩展、目标中已有文件和一个新文件。断言进程查询名为 `OngekiFumenEditor.Avalonia.Desktop`，当前 PID 不被 kill，其他实例被 kill；备份名匹配 `.bak_*`；成功后备份删除；启动路径为 `OngekiFumenEditor.Avalonia.Desktop.exe`，参数精确为 `--wait --notifySucess --sourceVersion <version>`。

复制失败测试刻意让前一目标已完成复制、后一目标抛错。按旧实现的无覆盖 `File.Move`，前一目标应保持新内容且旧内容留在 `.bak_*`，后一目标从 backup 恢复；这项看似不理想的状态是用户要求保留的旧版行为。测试名和注释必须明确是 legacy contract，不能顺手修成完整事务。

实际 EXE smoke 在专用临时根生成/放置一个无害的 `OngekiFumenEditor.Avalonia.Desktop.exe` stub（只记录参数后退出），source/target 均在该根下。等待 stub marker 和 updater 退出后清理，禁止使用真实安装目录、当前构建输出目录或正在运行的 Desktop。

### 阶段 6：JIT/AOT 与最终验证

验证矩阵：

| 产物 | 构建/发布 | 冒烟要求 |
| --- | --- | --- |
| CommandLine JIT | Windows TFM、self-contained win-x64 | 根帮助、四命令帮助、未知参数、convert 成功/-3/-4、svg SVG/PNG、jacket、临时 updater |
| CommandLine AOT | `PublishAot=true`/win-x64 | 与 JIT 同组，比较退出码和关键产物语义 |
| Desktop JIT | `win-x64-jit` profile | 有限时 GUI 启动无立即崩溃；供 CommandLine host 引用的程序集和 jacket 资源齐全 |
| Desktop AOT | `win-x64-aot` profile | 有限时启动；无新增无法解释的 AOT/trim 错误；jacket 原生依赖齐全 |

命令行模式的无窗口证据由两层组成：生命周期单测断言 `MainWindow`/窗口列表为空且窗口服务未解析；JIT/AOT 子进程在较长命令运行期间枚举属于该 PID 的顶层窗口，始终为零。每个进程设置硬超时并记录 stdout、stderr、`Process.ExitCode` 和产物状态。

计划验证命令（实施阶段执行，研究阶段未执行）：

```powershell
dotnet test .\tests\OngekiFumenEditor.Avalonia.Desktop.Tests\OngekiFumenEditor.Avalonia.Desktop.Tests.csproj -c Debug -v:minimal
dotnet test .\tests\OngekiFumenEditor.Avalonia.Tests\OngekiFumenEditor.Avalonia.Tests.csproj -c Debug -v:minimal
dotnet build .\OngekiFumenEditor.Avalonia.sln -c Release --no-incremental -v:minimal
dotnet test .\OngekiFumenEditor.Avalonia.sln -c Release --no-build -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.Desktop\OngekiFumenEditor.Avalonia.Desktop.csproj -p:PublishProfile=win-x64-jit -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.Desktop\OngekiFumenEditor.Avalonia.Desktop.csproj -p:PublishProfile=win-x64-aot -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.CommandLine\OngekiFumenEditor.Avalonia.CommandLine.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=false -v:minimal
dotnet publish .\src\OngekiFumenEditor.Avalonia.CommandLine\OngekiFumenEditor.Avalonia.CommandLine.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=true -v:minimal
git diff --check
```

完成测试实现后必须调用 `test-gap-analysis` 和 `assertion-quality`，把发现、修复、最终精确通过数和 publish/smoke 结果写入 `.testagent/status.md`。CI workflow 的旧占位输出校验必须记录为已知失败，不运行后宣称通过。

### 需求映射

| 清单 | 计划测试或证据 |
| --- | --- |
| `CLD-01` 薄启动器 | CommandLine csproj/Program 结构审核；CommandLine JIT/AOT 子进程均通过 `DesktopCommandLineHost` 得到业务退出码 |
| `CLD-02` 所有命令归 Desktop、领域服务留 Core | Desktop.Tests 编译引用关系；Core.Tests 只引用 Core；四类 Definition/Handler 的程序集断言 |
| `CLD-03` 初始化完整但无 GUI | `CommandLineMode_FrameworkInitialization_LoadsXamlLanguageThemeCoreDesktopDiAndLogging`; `CommandLineMode_FrameworkInitialization_DoesNotResolveMainViewCreateWindowOrInitializeStatusBar`; `CommandLineMode_PrepareExit_DoesNotRestoreOrSaveWindowState` |
| `CLD-04` 跳过 GUI 专属启动 | `CommandLineMode_CoreApp_DoesNotAttachKeyBindingsOrShowSplash`; `CommandLineMode_DesktopApp_DoesNotAttachXamlMcpOrProcessGuiStartupArgs` |
| `CLD-05` Classic Desktop/显式关闭/退出码 | `DesktopCommandLineHost_CommandCompletion_ShutsDownExplicitlyWithExecutorExitCode`；JIT/AOT process smoke |
| `CLD-06` 包、Injectio、可见性、TFM | 两个 csproj 的 MSBuild/结构审核；Desktop.Tests 能直接编译 internal 命令测试；四产物 build/publish |
| `CLD-07` parser/Duration | `AddOngekiFumenEditorCommandLine_ResolvesOnlyDefaultFumenParserManager`; `HandleAsync_ExistingAudio_UsesExactAudioDuration`; `HandleAsync_MissingAudio_UsesChartTailPlusFiveGrids` |
| `CLD-08` convert | 迁移的 18 项基线，尤其 `HandleAsync_RelativePath_ReturnsLegacyPathExitCodeWithoutCallingService`、失败/异常两项和 `ConvertFixture_ToOgkr_ProducesReparseableChartWithPreservedContent` |
| `CLD-09` SVG 必填路径与时长分支 | `Invoke_MissingEachRequiredSvgPath_DoesNotCallHandler`; `HandleAsync_AnyRelativeSvgPath_ReturnsMinusOneWithoutCallingDependencies`; 两项 Duration 测试 |
| `CLD-10` SVG 默认/PNG/-2 | `Invoke_DefaultOptions_UsesLegacySvgDefaults`; `HandleAsync_GeneratorOrRasterizerFailure_ReturnsMinusTwoAndWritesError`; `SvgFixture_Png_EndsAtIendAndImageSharpDecodesDeclaredDimensions` |
| `CLD-11` jacket 绑定/退出码 | `Invoke_DefaultOptions_Uses520And220JacketDimensions`; `Invoke_DistinctSmallWidthAndHeight_BindsToCorrectProperties`; Jacket Handler 三组测试 |
| `CLD-12` jacket 真模板与资源 | `Generate_RealTemplate_ProducesNormalAndSmallBundlesWithRequestedTextureDimensions`; `Generate_UpdateAssetBytes_PreservesExistingRecordAndAppendsBothJacketNames`; `DesktopOutput_ContainsJacketTemplateAndAllNativeDependencies` |
| `CLD-13` updater 行为/-1/-2/-3 | `Update_Success_RecursivelyCopiesIncludedFilesExcludesThreeExtensionsAndRestartsDesktop`; 三项失败测试；`Update_CopyFails_ReturnsMinusThreeAndPreservesLegacyPartialRollbackState` |
| `CLD-14` updater 重启参数与安全 EXE smoke | `Update_Success_KillsOnlyOtherDesktopProcessesAndUsesLegacyNotifyArguments`; `UpdaterExecutable_TemporaryFoldersAndHarmlessDesktopStub_CompletesWithoutTouchingWorkspace` |
| `CLD-15` 不注册 acb | `RootHelp_ListsConvertSvgJacketUpdaterAndOmitsAcb`; 精确四 Definition DI 断言；迁移跟踪文档审核 |
| `CLD-16` Desktop.Tests/18 项/Core-only | `--list-tests` 展开数审核、两个测试 csproj 引用审核、solution 发现 |
| `CLD-17` 框架覆盖 | `DefaultCommandExecutorTests`、四个 Definition 测试类、`CommandLineRegistrationTests` 和全部 Handler 退出码测试 |
| `CLD-18` JIT/AOT/无窗口/退出码 | 阶段 6 四产物矩阵、生命周期无窗口单测、CommandLine 两种产物的有符号退出码对照 |
| `CLD-19` 最终验证和 CI 已知失败 | solution build/test、`git diff --check`、`.testagent/status.md` 和 `docs/command-line-migration-tracking.md`；不得列 CI 占位检查为成功证据 |
