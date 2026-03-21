# KeyBindingDefinition 默认快捷键说明

本文整理了项目中通过 `KeyBindingDefinition` 声明的默认快捷键及其用途。

- 统计范围：仅包含 `new KeyBindingDefinition(...)` 定义的快捷键
- 不包含：`CommandKeyboardShortcut` 一类命令快捷键
- 主要来源：
  - `Modules/FumenVisualEditor/KeyBindingDefinitions.cs`
  - `Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml`
  - `Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs`
  - `Modules/FumenVisualEditor/Behaviors/BatchMode/*`

## 层级说明

- `全局`：只要编辑器上下文可用，就不受通常模式 / 批量模式限制。
- `通常模式`：主要用于谱面可视编辑器的普通编辑状态。
- `批量模式`：只有切换到批量模式后才生效。

## 备注

- 下文列出的都是默认键位，实际运行时可在设置页里修改。
- `D1` 到 `D6` 指键盘主键区数字键，不是小键盘数字键。
- `OemTilde` 对应常见键盘上的 `` ` / ~ `` 键。

## 普通模式 / 全局快捷键

| 定义字段 | 默认快捷键 | 层级 | 功能 |
| --- | --- | --- | --- |
| `KBD_ChangeDockableLaneType` | `S` | `通常模式` | 更改单个已选可附轨道节点的轨道类型。支持 `Left -> Center -> Right -> Left` 循环切换，以及 `WallLeft <-> WallRight` 互切，并同步迁移子节点与依附物件。 |
| `KBD_FastSetObjectIsCritical` | `C` | `通常模式` | 默认用于快速切换已选可 `Critical` 物件的 `Critical` 属性。若属性面板中只选中了一个轨道子节点或曲线控制对象，则会在当前鼠标位置为该轨道添加曲线控制点。 |
| `KBD_FastPlaceDockableObjectToWallLeft` | `OemTilde (~)` | `通常模式` | 将当前选中的单个可附物件重新挂到同拍的 `WallLeft` 轨道上，并自动修正物件位置。 |
| `KBD_FastPlaceDockableObjectToWallRight` | `D4` | `通常模式` | 将当前选中的单个可附物件重新挂到同拍的 `WallRight` 轨道上，并自动修正物件位置。 |
| `KBD_FastPlaceDockableObjectToRight` | `D3` | `通常模式` | 将当前选中的单个可附物件重新挂到同拍的 `Right` 轨道上，并自动修正物件位置。 |
| `KBD_FastPlaceNewHold` | `H` | `通常模式` | 在当前鼠标位置快速放置一个新的 `Hold`。 |
| `KBD_FastPlaceNewTap` | `T` | `通常模式` | 在当前鼠标位置快速放置一个新的 `Tap`。 |
| `KBD_FastPlaceDockableObjectToCenter` | `D2` | `通常模式` | 将当前选中的单个可附物件重新挂到同拍的 `Center` 轨道上，并自动修正物件位置。 |
| `KBD_FastPlaceDockableObjectToLeft` | `D1` | `通常模式` | 将当前选中的单个可附物件重新挂到同拍的 `Left` 轨道上，并自动修正物件位置。 |
| `KBD_DeleteSelectingObjects` | `Delete` | `全局` | 删除当前已选物件。 |
| `KBD_SelectAllObjects` | `Ctrl + A` | `通常模式` | 选中当前谱面中所有可显示物件。 |
| `KBD_CancelSelectingObjects` | `Escape` | `通常模式` | 取消当前选择。 |
| `KBD_HideOrShow` | `Q` | `通常模式` | 在预览模式和编辑模式之间切换。 |
| `KBD_ToggleBatchMode` | `Alt + B` | `全局` | 打开或关闭批量模式。 |
| `KBD_FastAddConnectableChild` | `A` | `通常模式` | 若只选中一个 `Hold`，则在鼠标位置为其添加 `HoldEnd`。若选中的是若干轨道节点，则按当前鼠标位置批量补出对应的下一段子节点，并保持原有横向相对位置。 |
| `KBD_FastSwitchFlickDirection` | `F` | `通常模式` | 切换当前所选 `Flick` 的方向。 |
| `KBD_CopySelectedObjects` | `Ctrl + C` | `全局` | 复制当前选中的可复制物件到编辑器剪贴板。 |
| `KBD_PasteCopiesObjects` | `Ctrl + V` | `全局` | 将已复制物件粘贴到当前鼠标位置。 |
| `KBD_ScrollPageDown` | `PageDown` | `全局` | 编辑器向下翻一页。 |
| `KBD_ScrollPageUp` | `PageUp` | `全局` | 编辑器向上翻一页。 |

## 批量模式快捷键

进入批量模式后，下列按键的含义不再是“立即执行一次动作”，而是“切换当前批量刷入 / 过滤子模式”。

- 左键：按当前子模式执行批量放置或过滤选择
- 右键：按当前子模式删除同类物件，或按过滤规则框选删除
- `Alt`：鼠标操作时临时退回普通点击逻辑
- 子模式切换同时支持 `快捷键` 和 `Shift + 快捷键`

| 定义字段 | 默认快捷键 | 子模式作用 | 附加修饰行为 |
| --- | --- | --- | --- |
| `KBD_Batch_ModeWallLeft` | `OemTilde (~)` | 切换到 `WallLeft` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeLaneLeft` | `D1` | 切换到 `Left` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeLaneCenter` | `D2` | 切换到 `Center` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeLaneRight` | `D3` | 切换到 `Right` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeWallRight` | `D4` | 切换到 `WallRight` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeLaneColorful` | `D5` | 切换到 `Colorful` 轨道刷入模式。 | `Shift`：添加后保留已有选择，并将新轨道一并选中。 |
| `KBD_Batch_ModeTap` | `T` | 切换到 `Tap` 刷入模式。 | `Ctrl`：刷入时直接把物件设为 `Critical`。 |
| `KBD_Batch_ModeHold` | `H` | 切换到 `Hold` 刷入模式。 | `Ctrl`：刷入时直接把物件设为 `Critical`。`Shift`：添加后保留已有选择，并将新物件一并选中。 |
| `KBD_Batch_ModeFlick` | `F` | 切换到 `Flick` 刷入模式。 | `Ctrl`：刷入时直接把物件设为 `Critical`。`Shift`：刷入时把方向设为右。 |
| `KBD_Batch_ModeLaneBlock` | `Z` | 切换到 `LaneBlock` 刷入模式。 | `Ctrl`：刷入时把方向设为右。 |
| `KBD_Batch_ModeNormalBell` | `E` | 切换到 `Bell` 刷入模式。 | 无。 |
| `KBD_Batch_ModeClipboard` | `V` | 切换到“从剪贴板刷入”模式。 | 无。当前仅支持刷入单个已复制物件；剪贴板为空或内容不适合批量刷入时会提示。 |
| `KBD_Batch_ModeFilterLanes` | `D6` | 切换到“只过滤 / 选取轨道”模式。 | 无。用于只对轨道类物件做选择或删除。 |
| `KBD_Batch_ModeFilterDockableObjects` | `Y` | 切换到“只过滤 / 选取可附物件”模式。 | 无。过滤范围为 `Tap`、`Hold`、`HoldEnd`。 |
| `KBD_Batch_ModeFilterFloatingObjects` | `U` | 切换到“只过滤 / 选取悬浮物件”模式。 | 无。过滤范围为 `Bell`、`Bullet`、`Flick`。 |

## 代码定位

- 定义入口：`OngekiFumenEditor/Modules/FumenVisualEditor/KeyBindingDefinitions.cs`
- 触发绑定：`OngekiFumenEditor/Modules/FumenVisualEditor/Views/FumenVisualEditorView.xaml`
- 普通模式动作实现：`OngekiFumenEditor/Modules/FumenVisualEditor/ViewModels/FumenVisualEditorViewModel.UserInteractionActions.cs`
- 批量模式实现：`OngekiFumenEditor/Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeBehavior.cs`
- 批量模式子模式定义：`OngekiFumenEditor/Modules/FumenVisualEditor/Behaviors/BatchMode/BatchModeSubmode.cs`
