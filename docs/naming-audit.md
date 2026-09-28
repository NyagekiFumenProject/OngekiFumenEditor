# 文件名与类型名一致性审计（naming audit）

> 建立日期：2026-09-28　范围：`OngekiFumenEditor/`（WPF 主工程，**已修正**）、`Avalonia/`（**仅记录，未改动**）

## 判定口径

脚本比对「文件名（去掉 `.xaml` / `.Designer` 约定后缀）」与「文件内声明的类型名」，排除约定性命名：`*.xaml.cs`、`*.Designer.cs`、partial 类后缀文件、同族聚合文件。结果分 **A 单类型但名字不同**、**B 前缀相近（疑似遗留改名）**、**C 聚合文件**。

## 一、WPF 侧（已修正，共 33 处改名 + 1 处类名）

| commit | 主题 | 关键改动 |
| --- | --- | --- |
| `8276152f` | 核心命名不符/拼写 | `ITexture.cs→IImage.cs`；`DefaultTextureDrawing.cs→DefaultSkiaTextureDrawing.cs`；`ICacheSvgManager.cs→ICachedSvgRenderDataManager.cs`；`IToolboxGenerator.cs→ToolboxGenerator.cs`；`MapToViewAttubute.cs→MapToViewAttribute.cs`；`ICommandFormater.cs→ICommandFormatter.cs`（并同步设计文档引用） |
| `ea56b1c0` | 复制粘贴遗留 | 4 模块 `IFumenMetaInfoBrowser.cs→IFumenXxx.cs`；7 模块 `ViewFumenMetaInfoBrowserCommand*.cs→ViewXxxCommand*.cs`；`OgkiFumenListBrowserViewModel.cs→FumenVisualEditorSettingsViewModel.cs` |
| `3d265914` | 大小写/单复数/模块入口 | `AutoPlayFaderLane*→AutoplayFaderLane*`；`BatchModeCommandHandlers.cs→BatchModeSubmodeCommandHandler.cs`；`DamageCommandParsers.cs→BulletDamageCommandParser.cs`；`BeamStartOperationGenerator.cs→BeamOperationGenerator.cs`；`FumenConverter`/`OgkiFumenListBrowser` 的 `MenuDefinitions.cs→<模块>.cs` |
| `158dc00a` | 模块入口（补） | `AudioAdjustWindow/MenuDefinitions.cs→AudioAdjustWindow.cs` |
| `44ae8e6b` | 类名拼写 | `OptionGeneratorTools` 的 `MenuDefintions→MenuDefinitions`（0 处引用） |

保留（非错误）：`McpMenuCommandDefinitions/Handlers.cs`、`ScriptMenuCommandDefinitions/Handlers.cs`、`ObjectTypeFilter.cs` 等同族聚合文件。

验证：改名后编译 0 错误；离屏渲染 harness 复跑 Skia 40/40、GL 16/16 无回归。

注意：`CoreLog`、`GLUtility.CheckError` 使用 `[CallerFilePath]`，改名会改变日志中的路径文本（无害）；Windows 大小写不敏感，纯大小写改名需两步 `git mv`。

## 二、Avalonia 侧（待处理）

共 **A 17 + B 6 + C 4** 处。A/B 建议按 WPF 相同规则改名（纯 `git mv`，零代码影响）；C 建议保留。

### A. 单类型但文件名不符

| 文件 | 声明类型 |
| --- | --- |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Base/EditorObjects/AutoPlayFaderLaneNext.cs` | `class AutoplayFaderLaneNext` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Base/EditorObjects/AutoPlayFaderLaneStart.cs` | `class AutoplayFaderLaneStart` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Kernel/Graphics/Skia/Drawing/TextureDrawing/DefaultTextureDrawing.cs` | `class DefaultSkiaTextureDrawing` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Models/Settings/OngekiSettingJsonSourceContext.cs` | `class OngekiJsonSourceGenerateContext` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenBulletPalleteListViewer/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewFumenBulletPalleteListViewerCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenCheckerListViewer/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewFumenCheckerListViewerCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenConverter/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewFumenConverterCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenEditorSelectingObjectViewer/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewFumenEditorSelectingObjectViewerCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenObjectPropertyBrowser/UIGenerator/ObjectOperationImplement/BeamStartOperationGenerator.cs` | `class BeamOperationGenerator` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenTimeSignatureListViewer/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewFumenTimeSignatureListViewerCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeCommandHandlers.cs` | `class BatchModeSubmodeCommandHandler` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditorSettings/ViewModels/OgkiFumenListBrowserViewModel.cs` | `class FumenVisualEditorSettingsViewModel` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/TGridCalculatorToolViewer/Commands/ViewFumenMetaInfoBrowserCommandDefinition.cs` | `class ViewTGridCalculatorToolViewerCommandDefinition` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Parser/Ogkr/CommandParserImpl/MetaInfo/DamageCommandParsers.cs` | `class BulletDamageCommandParser` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Platforms/Services/FileSystem/Providers/TemporaryEntryExtensions.cs` | `class SimpleFileSystemEntryExtensions` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Utils/Attributes/MapToViewAttubute.cs` | `class MapToViewAttribute` |
| `Avalonia/src/OngekiFumenEditor.Avalonia.Desktop/CommandLine/CommandLineServiceCollectionExtensions.cs` | `class DesktopCommandLineServiceCollectionExtensions` |

### B. 前缀相近（疑似遗留改名）

| 文件 | 声明类型 |
| --- | --- |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Kernel/Graphics/ITexture.cs` | `interface IImage` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenBulletPalleteListViewer/IFumenMetaInfoBrowser.cs` | `interface IFumenBulletPalleteListViewer` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenEditorSelectingObjectViewer/IFumenMetaInfoBrowser.cs` | `interface IFumenEditorSelectingObjectViewer` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenTimeSignatureListViewer/IFumenMetaInfoBrowser.cs` | `interface IFumenTimeSignatureListViewer` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/TGridCalculatorToolViewer/IFumenMetaInfoBrowser.cs` | `interface ITGridCalculatorToolViewer` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Parser/Ogkr/ICommandFormater.cs` | `interface ICommandFormatter` |

### C. 聚合文件（建议保留）

| 文件 | 类型数 | 首个类型 |
| --- | --- |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenEditorSelectingObjectViewer/Base/SelectionFilter/ObjectTypeFilter.cs` | 2 | `FilterObjectTypeCategory` |
| `Avalonia/src/OngekiFumenEditor.Avalonia/Modules/FumenVisualEditor/Setup/EditorProjectSetupContracts.cs` | 6 | `SetupFumenMode` |
| `Avalonia/src/OngekiFumenEditor.Avalonia.Browser/Modules/BrowserOpfsBrowser/BrowserOpfsContracts.cs` | 6 | `BrowserOpfsEntryKind` |
| `Avalonia/src/OngekiFumenEditor.Avalonia.Browser/Platforms/Services/FileSystem/BrowserOpfs/BrowserOpfsJsonModels.cs` | 7 | `BrowserOpfsEntryDto` |
