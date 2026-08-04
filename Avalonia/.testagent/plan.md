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

## Desktop acb 命令 Broad-scope 测试计划（2026-08-03）

### 阶段 A：注册与 Definition

计划文件：

- `CommandLine/Acb/AcbCommandRegistrationTests.cs`
- `CommandLine/Acb/AcbCommandLineDefinitionTests.cs`

计划测试：

- `DesktopRegistration_RootAndCommandHelpDiscoverAcbWithExpectedOptions`
- `DesktopRegistration_ResolvesAcbDefinitionHandlerAndServiceAsSingletons`
- `Invoke_DefaultOptions_UsesLegacyPreviewRange`
- `Invoke_AllOptions_BindsAcbOptionsAndReturnsHandlerExitCode`
- `Invoke_MissingEachRequiredAcbOption_DoesNotCallHandler`

根帮助测试断言 `acb` 位于实际 `DefaultCommandExecutor.RootCommand.Subcommands` 且 `--help` 输出含命令名；
`acb --help` 必须同时列出 `--musicId`、`--inputFile`、`--outputFolder`、`--previewBegin`、
`--previewEnd`。DI 测试直接解析闭合的 `ICommandLineHandler<AcbGenerateOption>` 和
`IAcbGenerateService`，断言具体类型及 singleton 身份。

默认值测试在同一次调用中断言 `PreviewBeginTime=60000` 与 `PreviewEndTime=80000`。必填参数测试用三个
数据行分别移除 musicId/input/output，并断言解析失败、错误文本包含缺失选项、Handler 未调用。

### 阶段 B：Handler

计划文件：`CommandLine/Acb/AcbCommandLineHandlerTests.cs`

计划测试：

- `HandleAsync_AnyRelativeAcbPath_ReturnsMinusSevenWithoutCallingService`
- `HandleAsync_ServiceFailureOrException_ReturnsMinusEightAndWritesReason`
- `HandleAsync_AbsolutePaths_ForwardsSameOptionsAndCancellationTokenToInjectedService`

路径测试分别覆盖 input/output，相对路径时断言 `-7`、service 零调用和唯一错误输出。失败测试以数据行覆盖
service 返回失败与抛异常，两者都必须为 `-8` 且保留具体原因。成功映射测试使用可取消 token，断言
service 收到同一个 option 实例和完全相等的 token，返回 0 且 stderr 为空。

### 阶段 C：真实 48 kHz 生成与解析

计划文件：`CommandLine/Acb/AcbGenerateServiceIntegrationTests.cs`

计划测试：

- `Generate_48KhzPcmWave_CreatesAcbAwbAndMusicSourceThatReopen`

测试步骤：

1. 在 GUID 临时目录写入 RIFF/WAVE PCM fixture：48000 Hz、16-bit、stereo、至少 0.25 秒非零正弦样本。
2. 使用非对称 music id（例如 427）和明确 preview 值调用 `DefaultAcbGenerateService`。
3. 断言结果成功，并检查 `music0427.acb`、`music0427.awb`、`MusicSource.xml` 均非空。
4. 解析 XML，精确断言 `Name/id=0427`、`Name/str=0427`、`dataName=musicsource0427`、
   `acbFile/path=music0427.acb`、`awbFile/path=music0427.awb`。
5. 通过 `AcbFile.FromStream` 打开 ACB，断言 `FormatVersion>0`、cue 非空、外置 AWB 非空、文件名非空，
   并从首个 cue 打开非空 HCA data stream。
6. 用独立 FileStream 构造 `Afs2Archive` 打开 AWB，调用 `Initialize()`，断言版本、对齐和 file records 合法。
7. 测试结束释放所有流并递归删除临时目录。

### 阶段 D：官方源码项目引用

计划文件：`CommandLine/Acb/AcbProjectReferenceTests.cs`

计划测试：

- `DesktopProject_ReferencesAcbGeneratorProjectWithoutDllBindings`
- `AcbGeneratorSubmodule_UsesOfficialRepository`
- `AcbGenerateService_UsesManagedProjectWithoutNativeInterop`

从 `AppContext.BaseDirectory` 向上定位 solution，加载 Desktop csproj 和仓库 `.gitmodules`，断言：

- 只有一个指向 `Dependencies/AcbGeneratorFuck/src/AcbGeneratorFuck/AcbGeneratorFuck.csproj` 的无条件
  `ProjectReference`，对应项目文件存在。
- Desktop csproj 不含任何 ACB `Reference` 或 `Content` 二进制 binding。
- `.gitmodules` 路径为 `Avalonia/Dependencies/AcbGeneratorFuck`，URL 精确匹配官方仓库。
- service 直接调用 `AcbGeneratorFuck.Generator.Generate`，不存在 `NativeAcbGeneratorInterop`、
  `#if NATIVE_AOT` 或旧 `.aot.dll` 文件。

### 阶段 E：构建、测试与质量复核

1. 生产 API 落地后先运行仅 acb 过滤：
   `dotnet test ...Desktop.Tests.csproj -c Debug --filter FullyQualifiedName~Acb -v:minimal`。
2. 修复仅限允许的 acb 测试目录；生产 API 不匹配时报告明确 blocker，不修改生产代码/csproj。
3. 运行完整 Desktop.Tests，确认新增注册断言与既有“仅四命令/omits acb”测试是否已由生产变更同步更新；若旧测试失败且不在写入范围，报告给主代理处理。
4. 运行 solution 全量测试一次。
5. 调用 `test-gap-analysis` 与 `assertion-quality`，把结果和最终命令写入 `.testagent/status.md`。
6. 对允许路径执行 `git diff --check`，并严格复核每项需求对应的具体测试名。

### acb 需求映射

| Requirement | Planned evidence |
| --- | --- |
| `命令注册/帮助` | `DesktopRegistration_RootAndCommandHelpDiscoverAcbWithExpectedOptions`; `DesktopRegistration_ResolvesAcbDefinitionHandlerAndServiceAsSingletons` |
| `必填参数与默认 previewBegin=60000/previewEnd=80000` | `Invoke_DefaultOptions_UsesLegacyPreviewRange`; `Invoke_MissingEachRequiredAcbOption_DoesNotCallHandler` |
| `路径错误 -7` | `HandleAsync_AnyRelativeAcbPath_ReturnsMinusSevenWithoutCallingService` |
| `生成失败 -8` | `HandleAsync_ServiceFailureOrException_ReturnsMinusEightAndWritesReason` |
| `Handler 到可注入 IAcbGenerateService 映射` | `HandleAsync_AbsolutePaths_ForwardsSameOptionsAndCancellationTokenToInjectedService`; DI singleton 测试 |
| `真实临时 48k PCM WAV 生成 ACB/AWB/MusicSource.xml，并验证 XML musicId/文件名及 ACB/AWB 能由可用解析链重新打开` | `Generate_48KhzPcmWave_CreatesAcbAwbAndMusicSourceThatReopen` |
| `Desktop引用项目https://github.com/NyagekiFumenProject/AcbGeneratorFuck, 不需要dll依赖` | `DesktopProject_ReferencesAcbGeneratorProjectWithoutDllBindings`; `AcbGeneratorSubmodule_UsesOfficialRepository`; `AcbGenerateService_UsesManagedProjectWithoutNativeInterop` |

### Desktop acb 执行结果（2026-08-03）

- [x] 阶段 A：Definition、必填参数、默认值、显式值和 Handler 退出码穿透。
- [x] 阶段 B：路径 `-7`、失败/异常 `-8`、注入映射及取消透传。
- [x] 阶段 C：真实 48 kHz PCM WAV 生成三份产物，解析 XML，并由 DereTore ACB/AWB 解析链重新打开。
- [x] 阶段 D：官方子模块、无条件 ProjectReference、无预编译 DLL binding 和无 Native Interop。
- [x] 阶段 E：Acb 筛选、Desktop.Tests、solution 回归、test-gap-analysis、assertion-quality、编码与 diff 检查。

## FileDialog/SimpleFileSystem 迁移计划（2026-08-03）

1. 覆盖 standalone storage file 的读取、覆盖写、缓存/长度刷新、取消和释放。
2. 覆盖谱面转换与 WAV 调整的 `ISimpleFile` 流分支，同时保留现有 CLI 路径分支测试。
3. 若 SVG 领域属性迁移，覆盖文件流加载与既有路径格式兼容。
4. 先构建并运行 Core 聚焦测试，再构建 Desktop/Browser，最后执行 solution 非增量构建与全量测试。

### FileDialog/SimpleFileSystem 完成清单（2026-08-03）

- [x] `OpenFileAsync`/`SaveFileAsync`/`OpenDirectoryAsync` 分别返回 `Task<ISimpleFile>`、
  `Task<ISimpleFile>`、`Task<ISimpleDirectory>`，取消仍返回空结果。
- [x] 13 个 picker 调用点仅在用户读写范围内迁移；CLI、项目 JSON、日志/缓存、自动关联文件扫描等
  既有路径 I/O 不做全仓替换。
- [x] 用户选择的谱面、WAV、SVG 使用 SimpleFileSystem 读取；目录设置只从 `LocalPath` 持久化原生路径。
- [x] picker 写入统一使用 `ISimpleFile.WriteAsync`；本地目标通过同目录临时文件提交，失败/取消保留原文件。
- [x] 非本地 SVG 内容嵌入谱面格式；直接 picker 打开的谱面通过 `ISimpleFile` 自动保存。
- [x] 完成 Core/Desktop 全量测试、Browser 构建、solution 非增量构建、断言质量和实证伪变异复核。

## 跨平台临时文件夹服务测试计划（2026-08-04）

### 阶段 A：Core 契约与 discard

1. `TemporaryFolderProviderContractTests` 使用事务型内存后端覆盖固定名称复用、唯一占位/并发唯一、嵌套目录、长度/全部字节/只读流、覆盖、追加、幂等删除和 provider 清理。
2. 同一测试类覆盖 writer 抛错、writer 取消、预取消，以及 writer 成功后触发取消仍提交。
3. 参数化覆盖 rooted path、`/`、`\\`、`.`、`..`、控制字符、Windows 非法字符、结尾点/空格与保留设备名。
4. `DiscardTemporaryFolderProviderTests` 覆盖 `IsAvailable=false`、writer 执行、查找为空、读取抛 `FileNotFoundException`、删除/清理不产生数据。

### 阶段 B：Desktop 集成

1. `DesktopTemporaryFolderProviderTests` 使用每例独立临时根，验证默认根常量、物理占位、`LocalPath` 根包含性、跨 provider 实例保留和并发唯一命名。
2. 在隔离根旁放置哨兵文件，验证 `ClearAsync`/递归删除只影响根内内容；测试结束由测试自身回收隔离目录。
3. DI 注册测试解析 `AddOngekiFumenEditorAvaloniaDesktop()`，断言 `ITemporaryFolderProvider` 为单例 Desktop 实现。

### 阶段 C：消费者

1. `ImageLoaderTemporaryCacheTests` 用可控下载函数与内存临时后端证明首次未命中下载并持久化、第二实例命中而不下载。
2. `FileLogOutputTests` 写入两条日志并等待 flush，断言 `logs/runtime` 相对路径和精确追加内容。
3. `EditorProjectFileManagerTemporaryFileTests` 通过临时句柄保存并重新加载工程模型，证明救援序列化不依赖 `System.IO` 路径。
4. Desktop ACB/Jacket 既有单元/集成测试改为注入隔离 Desktop provider，并断言所用工作目录位于注入根；保留原产物内容断言。

### 阶段 D：静态、构建与运行验证

1. 运行 Core 与 Desktop 聚焦测试，修复后各自全量运行；最后执行 solution 非增量构建。
2. 执行 `rg -n TempFileHelper src`、JS 语法检查和 `git diff --check`。
3. 发布并短启动 Desktop Native AOT；发布 Browser Release AOT 与 LLVM Browser。
4. 在独立 localhost origin 中运行 OPFS 与应用日志烟测，确认无 JS interop 错误。
5. 对新增测试逐项复核断言；把最终测试计数、未执行项和残余风险写入 `.testagent/status.md`。

### 需求映射

| Requirement | Planned evidence |
| --- | --- |
| `公共契约测试：唯一占位、固定名称复用、嵌套目录、读写追加、失败写入回滚、取消、删除、清理和路径逃逸防护。` | `TemporaryFolderProviderContractTests` 中逐行为命名的测试 |
| `discard 测试：写入不报错但不产生数据，TryGet 返回空，直接读取按不存在处理。` | `DiscardTemporaryFolderProviderTests` |
| `Desktop 集成测试：可注入隔离根目录，验证默认根路径、LocalPath、跨实例保留、并发唯一命名及清理不越界。` | `DesktopTemporaryFolderProviderTests` |
| `消费者测试：图片缓存命中/未命中、日志追加、救援序列化，以及 Desktop ACB/Jacket 临时路径迁移。` | 阶段 C 的四组测试及既有 ACB/Jacket 集成断言 |
| `确认 rg TempFileHelper 无结果。` | 阶段 D 静态检查 |
| `发布并短启动 Desktop Native AOT。` | 阶段 D Desktop AOT 命令与进程退出记录 |
| `发布标准 Browser Release AOT 和 LLVM Browser；在独立 localhost origin 中执行 OPFS 创建、写入、读取、追加、删除烟测，并确认应用日志实际写入 temp/logs/runtime 且无 JS 互操作错误。` | 阶段 D 两种发布产物及 localhost 浏览器控制台/OPFS 结果 |

### 执行结果（2026-08-04）

- Core 全量 xUnit：219/219；Desktop 全量 xUnit：105/105；合计 324/324，0 失败、0 跳过。
- Desktop Native AOT 发布成功，最终 EXE 为 69,832,704 字节；精确进程短启动存活 8 秒。
- 标准 Browser Release AOT 发布成功；独立 `localhost:13048` origin 中 OPFS 读回 `[1,2,3]`，追加后 `[1,2,3,4,5]`，长度 5，文件与目录清理后均不存在。
- 标准 Browser 真实启动日志写入 `temp/logs/runtime`，烟测时为 6447 字节；应用 origin 控制台无错误或警告。
- Node ESM 初始化异常夹具确认 `SecurityError` 降级、`QuotaExceededError` 上抛。
- LLVM Browser 发布成功，OPFS JS 模块读回 `[7,8,9]`；应用受既有 preview.2 LLVM/Avalonia JSExport 不兼容影响，启动时报缺少 `_Avalonia_Browser__GeneratedInitializer__Register_`，不能宣称运行烟测通过。
