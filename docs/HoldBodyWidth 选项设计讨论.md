# HoldBodyWidth 选项设计讨论

## 目标

在 `EditorSetting` 中增加 `HoldBodyWidth`，用于控制谱面编辑器内 Hold 主体线条的绘制宽度；在应用设置窗口的“谱面可视化编辑器全局设置”页面中提供可修改控件，并由设置窗口保存。

## 代码库事实

- Hold 主体由 `HoldDrawingTarget` 绞合可见轨道点后调用 `IDrawCommandListBuilder.DrawLines` 绘制。
- 有轨道路径与无轨道路径两个分支目前都将线宽固定为 `13`。
- `DrawLines` 接收 `float` 线宽，且要求数值严格大于 `0`。
- `EditorSetting` 的属性以 `EditorGlobalSetting.Default` 为持久化来源。
- `EditorSetting` 监听全局设置变更，因此一个编辑器修改后，其他已打开编辑器可同步该值。
- 指定的全局设置页直接绑定 `EditorGlobalSetting.Default`，并在设置窗口执行保存时由 `ApplyChanges()` 调用 `Save()`。
- 指定的全局设置页当前主要使用 `TextBox` 编辑数值，尚未使用 `RangeValue`。
- 仓库没有已投入使用的整数步进框；自定义 `RangeValue` 已有绑定 `int` 设置的先例，并能通过滑块表达最小值、最大值与步长。
- 若将 `TextBox.Text` 直接绑定到 `int`，非整数或空文本虽无法写入源属性，但无效文本可能仍显示在输入框中；最终方案改用字符串缓冲统一处理提交与恢复。
- OpenGL 后端在屏幕坐标中应用线宽，Skia 后端也将该值作为描边宽度；界面可将单位表达为 `px`。
- 主工程当前没有一方单元测试项目；仓库中的测试项目均属于依赖项，不适合作为该设置的测试承载位置。
- `EditorProjectDataModel` 公开了一个 `EditorSetting` 实例，项目 JSON 序列化器会写出其公开属性；但画布实际读取的是 `FumenVisualEditorViewModel.Setting` 的独立全局包装实例，项目模型中的同名对象没有运行时消费点。
- 仓库惯例要求新增用户设置时同步更新 `EditorGlobalSetting.settings`、生成的 `EditorGlobalSetting.Designer.cs` 与 `App.config`。
- 设置窗口的 `OK` 是默认按钮；文本框仍聚焦时直接按 `Enter` 可能触发保存但不先触发默认的失焦更新，因此需显式处理才能保证当前文本被提交。
- `EditorSetting` 现有的可写属性会同步 `EditorGlobalSetting.Default` 并调用约 2 秒延迟保存；全局设置页则绕过该包装，仅在 `OK` 时调用 `Save()`。
- 编辑器画布持续逐帧重绘；Hold 主体每帧读取当前编辑器设置即可即时反映宽度变化，无需额外刷新或“应用”按钮。
- 当前固定宽度是 `13`，故新设置默认值确定为 `13`，以保持升级前后的显示不变。

## 最终方案

- 类型：`int`；绘制时由方法调用隐式转换为 `float`。
- 默认值：`13`。
- 允许范围：`1–50`，步长 `1`。
- 生效范围：两个 Hold 主体绘制分支均使用 `target.Editor.Setting.HoldBodyWidth`。
- 持久化：新增用户级 `EditorGlobalSetting.HoldBodyWidth`；设置页在 `OK` 时保存，代码经 `EditorSetting` 修改时沿用约 2 秒延迟保存。
- UI：放入 `Kernel/SettingPages/FumenVisualEditor/Views/FumenVisualEditorGlobalSettingView.xaml` 的 `Render` 顶部，仅使用 `TextBox` 输入，并显示 `px (1–50)`。
- 交互：失焦、按 `Enter` 或点击 `OK` 时提交；提交后通过全局设置变更即时影响所有编辑器画布。
- 本地化：简体中文“Hold主体宽度:”、英文“Hold body width”、日文“Hold本体の幅：”。

## 已确定的输入实现

- ViewModel 使用字符串缓冲属性承接 `TextBox` 的键入内容；缓冲随键入更新但不修改全局设置，因此不属于逐按键提交。
- 失焦、按 `Enter` 与 `ApplyChanges()` 三条路径调用同一个提交方法。
- 提交方法解析整数、限制到 `1–50`、写入 `EditorGlobalSetting.Default.HoldBodyWidth`，再把缓冲格式化为最终有效值。
- 解析失败时不修改全局设置，并把缓冲恢复为当前有效整数。
- `ApplyChanges()` 在 `Save()` 前再次提交缓冲，保证鼠标点击 `OK`、键盘触发默认按钮等路径都不会丢失最后输入。
- 全局设置变化时通知缓冲属性刷新，使外部修改和多编辑器同步后的页面显示保持一致。
- 为可靠区分设置会话的打开、保存与取消，在 Gemini 的 `ISettingsEditor` 中增加 `BeginEdit()` 与 `CancelChanges()` 生命周期方法，并提供默认空实现。
- 设置窗口初始化并收集完编辑器后调用 `BeginEdit()`；所有 `ApplyChanges()` 成功后标记会话已保存；窗口关闭时若未保存，则调用 `CancelChanges()`。这同时覆盖取消按钮、`Esc` 与窗口关闭按钮，且切换设置页面不会误触发回滚。
- `FumenVisualEditorGlobalSettingViewModel` 在 `BeginEdit()` 中记录 `HoldBodyWidth` 原始快照，在 `CancelChanges()` 中恢复全局值和文本缓冲；`ApplyChanges()` 正常保存时不回滚。

## 决策记录

### 已确认 1：允许范围与调节精度

结论：范围 `1–50`，步长 `1`。

理由：底层禁止 `0` 或负数；整数像素足够直观，默认值 `13` 位于范围中部，`50` 也能覆盖明显加粗的调试或可访问性需求。

### 已确认 2：属性是否允许小数

结论：使用 `int`，仅允许整数宽度。

理由：整数与步长 `1` 的决定一致，像素宽度也无需小数；项目已有多个 `int` 类型视觉设置可作为类型先例。

### 已确认 3：影响范围是否包含 SVG 预览

结论：仅影响谱面编辑器画布，不影响 SVG 预览或导出。

理由：`HoldBodyWidth` 属于 `EditorSetting`，需求也指向显示时的 Hold 绘制；SVG 预览是独立输出管线，若共享该值会使导出结果随用户本机偏好变化，降低输出一致性。

### 由代码确定：修改后的生效与保存时机

结论：修改后在当前及其他已打开编辑器的后续绘制帧中即时生效；用户执行设置窗口的现有保存操作时写盘，不增加独立的“应用”按钮。

依据：编辑器画布持续逐帧重绘，`EditorSetting` 又会同步 `EditorGlobalSetting.Default.PropertyChanged`。指定页面直接绑定全局设置，其 `ApplyChanges()` 已负责调用 `Save()`。

### 已确认 4：设置页显示文案

结论：采用以下文案，属性与资源键保持 `HoldBodyWidth`：

- 简体中文：`Hold主体宽度:`
- 英文：`Hold body width`
- 日文：`Hold本体の幅：`

### 已确认 5：设置页归属

结论：放置在 `OngekiFumenEditor/Kernel/SettingPages/FumenVisualEditor/Views/FumenVisualEditorGlobalSettingView.xaml`，不放在 `Modules/FumenVisualEditorSettings/Views/FumenVisualEditorSettingsView.xaml`。

### 已确认 6：全局设置页内的分组

结论：放在 `Render` 分组顶部、`Limit FPS` 之前，并置于 `PreviewMode` 子分组之外。

理由：Hold 主体宽度属于通用画布渲染设置，不只在预览模式生效；放在此处也能与帧率、预览渲染选项形成清晰的视觉设置集合。

### 已确认 7：输入控件

结论：只使用 `TextBox`，不使用 `RangeValue`、滑块或步进框。

影响：必须另行定义非整数、空值及超出 `1–50` 范围时的行为。

### 已确认 8：非法与越界输入

结论：

- 非整数或空文本解析失败时不修改设置，并将输入框恢复为上一个有效值。
- 小于 `1` 的整数自动改为 `1`，大于 `50` 的整数自动改为 `50`。
- `EditorSetting` 读取全局配置时再次限制到 `1–50`，防止历史配置或手工修改配置导致 `DrawLines` 收到非法宽度。

实现方式：在 `FumenVisualEditorGlobalSettingViewModel` 增加字符串缓冲属性和统一提交方法；`EditorSetting.HoldBodyWidth` 同样对初始化值、setter 输入及全局同步值执行限制。

### 已确认 9：文本值提交时机

结论：在输入框失去焦点、按 `Enter` 或执行设置窗口保存时提交；提交后画布立即反映。

理由：逐按键提交会在输入多位整数时产生 `1`、`13` 这样的中间值并让画布闪变。

### 已确认 10：是否显示单位

结论：在 `TextBox` 后增加静态 `TextBlock` 显示 `px`。

理由：输入控件仍然只有一个 `TextBox`，单位不进入可编辑文本，也无需新增本地化资源；这能让 `1–50` 的含义更明确。

### 已确认 11：Hold 类型覆盖范围

结论：同时影响 `HLD`、`CHD`、`XHD` 三类 Hold 的主体线条。

理由：它们当前共用同一个 `HoldDrawingTarget` 和固定宽度 `13`，统一替换才能让选项语义保持为“Hold 主体宽度”，避免不同 Hold 类型出现不一致。

### 已确认 12：是否影响 Hold 头尾图案

结论：仅改变 Hold 主体线条，不缩放起点与终点的 Tap 图案。

理由：头尾由独立的 `HoldTapDrawingTarget` 以贴图方式绘制，而选项名称明确为 `HoldBodyWidth`；保持头尾不变可避免把线宽设置扩展成整体物件缩放。

### 已确认 13：历史非法配置是否回写修复

结论：读取到小于 `1` 或大于 `50` 的历史/手工配置时，将其收敛到最近边界作为有效值；下次用户在设置窗口点击保存时，把收敛后的值回写配置文件。

理由：无需仅因启动时发现非法值而立即写盘；这样既能保证绘制安全，也保持与设置窗口统一的保存时机。

### 历史决策 14：取消设置窗口的行为（已废止）

本项早期决定已由决策 27–30 完整替代，不再作为实现依据。

废止原因：用户最终要求未保存关闭时回滚 `HoldBodyWidth`，包括取消按钮、`Esc` 和窗口关闭按钮，并立即恢复所有已打开编辑器的画布。

当前行为以决策 27–30 及最终方案为准。

### 已确认 15：是否在界面提示允许范围

结论：在输入框后显示 `px (1–50)`，明确提示允许范围。

理由：由于只使用 `TextBox`，用户无法从控件本身得知边界；显示范围能解释越界值为何会被自动修正，且无需增加输入控件或本地化资源。

### 已确认 16：默认值

结论：默认值设为 `13`。

理由：这是当前 `HoldDrawingTarget` 两条绘制路径中写死的线宽，因此升级后视觉效果不会变化，只有主动修改设置的用户才会看到差异。

### 已确认 17：验收与自动化测试范围

结论：不为本功能新建测试工程；要求主项目编译通过，并执行手工场景检查。

手工验收覆盖：默认值、上下界收敛、非整数拒绝、失焦生效、保存后重启保留、取消后不写盘、三类 Hold 同步变化、头尾与 SVG 不受影响。

理由：该范围能覆盖本功能的所有已确认行为，且与项目现有测试基础相称。

### 已确认 18：是否写入项目文件

结论：在 `EditorSetting.HoldBodyWidth` 上添加 `[JsonIgnore]`，不将它写入 `.nyagekiProj`。

理由：该值已明确由全局设置页管理并保存到用户配置；项目文件中的 `EditorSetting` 没有被画布使用，重复序列化只会制造一个无法生效的第二数据源，并造成无意义的项目文件差异。

### 已确认 19：多个编辑器的同步范围

结论：提交新宽度后同时影响所有已打开的谱面编辑器，并由之后新打开的编辑器继承同一个值。

理由：该选项位于全局设置页，`EditorSetting` 现有的全局变更监听也天然支持这一行为；若只影响当前编辑器，反而会与全局设置的归属冲突。

### 已确认 20：编辑器模式覆盖范围

结论：普通编辑模式与编辑器内预览模式都使用 `HoldBodyWidth`。

理由：两种模式共用 `HoldDrawingTarget`；统一应用能保持切换模式时 Hold 主体宽度稳定。此前排除的是独立 SVG 预览/导出管线，不是编辑器内预览模式。

### 已确认 21：按 Enter 保存时的提交行为

结论：文本框聚焦时按 `Enter`，先提交并收敛当前输入，再由设置窗口的默认 `OK` 流程保存和关闭。

理由：保留失焦提交，不改成逐按键提交，只额外处理 `Enter`，能与此前确认的提交时机一致且避免键盘保存丢失最后一次输入。

### 已确认 22：`EditorSetting` setter 的保存语义

结论：`EditorSetting.HoldBodyWidth` 保持可写。setter 先收敛到 `1–50`，同步 `EditorGlobalSetting.Default.HoldBodyWidth`，再调用 `RequestSave()`。

全局设置页仍直接绑定自己的代理属性，因此页面内编辑只在 `OK` 时写盘；只有代码直接通过 `EditorSetting.HoldBodyWidth` 修改时才走延迟保存。

理由：保持 `EditorSetting` API 与其他可写属性一致。

### 已确认 23：无效文本的界面表现

结论：失焦或按 `Enter` 提交空值、非整数等无效文本时，立即把输入框恢复为上一个有效整数，不保留无效文本。

背景：`TextBox` 输入空值或非整数时，WPF 会拒绝更新 `int` 源属性，但默认可能让无效文本继续显示在输入框中，直到页面重建。

理由：这样“保留上一个有效值”在数据和界面上完全一致，也避免用户点击 `OK` 后误以为无效文本已保存。

### 已确认 24：缩放时的宽度语义

结论：保持固定屏幕像素宽度，不随 `XGridUnitSpace`、水平偏移或垂直显示缩放改变。

理由：当前固定值 `13` 在 OpenGL 和 Skia 管线中就是按屏幕空间线宽应用；直接替换为设置值可保持既有行为，且与界面单位 `px` 一致。

### 已确认 25：磁盘配置损坏的恢复边界

结论：不处理磁盘中的非整数损坏值；本功能只将可解析整数限制到 `1–50`。

背景：范围外但仍是整数的配置值可以安全收敛；若用户手工把强类型配置写成 `abc` 等非整数，`ApplicationSettingsBase` 可能在返回属性值之前就抛出配置解析异常。

理由：非整数属于整个用户配置文件损坏，应沿用应用现有的配置错误处理；不为单个字段绕过强类型设置框架。

### 已确认 26：渲染后端验收范围

结论：手工验收同时覆盖 OpenGL 与 Skia 两个可用渲染后端，至少分别检查宽度 `1`、`13` 和 `50`。

理由：设置接线共用同一绘制命令，但最终线宽由两个独立后端实现；分别检查可防止某一后端出现缩放、裁切或抗锯齿差异。

### 已确认 27：取消时回滚

结论：用户点击设置窗口的“取消”按钮时，回滚 `HoldBodyWidth` 到本次打开设置窗口时的值。

影响：需要在设置会话开始时保存快照，并在明确的取消路径恢复 `EditorGlobalSetting.Default.HoldBodyWidth` 与输入框显示；正常点击 `OK` 时丢弃快照，不回滚。

### 已确认 28：取消时的回滚范围

结论：只回滚本次新增的 `HoldBodyWidth`，不改变该设置页其他已有选项的取消行为。

理由：严格限定在本需求范围内，避免把一个新选项变成整页设置事务化改造。

### 已确认 29：哪些关闭路径触发回滚

结论：除点击“取消”按钮外，按 `Esc` 和点击窗口关闭按钮等所有未成功执行 `OK` 保存的关闭路径都回滚 `HoldBodyWidth`。

理由：避免不同关闭方式产生不一致结果；实现时以“设置会话是否已成功保存”判断，而不是只监听按钮点击。

### 已确认 30：回滚后的画布表现

结论：取消回滚时，立即让所有已打开编辑器中的 Hold 主体宽度恢复，不等待重启或重新打开谱面。

实现效果：恢复快照会触发共享全局设置的变更通知，现有逐帧绘制会在下一帧显示原宽度。

## 逐文件实施清单

- `OngekiFumenEditor/Properties/EditorGlobalSetting.settings`：新增用户级 `System.Int32` 设置，默认值 `13`。
- `OngekiFumenEditor/Properties/EditorGlobalSetting.Designer.cs`：同步生成强类型 `HoldBodyWidth` 属性。
- `OngekiFumenEditor/App.config`：在编辑器全局设置节声明默认值 `13`。
- `OngekiFumenEditor/Modules/FumenVisualEditor/Models/EditorSetting.cs`：新增带 `[JsonIgnore]` 的 `int HoldBodyWidth`，在初始化、setter 与全局变更同步时限制到 `1–50`，并沿用通知和延迟保存模式。
- `OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/TargetImpl/OngekiObjects/Holds/HoldDrawingTarget.cs`：用当前编辑器的 `HoldBodyWidth` 替换两处固定值 `13`。
- `OngekiFumenEditor/Kernel/SettingPages/FumenVisualEditor/ViewModels/FumenVisualEditorGlobalSettingViewModel.cs`：增加文本缓冲、统一提交、范围收敛、无效恢复、外部变更同步，并在 `ApplyChanges()` 保存前提交。
- `OngekiFumenEditor/Kernel/SettingPages/FumenVisualEditor/Views/FumenVisualEditorGlobalSettingView.xaml`：在 `Render` 顶部加入标签、单个 `TextBox` 和 `px (1–50)`，接入失焦与 `Enter` 提交。
- `OngekiFumenEditor/Properties/Resources.resx`、`Resources.zh-Hans.resx`、`Resources.ja.resx`、`Resources.Designer.cs`：加入并生成三语 `HoldBodyWidth` 文案。
- `Dependences/gemini/src/Gemini/Modules/Settings/ISettingsEditor.cs`：直接加入 `BeginEdit()` 与 `CancelChanges()` 生命周期方法，默认不执行操作。
- `Dependences/gemini/src/Gemini/Modules/Settings/ViewModels/SettingsViewModel.cs`：会话开始时建立快照；所有设置成功应用后标记已保存；未保存关闭时统一调用取消回滚。

## 验收矩阵

- 编译：主项目成功编译，不新建测试工程。
- 默认与持久化：首次使用显示 `13`；点击 `OK` 后重启仍保留；取消、`Esc` 或关闭窗口时恢复本次设置会话开始时的值且不写盘。
- 输入：空值/非整数恢复上一有效值；`0`、负数收敛到 `1`；大于 `50` 收敛到 `50`；失焦、`Enter`、`OK` 都能提交最后输入。
- 同步：所有已打开编辑器及之后新开的编辑器使用同一宽度。
- 绘制范围：`HLD`、`CHD`、`XHD` 在普通编辑与编辑器内预览模式均生效；头尾 Tap 图案和独立 SVG 预览/导出不变。
- 渲染：OpenGL 与 Skia 分别检查 `1`、`13`、`50`，并确认缩放时保持固定屏幕像素宽度。

## 状态

讨论完成，完整方案已由用户确认并完成实现。

## 实现验证

- 主项目 `Debug` 构建成功，C#、WPF XAML 与资源编译均为 `0` 错误。
- `HoldDrawingTarget` 的两条主体绘制路径均已使用 `EditorSetting.HoldBodyWidth`。
- 三种本地化资源与用户设置键均通过 XML 解析和重复键检查。
- 主仓库与嵌套 Gemini 仓库的相关差异均通过 `git diff --check`。
- 构建仍报告仓库既有的 NuGet 兼容性/漏洞、nullable 与少量未使用成员警告；本实现未新增编译错误。
- OpenGL/Skia 的视觉效果、设置窗口交互与重启持久化仍需在 GUI 中按“验收矩阵”手工确认。
