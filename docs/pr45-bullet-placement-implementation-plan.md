# PR #45：子弹持续放置与伤害快捷键实现流程

编写日期：2026-10-20；实现记录更新：2026-10-21。目标工程：`OngekiFumenEditor/`（WPF）。

需求依据：[pr45-bullet-placement-functional-requirements.md](pr45-bullet-placement-functional-requirements.md)。本文保留实施设计与需求映射，**WPF 功能代码已实现，未签入，等待用户代码验收**；实际验证结果和原生输入覆盖限制见第 6 节。2026-10-20 用户已确认：`FumenBulletPalleteListViewerView.xaml` 的界面外观与布局对齐需求 U 节链接的 PR #45 diff；随后移除 U-3 的原事件契约约束，新增复选框交互改按 MVVM 实现。需求编号沿用原文；本文的 D、R、F、V 分别表示交互口径、代码落点、实施阶段与验收场景。

## 1. 目标与范围

1. **G-1 — 连续放置。** 选择一个“模板＋Bullet／Bell”组合后，显示鼠标预览，支持点击或拖放放置，并在每次放置后保留选择；统一使用现有吸附规则。
2. **G-2 — 取消与快捷调整。** 完整覆盖 B 组取消条件；对谱面内已选中的 Bullet 提供伤害循环快捷键，支持混合选择、批量修改与文本输入保护。
3. **G-3 — 保持改动边界。** 不纳入本地子弹组模板箱、其他编辑增强或按住鼠标沿途刷物件，对应原文 S-1、S-2、S-3。U 组保留原 diff 的控件外观、文案、尺寸、间距与既有入口，不混入后续标题缩短、删除旧创建入口或收紧操作列的改动；新增复选框的绑定与命令采用 MVVM，不受已移除的 U-3 原事件契约约束。其余内部实现不必照搬原 PR，Avalonia 功能不在本次实施范围内。

## 2. 本轮采用的四项交互口径

以下 D-1～D-4 是实施时采用的推荐口径，已回写需求文档 Q 节；属于本轮实现选择，不追溯为原 PR 已明确的需求。U 组保留的界面约束已由用户确认。后续若调整交互口径，应同时调整实现与验收，不改变已确认的界面外观和入口布局；新增复选框事件不要求与原 diff 一致。

| 编号 | 对应问题 | 采用口径 | 主要取舍 |
| --- | --- | --- | --- |
| **D-1** | Q-1：拖放 | 勾选 Bullet／Bell 开启持续放置，在谱面有效位置单击，或在编辑区按下、拖动后释放，各生成一个物件并保留会话。右侧旧链接保留原有单次拖放；其启动时取消持续放置，完成后不自动进入持续状态。 | 不给新复选框增加拖放源绑定，不删除旧链接，不实现沿途绘制；同一手势不能重复提交。 |
| **D-2** | Q-2：失焦 | 以 `IEditorDocumentManager.OnActivateEditorChanged` 的当前活动编辑器切换为取消边界，而不是窗口失活或谱面控件失焦。 | 同一窗口内点击管理器、过滤框或属性面板不取消；切换到另一编辑器会取消；同一编辑器重复激活保留。 |
| **D-3** | Q-3：伤害循环 | ↑：`Normal → Hard → Danger → Normal`；↓反向。多个 Bullet 分别从各自原值前进或后退一步。 | 不把混合伤害强行统一；一次按键作为一个可撤销的批量编辑动作。 |
| **D-4** | Q-4：快捷键配置 | 默认 ↑／↓，接入现有快捷键配置体系。 | 不另写键位配置；保留 #42 的完整意图，不冒充 #45 已明确要求。 |

## 3. 实施前代码起点与代码落点

R-1～R-7 保留编写方案时的静态核对，表中的“当前”指实施前状态，不代表交付后仍缺失相应能力。R-8、R-9 是本轮新增入口，具体完成记录见第 6 节。

| 编号 | 当前入口 | 当前事实与实施方向 |
| --- | --- | --- |
| **R-1** | [管理器 ViewModel](../OngekiFumenEditor/Modules/FumenBulletPalleteListViewer/ViewModels/FumenBulletPalleteListViewerViewModel.cs)、[管理器视图](../OngekiFumenEditor/Modules/FumenBulletPalleteListViewer/Views/FumenBulletPalleteListViewerView.xaml) | 当前仍由 `OnMouseMoveAndDragNewBullet/Bell` 发起一次性拖放，操作列保留两个创建链接与克隆入口，没有“连续放置”列。按原 diff 新增该列的外观，通过可绑定状态与命令接入唯一放置会话，并保留原操作列；不引入原 PR 的控件事件同步入口。 |
| **R-2** | [编辑器交互 partial](../OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs)、[编辑器视图](../OngekiFumenEditor/Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml) | 已有鼠标按下／移动／释放、框选与拖动物件、`Grid_Drop` 和上下文菜单。新放置手势需要在这些正常操作之前分流，避免一次输入同时创建和框选。 |
| **R-3** | [EditorAddObjectDropAction](../OngekiFumenEditor/Modules/FumenVisualEditor/Base/DropActions/EditorAddObjectDropAction.cs)、[DefaultObjectInteractiveAction](../OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/Interactives/Impls/DefaultObjectInteractiveAction.cs) | 现有拖放经 `MoveObjectTo` 完成坐标处理，再通过 `UndoRedoManager` 添加物件。吸附已包含 T/X 开关、强制吸附及 `ForceXGridIntegerUnit`，应复用而不是复制公式。 |
| **R-4** | [DefaultEditorDocumentManager](../OngekiFumenEditor/Modules/FumenVisualEditor/Kernel/DefaultImpl/DefaultEditorDocumentManager.cs)、[编辑器生命周期](../OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.cs) | 已有活动谱面通知及激活／停用／关闭流程。当前活动编辑器 setter 会直接通知，因此放置侧必须区分真正切换与重复通知同一个实例。 |
| **R-5** | [KeyBindingDefinitions](../OngekiFumenEditor/Modules/FumenVisualEditor/KeyBindingDefinitions.cs)、[ActionMessageKeyBinding](../OngekiFumenEditor/UI/KeyBinding/Input/ActionMessageKeyBinding.cs)、[DefaultKeyBindingManager](../OngekiFumenEditor/Kernel/KeyBinding/DefaultKeyBindingManager.cs) | 已有声明、视图绑定及配置保存／加载链路。已读的按键匹配与触发代码不提供文本输入焦点保护，不能假定新增动作天然满足 C-4。 |
| **R-6** | [Bullet](../OngekiFumenEditor/Base/OngekiObjects/Projectiles/Bullet.cs)、[BulletDamageType](../OngekiFumenEditor/Base/OngekiObjects/Projectiles/Enums/BulletDamageType.cs) | 已有 `BulletDamageTypeValue` 及 `Normal/Hard/Danger`。这里不需要新增伤害模型或更改谱面格式；操作对象是 Bullet 实例，不是模板或 Bell。 |
| **R-7** | [连接物件操作](../OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/ConnectableObjectOperationViewModel.cs)、[Hold 操作](../OngekiFumenEditor/Modules/FumenObjectPropertyBrowser/ViewModels/HoldOperationViewModel.cs)、[Gemini 工具箱](../Dependences/gemini/src/Gemini/Modules/Toolbox/Views/ToolboxView.xaml.cs) | 其他工具各有 `DragDrop.DoDragDrop` 启动入口。本轮 B-5 按用户口径改为在编辑器原有 `Grid_Drop` 取消，不接入 `QueryContinueDrag`，因此不覆盖拖放启动阶段；这些入口仍作为 Drop 实际落入编辑器的来源。 |
| **R-8** | [DrawProjectilePlacementPreviewHelper](../OngekiFumenEditor/Modules/FumenVisualEditor/Graphics/Drawing/Editors/DrawProjectilePlacementPreviewHelper.cs)；接入[主绘制 partial](../OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.Drawing.cs) | 已新增用户指定的 A-3 绘制 helper。通过 `IFumenBulletPalleteListViewer` 的预览请求属性读取当前模板与物件类型，绘制同一会话的有效候选位置；不承担谱面提交或命中注册。 |
| **R-9** | [IFumenBulletPalleteListViewer](../OngekiFumenEditor/Modules/FumenBulletPalleteListViewer/IFumenBulletPalleteListViewer.cs)、[IPlaceBatchProjectileInfo](../OngekiFumenEditor/Modules/FumenBulletPalleteListViewer/IPlaceBatchProjectileInfo.cs) | 已增加 `IsRequestPlaceBatchProjectilePreview`、`RequestBatchPlaceProjectileInfo`，以只读 `Pallete`、`IsBell` 描述子弹模板及 Bullet／Bell 类型；详细契约见 I-1。 |

### I-1 — 管理器预览请求契约

两个管理器属性及信息接口名称按用户指定；本轮实现采用只读 `Pallete`、`IsBell`，接口形状如下，空值语义见 I-1.2。

```csharp
public interface IFumenBulletPalleteListViewer : ITool
{
    bool IsRequestPlaceBatchProjectilePreview { get; }
    IPlaceBatchProjectileInfo RequestBatchPlaceProjectileInfo { get; }
}

public interface IPlaceBatchProjectileInfo
{
    BulletPallete Pallete { get; }
    bool IsBell { get; }
}
```

1. **I-1.1 — 请求内容。** `Pallete` 引用当前谱面中的模板；`IsBell = true` 表示 Bell，`false` 表示 Bullet，不再同时维护可互相矛盾的 `IsBullet` 标记。信息对象只描述“准备放什么”，不携带控件、鼠标事件、绘制资源或动态位置；当前候选坐标仍由 F-3.2 的公共定位链计算。实现对象对外只读，切换模板或类型时替换，不在绘制过程中改写请求内容。
2. **I-1.2 — 请求一致性。** 无请求时，`RequestBatchPlaceProjectileInfo` 返回 `null`，`IsRequestPlaceBatchProjectilePreview` 为 `false`；有请求时，信息非空且引用当前谱面的有效模板，布尔值为 `true`。布尔值从请求是否存在派生，不另存可独立修改的开关。鼠标暂时离开绘图区只影响实际绘制可见性，不清空请求；会话取消或模板失效才清空。可空标注按工程现行约定处理，上述空值语义不变。
3. **I-1.3 — 唯一状态来源。** 保留 F-1 的单一放置会话：建议由当前编辑器持有请求信息与候选状态，管理器接口的两个属性仅呈现该会话的当前请求，不复制维护另一套模板或类型。管理器命令负责发起选择／取消，所有 B 组取消条件走统一取消入口。UI 勾选、helper 预览与正式放置必须对应同一份请求信息。
4. **I-1.4 — 通知与读取。** 请求替换、清空或当前编辑器真正切换时，管理器 ViewModel 转发相应属性变更通知，刷新两个公开属性及派生勾选状态；重复指定同一编辑器不重置请求。信息实例在请求未变时复用，属性 getter 和逐帧绘制不新建它。
5. **I-1.5 — 绘制消费。** 渲染宿主在初始化时取得并缓存管理器接口引用，helper 读取这两个公开属性，不依赖具体管理器 ViewModel 或视图控件，也不在每帧解析 IoC。绘制开始时取得当前请求，只有正在绘制的编辑器确为请求所属的活动编辑器、候选位置属于该请求且有效时才显示，避免切换模板或谱面后绘制旧候选物件。

接口命名中的 `Batch` 不扩展为“按住沿途连续绘制”，也不等同于现有 `IsBatchMode`；F-1 与已有批量工具互斥的边界保持不变。

## 4. 实施顺序与设计约束

### F-1 — 建立唯一的放置会话

1. **F-1.1** 按 D-1～D-4 定稿交互；将放置状态收敛到当前编辑器的一处所有者。建议由 `FumenVisualEditorViewModel` 持有，管理器通过只携带模板与物件类型的命令发出选择／取消请求；`IFumenBulletPalleteListViewer` 按 I-1 暴露该会话的只读请求视图，UI 通过状态绑定呈现勾选结果，同一窗口切换编辑器时先清掉旧会话。
2. **F-1.2** 会话以 `IPlaceBatchProjectileInfo` 保存模板引用与 Bullet／Bell 类型，另持有瞬态候选物件、位置和当前手势状态，不重复保存一套可独立改变的模板／类型字段。未激活时请求为空；激活时只有一个组合。不要把这些状态写进 `BulletPallete` 或谱面文件，也不要用模板列表的多选集合表达放置选择。
3. **F-1.3** 提供集中、可重复调用的开始／替换、更新预览、提交一次、取消操作。可用同一 ViewModel 的独立 partial 组织代码，不必新增通用工具框架。仅允许当前有效谱面的编辑模式进入放置；与已有批量工具互斥，避免两套输入处理同时生效。

阶段完成条件：即使 UI 尚未接齐，切换模板或物件类型也只能得到一个活动组合，所有取消都能收敛到同一清理入口。

### F-2 — 改造子弹管理器入口

1. **F-2.1** 按 U-1、U-2、U-4、U-6 对齐指定 diff 的外观：在 ID 列前新增 `Width="110"`、标题资源为 `ContinuousProjectilePlacement` 的列，横向排列 Bullet／Bell 两个 `CheckBox`，保留各自 `Content`、`Tag` 及 Bullet 的 `Margin="0,0,8,0"`。中文标题为“连续放置”，不再缩短为“放置”；绑定与命令不照搬原事件实现。
2. **F-2.2** 采用 `IsChecked` 单向状态绑定与 `Command`／`CommandParameter`：勾选状态由唯一放置会话派生，命令只接收模板和物件类型，执行选择／取消后通过属性变更通知刷新相关状态。不要同时通过双向属性写入和命令重复更新会话。ViewModel 不持有 `CheckBox`／`FrameworkElement`，不接收控件或 `ActionExecutionContext` 来读取勾选和 `Tag`，不依赖 `Loaded`／`DataContextChanged` 遍历控件修正状态；保留的静态 `Tag` 不作为业务状态来源。切换模板、类型、取消或回收列表项后均由绑定反映真实状态，不另给复选框添加拖放交互。
3. **F-2.3** 按 U-5、U-7 保留右侧原有 Bullet／Bell 创建链接和克隆入口，以及原 `Width="240"`、文案、提示和事件绑定；保留其余工具栏、列与列表设置，不进行额外布局清理。补齐原标题资源的 en／zh-Hans／ja 值，新增功能动作名称仍沿用现有本地化方式。

阶段完成条件：界面外观与保留的 U 组约束一致，勾选由 MVVM 状态绑定和命令驱动；无需 ViewModel 操作控件即可保持互斥与正确回显。新增持续放置选项与原有单次拖放链接各自职责明确，原模板管理能力不变。

### F-3 — 接通预览与单次放置

1. **F-3.1** 放置会话根据 `IPlaceBatchProjectileInfo.Pallete` 与 `IsBell` 准备可复用的瞬态候选 Bullet／Bell；`DrawProjectilePlacementPreviewHelper` 通过管理器接口读取预览请求，专门负责绘制其有效候选位置。鼠标移动只更新候选状态，不改请求信息、不向 `Fumen` 添加物件，不改变选择、脏状态或撤销历史。预览不进入正常物件命中与框选集合；鼠标离开有效绘图区时只隐藏预览，保留请求，不等同于 D-2 的失焦取消。
2. **F-3.2** 预览与正式放置共用定位链：将编辑区指针坐标转换为谱面坐标，经 `MoveObjectTo` 所在交互链应用当前 T/X 吸附、强制吸附及 `ForceXGridIntegerUnit`，得到同一份有效候选 `TGrid`／`XGrid`。helper 读取该结果绘制，不复制吸附公式，也不自行再吸附一次；无法得到有效落点时不显示或提交旧位置。缓存最后的视口内指针位置，在鼠标移动、滚动、缩放或吸附设置变化时刷新候选位置，指针静止时也保持准确。既有单次 WPF 拖放仍通过 `Grid_Drop` 接入同一定位／提交链，不改右侧旧链接绑定；如持续手势采用 WPF 拖放，其位置更新还需接入 `DragOver`。
3. **F-3.3** 持续放置的点击／拖动释放与既有单次 Drop 复用“提交一个物件”的逻辑。沿用现有编辑模式、谱面时长边界和模板有效性检查；每次创建独立 Bullet／Bell，通过既有撤销系统加入谱面。持续放置使用当前会话模板并在成功后保留会话；旧链接使用该行模板且保持单次语义，不隐式激活持续放置。
4. **F-3.4** 同一个手势只能提交一次；无效 Drop 不创建。提交时固定物件位置和属性，Redo 恢复该次操作结果，不根据后来改变的鼠标位置、模板选择或吸附设置重新计算。可以复用或调整现有添加动作，但不要直接复制当前 Drop 方法形成两套提交逻辑。预览对象可在会话内复用，不在每次鼠标移动或渲染帧中重新创建。
5. **F-3.5** 新增 `Graphics/Drawing/Editors/DrawProjectilePlacementPreviewHelper`，按 I-1 消费管理器预览请求，通过现有 `IFumenEditorDrawingContext` 与 `IDrawCommandListBuilder` 输出绘制命令。在主绘制 partial 的正常物件绘制结束、恢复默认绘制上下文后调用一次，使用同一视图矩阵与坐标转换，避免按每个变速组重复绘制。复用 Bullet／Bell 的贴图、尺寸、锚点与贴图缩放计算，必要时提取无命中副作用的共享绘制部分；不直接调用会执行 `RegisterSelectableObject` 的完整设计模式绘制路径。请求未开启、信息为空、位置无效或不在编辑模式时不画；helper 不反向修改请求／会话或持有 WPF 控件。初始化、渲染资源重建和释放随现有 helper 生命周期接入，绘制缓冲复用，共享纹理不重复释放，不在每次鼠标移动或每帧重新创建物件、请求信息或加载贴图。

阶段完成条件：持续放置时点击一次／拖放一次各生成一个物件，继续操作无需重选模板；旧链接仍可单次拖放。预览不污染谱面，所有放置均可正确撤销与重做。

### F-4 — 一次补齐取消与事件收尾

所有入口调用 F-1 的取消操作，同时清除会话、预览、勾选和待提交手势。下表中的 B 编号均指原需求，而非新增规则。

| 需求 | 接入与注意点 |
| --- | --- |
| **B-1** | 使用原有绘图区鼠标事件：`OnMouseDown` 的右键按下调用取消入口并消费按下，`OnMouseUp` 只消费同一次右键释放。不新增窗口 `ContextMenuOpening` 订阅，因此不额外保证右键菜单抑制。 |
| **B-2** | 订阅 `IEditorDocumentManager.OnActivateEditorChanged`：只有当前活动编辑器不再是本实例时取消；同一实例重复激活不取消。不再监听窗口 `Deactivated`。 |
| **B-3、B-7** | 活动编辑器通知先比较实例是否相同，只有真正换谱面才取消；关闭编辑器或替换其谱面数据时也清理会话，不能把旧模板引用带入新数据。 |
| **B-4** | 编辑／预览模式变化，以及其他编辑模式接管输入时取消。回到编辑模式不能自动恢复旧放置。 |
| **B-5** | 在原有 `Grid_Drop` 入口最先调用取消，覆盖其他工具的 Drop 实际落入编辑器的时刻；不订阅 `QueryContinueDrag`，因此不在拖放启动阶段取消。持续放置自身的鼠标手势不走 OLE Drop，不会被误取消。 |
| **B-6** | 监听当前谱面的模板集合变化，而不仅是在管理器“删除”按钮上清理；提交前再次确认模板仍属于该谱面。仅在删除真正成功或模板实际失效时取消，删除被拒绝、过滤隐藏模板都不等于失效。 |

阶段完成条件：取消发生后，即使原拖放稍后返回或释放事件晚到，也不能补放一个物件；窗口／编辑器结束使用时不遗留事件订阅。

### F-5 — 加入伤害循环快捷键

1. **F-5.1** 在现有 `KeyBindingDefinitions` 与编辑器视图绑定中加入上一种／下一种伤害动作，默认 ↓／↑；按 D-4 复用现有配置链，并检查与已有绑定的冲突，不在全局裸 `KeyDown` 中硬编码两条旁路。
2. **F-5.2** 执行前检查活动编辑器、编辑模式及实际键盘焦点。文本框、富文本、可编辑下拉框的输入部分等文本编辑场景保留原键位行为；命令被拦截或没有选中 Bullet 时，不消费方向键。
3. **F-5.3** 从当前谱面的 `SelectObjects` 筛选 Bullet，按 D-3 对每个原值分别循环；一次按键保存该批对象的旧值与目标值，提交一个撤销动作。Undo 恢复各自旧值，Redo 恢复固定目标值；混选中的 Bell 和其他物件不修改，也不清除其选择。此动作不改变当前放置模板或放置类型。

阶段完成条件：单选、多选及混合伤害结果正确，文本输入不受干扰，撤销／重做还原的是整批对象各自的状态。

### F-6 — 集成验证与交付

1. **F-6.1** 接通上述路径后构建 WPF 生产工程；优先在现有独立校验设施中补充真正影响用户的边界回归，例如取消后晚到的释放／Drop、相同编辑器重复通知、混合伤害撤销。若确需独立测试工程，按 csproj 调用，不加入 `OngekiFumenEditor.sln`。U 组明确指定的 XAML 属性与文案通过 diff 核对及真实视图验收，常驻行为测试不固定源代码排版或私有方法结构。
2. **F-6.2** 用真实 WPF 视图执行第 5 节的场景，观察勾选、预览、菜单与谱面实际结果。直接调用方法或 MCP 修改对象不能替代鼠标路由、焦点、拖放与画面的验收。测试实例须遵守仓库后台启动和立即最小化约定，仅操作仓库路径下自建实例；打开谱面只用 `tests/mcp-e2e/.tmp/` 下带音频的完整副本，不接管用户桌面实例。
3. **F-6.3** 完成后记录实际执行过的验证及未覆盖边界，更新需求中的设计决定和功能说明，只删除确已被替代的内部路径；原 diff 保留的右侧创建链接及其行为继续保留。按仓库约定追加 `Avalonia/WPF-MIGRATION-SYNC.md`，注明 WPF 改动与 Avalonia 尚未处理，**不改变同步点**。

## 5. 验收清单

本表保留 V-1～V-12 的验收目标，实际执行的层次、结果及未覆盖边界另见第 6 节；不能仅凭合成路由事件把原生鼠标、焦点和 OLE 拖放标为已验收。

| 编号 | 对应需求 | 场景与预期 |
| --- | --- | --- |
| **V-1** | A-1、A-2；U-1、U-2；I-1 | 在不同模板、Bullet／Bell 之间切换，并滚动触发列表虚拟化；始终只有一个活动项，公开请求中的模板和类型与勾选及预览一致，行多选不改变放置组合。取消后请求为空、请求标志为 false、勾选与预览同时清除；绑定不依靠加载事件手动修正。 |
| **V-2** | A-3、A-4、A-5；Q-1；I-1 | 勾选一次后在谱面内连续单击、拖动释放；每次只新增一个并保持选择，预览正确，释放不重复新增。鼠标移出绘图区时隐藏预览、不创建物件，但请求仍有效；返回后恢复预览。另测保留的右侧旧链接：按 D-1 接管后只做单次拖放，不误启动持续放置。 |
| **V-3** | A-3、A-6 | 切换 T/X 吸附、强制吸附与整数 XGrid 设置，并改变缩放／滚动位置；helper 的预览与最终落点均遵循现有规则。额外保持鼠标静止后改变视口或吸附设置，预览仍更新到正确候选位置；无效位置不沿用上次落点。 |
| **V-4** | B-1 | 激活后在绘图区内右键按下再释放：会话和预览消失；不验证其他面板右键或菜单抑制。 |
| **V-5** | B-2；Q-2 | 同一编辑器重复激活时保留会话；切换到另一编辑器后会话清空，回来不自动恢复。不再验证窗口失活或浮动工具窗激活。 |
| **V-6** | B-3、B-4、B-7 | 切换谱面、切换编辑／预览或批量模式后不再携带旧状态；重复通知同一个当前编辑器则保持放置。关闭／重新加载谱面无旧预览残留。 |
| **V-7** | B-5 | 从工具箱、连接物件操作或 Hold 操作拖放并在编辑器上释放，Drop 处理前放置已退出；拖放启动阶段不要求取消，也不要求拦截其他工具的拖放。 |
| **V-8** | B-6 | 删除当前未被引用的模板后无法再放置；从非管理器入口移除模板同样生效。删除被引用模板而遭拒绝、或仅过滤隐藏模板时，不误判失效。 |
| **V-9** | C-1、C-2、C-3；Q-3 | 选中 Normal／Hard／Danger 各一个 Bullet，并混入 Bell；↑ 后分别为 Hard／Danger／Normal，↓ 为反向，Bell 不变。仅选 Bell 时不消费方向键。 |
| **V-10** | C-4；Q-4 | 管理器、属性编辑及其他文本输入控件内的 ↑／↓ 保持原行为；编辑区内可触发动作。采用 D-4 时，改键并重新加载配置后使用新键位。 |
| **V-11** | A-3；编辑操作一致性 | 连续放置逐次撤销／重做；混合伤害调整整批撤销／重做。改变吸附设置或当前放置选择后再 Redo，仍还原原操作结果；仅移动预览不修改谱面或脏状态、不产生撤销记录，预览不能被点击选中或框选。 |
| **V-12** | U-4、U-5、U-6、U-7 | 核对原 diff 的外观规格与真实视图，不比较已移除的原事件契约：标题资源为 `ContinuousProjectilePlacement`，中文为“连续放置”；新列宽 110、Bullet 间距 `0,0,8,0`，ID 列宽 60、操作列宽 240 不变。右侧 Bullet／Bell 创建及克隆均保留；新建、删除、过滤、行内编辑、多选正常，其他布局不额外改动。 |

## 6. 实现与验证记录（2026-10-21）

### 6.1 实现落点

1. **E-1 — 请求与管理器。** 新增 `IPlaceBatchProjectileInfo` 和不可变的 `PlaceBatchProjectileInfo`；管理器接口公开用户指定的两个请求属性，ViewModel 只呈现活动编辑器的会话。新列使用单向勾选绑定、`ProjectilePlacementCheckedConverter` 和命令，不读写控件来同步状态。保留 U 组规定的列宽、间距、三语标题及右侧 Bullet／Bell 创建和克隆入口。
2. **E-2 — 会话与输入。** [FumenVisualEditorViewModel.ProjectilePlacement.cs](../OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.ProjectilePlacement.cs) 持有唯一请求、可复用候选和待提交手势；点击／拖动释放一次提交一个独立物件。`FumenVisualEditorViewModel` 构造函数订阅 `IEditorDocumentManager.OnActivateEditorChanged`，`Dispose` 时解除；不再依赖视图生命周期注册输入事件。B-1 的右键取消由原有绘图区鼠标事件判断，B-5 由原有 `Grid_Drop` 取消，左键手势统一由原有 `OnMouseDown`／`OnMouseUp` 及其 `TryHandle...` 路径处理，不再注册任何窗口鼠标事件。`FumenVisualEditorView.xaml.cs` 保持原版。谱面切换、模式接管、项目数据替换、模板集合失效和 Dispose 都清理会话。
3. **E-3 — 定位与绘制。** 预览和旧单次 Drop 共用现有交互定位及物件提交链。helper 复用 Bullet／Bell 的贴图、尺寸、锚点和缩放，绘制半透明候选，不向谱面、选择集或命中缓存注册物件。静止指针下的视口、吸附设置和时长边界变化都会重新判定；无效位置不沿用旧候选，Undo／Redo 使用提交时固定的位置。
4. **E-4 — 伤害快捷键。** 默认 ↑／↓ 接入已有配置体系，逐个循环已选 Bullet 的伤害类型，一次按键一个批量撤销动作；不修改 Bell、模板、当前放置类型或原选择。文本输入和非活动／非编辑谱面不消费按键，重映射的 Alt 组合也走正常匹配和保护逻辑。
5. **E-5 — 独立回归。** 新增 [tests/bullet-placement](../tests/bullet-placement/BulletPlacementCheck.csproj) 控制台校验工程，按 csproj 单独运行，不加入生产解决方案。使用真实管理器 BAML、编辑器 XAML、WPF 路由和 Skia CPU 绘制；不显示测试窗口，不持久化测试设置。

### 6.2 实际执行结果

| 编号 | 验证层次 | 结果与覆盖 |
| --- | --- | --- |
| **T-1** | WPF Debug 构建 | `dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Debug --no-restore -v minimal` 成功。 |
| **T-2** | 放置、绑定、路由与图像回归 | **212 项断言全部通过**。覆盖模板／类型互斥、行多选与虚拟化、过滤隐藏、重复激活、点击／拖动单次提交、取消后晚到释放、滚动／缩放／吸附变化、时长缩短与恢复、模板移除、谱面替换、批量模式接管、旧 Drop、绘图区原鼠标右键取消、`Grid_Drop` 取消、激活编辑器切换、伤害混选及固定结果的 Undo／Redo；真实视图路由覆盖普通改键、Alt 改键、TextBox／RichTextBox／PasswordBox／可编辑 ComboBox。 |
| **T-3** | 预览图像对照 | 对 Circle／Needle／Square 各 Normal／Large、Bell，以及 1.75 倍贴图缩放进行实际光栅图像比较：预览与独立正常物件的类型、尺寸、锚点、位置一致，只降低透明度。验证移出、取消、切换编辑器后无旧像素，释放 helper 不破坏共享贴图；已查看汇总图。包含在 T-2 的断言总数内。 |
| **T-4** | 既有 MCP 全套 | `node tests/mcp-e2e/run.mjs --url http://127.0.0.1:39282/mcp`：**1228/1228** 检查通过，涵盖物件、连接物件、Hold、动作事务、子弹模板、元数据、脚本和文档生命周期。 |
| **T-5** | 真实编辑器冒烟 | 仓库自建 WPF 实例在后台最小化运行，只打开 `.tmp` 内带音频的完整谱面副本。通过公开手势入口验证预览不写谱面、预览与落点坐标一致、放置后会话持续；启动加载临时 F13 映射并经真实视图 KeyDown 路由将 Normal 改为 Hard，MCP 查询确认 Undo 回到 Normal、Redo 回到 Hard；实际刷新渲染宿主后捕获并查看了含半透明预览的编辑器画面。该轮之后 B-1/B-5 已改为原有鼠标与 Drop 路径，窗口右键路径不再属于当前实现。 |

**T-6 — 覆盖边界。** T-2 的输入事件是合成 WPF 路由，T-5 的放置使用公开手势入口；两者不是物理鼠标键盘端到端操作。未执行物理输入、原生 OLE 拖拽捕获和多显示器 DPI 切换。图像及活实例使用 Skia CPU，未对 OpenGL／D3D 后端逐一验收。以上边界不标记为通过，用户交互验收应重点覆盖 V-4、V-5、V-7。

**T-7 — 构建与夹具说明。** 构建仍输出 NuGet 依赖安全／兼容性及部分可空注解等警告，本轮未扩大范围升级依赖。隐藏渲染夹具没有显示顶层窗口，日志中的 DPI 监听注册失败表示不存在该窗口的 PresentationSource，不影响已执行的离屏图像比较；它不构成原生 DPI 验收证据。回归调试修正了夹具的真实 Shell 激活、有效项目数据与音频时长；时长缩短用例先滚动到视口最小高度以外，确保确实越界，而不是把合法落点错误断言为无效。

### 6.3 复现与画面证据

**T-8 — 独立回归命令。** 从仓库根执行。先按 T-1 构建生产工程；以下命令只构建并运行校验工程，使用已生成的 Debug 依赖，避免活实例占用输出目录时重建生产工程。

```powershell
dotnet run --project tests/bullet-placement/BulletPlacementCheck.csproj -c Debug -p:DisableFody=true -p:BuildProjectReferences=false -- F:/Source/OngekiFumenEditor
```

**T-9 — 本地画面证据。** 生成于忽略目录 `tests/mcp-e2e/.tmp/pr45/`，不作为产品资源或测试基准图片签入。前两张可由独立回归重新生成，第三张来自本轮后台真实实例。

| 编号 | 文件 | 内容 |
| --- | --- | --- |
| **T-9.1** | [manager-placement.png](../tests/mcp-e2e/.tmp/pr45/manager-placement.png) | 真实管理器视图：唯一勾选、原工具栏及右侧创建／克隆入口。 |
| **T-9.2** | [rendering-contact-sheet.png](../tests/mcp-e2e/.tmp/pr45/rendering-contact-sheet.png) | 半透明候选与正常物件的九组图像对照。 |
| **T-9.3** | [live-editor-preview.png](../tests/mcp-e2e/.tmp/pr45/live-editor-preview.png) | 真实 WPF 编辑器渲染宿主中的鼠标候选预览。 |

### 6.4 交付状态

**E-6 — 交付状态。** 当前实现已按用户要求以 `48c08772`（提交消息含 `WIP`）签入为检查点；随后把输入接线从视图迁入 ViewModel，并将 `FumenVisualEditorView.xaml.cs` 恢复到该检查点之前的原版，这部分解耦改动尚未提交、未推送，等待用户代码验收。B-1 已改为绘图区原鼠标事件取消，B-5 改为原有 `Grid_Drop` 取消，B-2 改为 `IEditorDocumentManager.OnActivateEditorChanged`；解耦后重新构建成功，独立回归 **212 项**再次全部通过。自建后台编辑器已停止，临时运行配置和 F13 改键已还原；一次性探针与启动脚本清理，不触碰用户桌面部署实例。Avalonia 功能代码本轮未处理，仅按约定回写迁移记录，**不改变同步点 `8b2940785`**。

