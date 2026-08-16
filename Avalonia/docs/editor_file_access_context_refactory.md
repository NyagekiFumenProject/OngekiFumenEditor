```C#
/// <summary>
/// Represents a context for a fumen project workspace, containing the main project directory,
/// additional directories, and relevant main files.
/// EditorFileAccessContext is built by different platform implementations.
/// </summary>
class EditorFileAccessContext
{
    /// <summary>
    /// The main directory of the fumen project, which contains the primary resources and files for the project.
    /// </summary>
    public ISimpleDirectory ProjectDirectory { get; set; }

    /// <summary>
    /// The directories that are added to the project, which may contain additional resources (for example .svg files used/referenced by fumen file) or files related to the project.
    /// AdditionDirectories will not contain ProjectDirectory
    /// </summary>
    public List<ISimpleDirectory> AdditionDirectories { get; set; }

    public ISimpleFile ProjectFile { get; set; }
    public ISimpleFile FumenFile { get; set; }
    public ISimpleFile AudioFile { get; set; }
}


class EditorContext
{
    public EditorFileAccessContext FileAccessContext {get;set;}
}
```

运行时契约中不存在 `ProjectFileLocator`。`ProjectFile`、`FumenFile` 和 `AudioFile` 已由平台 Provider 解析、验证并作为直接文件能力放入 `EditorFileAccessContext`；`FumenVisualEditorViewModel.New(EditorContext)` / `Load(EditorContext)` 不再根据工程文件位置或路径字符串重新查找这些主文件。

`.nyagekiProj` 不再持久化 `FumenFilePath` / `AudioFilePath`。项目模型和序列化格式不保存主文件路径或定位符；加载时只允许读取 `EditorFileAccessContext.ProjectFile`、`FumenFile` 和 `AudioFile` 已绑定的文件能力。冷打开采用显式绑定：Provider 在选定工程描述符后始终要求用户确认一个谱面文件和一个音频文件，即使候选唯一也不自动关联；验证完成后才调用 Core。最近记录已经能够恢复完整文件能力时可以跳过该对话框。

## 实施状态（2026-08-17）

- D22：`ProjectFileLocator` 已从 `EditorContext`、救援元数据和 Core 加载入口删除；`EditorProjectDataUtils` 只保留基于完整 `EditorFileAccessContext` 的加载流程。
- D23：最新 `.nyagekiProj` 数据版本为 `0.5.5`，不再序列化 `FumenFilePath` / `AudioFilePath`。`0.5.2` 和 `0.5.4` 仅保留为旧文件读取契约，迁移时丢弃路径字段并在后续保存时写成 `0.5.5`。
- D24：新增项目文件绑定对话框；现有项目文件夹打开流程会扫描受支持候选，也允许通过 StorageProvider 选择外部文件。用户取消或任一角色绑定失败时不创建编辑器上下文，并释放尚未转交的文件能力。
- ACB 外置 AWB 由 Provider 在构造上下文时补齐：能够取得明确的同级 AWB 时直接绑定，否则要求用户显式选择。
- 当前共享 Provider 的文件夹入口已经采用上述规则；Desktop 直接选择单个 `.nyagekiProj` 的平台专用入口尚未单独暴露，实现后必须复用同一显式绑定流程。
- 实现提交为 `0667b2d9`。验证结果：449 项测试通过，完整解决方案构建通过，`ENABLE_CROSS_PLATFORM_FAST_OPEN` 条件构建通过。

## 原始计划（历史，已由实时审核 D1-D24 修订）
1. 移植好的FumenVisualEditorProvider内容拆分不需要了，改成不同平台实现不同的IEditorProvider，比如DefaultBrowserEditorProvider和DefaultDesktopBrowserEditorProvider,后面简称后者新实现为新provider，前者老实现为老provider。不同平台的provider有不同的表现和平台特色支持
2. FumenVisualEditorViewModel不再实现New()/Load()/Load(recordInfo)。改回由新provider实现具体前者几个方法的具体业务逻辑
3. FumenVisualEditorViewModel将实现New(EditorContext)/Load(EditorContext), 通过EditorContext对象和它的EditorFileAccessContext对象，获取对应的文件读写对象去进行加载和初始化
3. EditorFileAccessContext和RecentRecordInfo能相互转换，后者转换成前者需要判断是否都保有权限能读写
4. SVG相关内容和支持暂不考虑，全部忽略

Browser的EditorProvider具体表现:
1. 支持新建操作，可以参考老Provider对应实现，给出一个Setup对话框要求用户通过AvaloniaStorageAPI分别提供项目文件夹路径，以及音频文件路径, 以及可选的已有谱面文件路径,但这些文件如果不在项目文件夹里面，就会复制到项目文件夹的autoImport/*.*内并重定向引用它们， 编辑器保存时如果没有指定谱面文件保存路径，那么就会跳出一个对话框让用户选择谱面保存路径，并检查这个路径在不在项目文件夹之内，不在就报错让用户重新选择。由此这些参数可以构造出EditorFileAccessContext以及EditorContext
2. 支持加载谱面文件夹，跟旧provider一样的实现

Desktop的EditorProvider具体表现:
1. 支持新建操作，跟Browser的EditorProvider一样，暂时可以共用一套逻辑
2. 支持通过项目文件.nyagekiProj打开编辑器。项目文件不再记录音频和谱面路径；选定项目文件后必须显示显式绑定对话框，由用户确认音频文件和谱面文件，再构造EditorFileAccessContext以及EditorContext。不得按唯一候选、同名、修改时间或目录顺序自动绑定。当前文件夹打开入口已实现该流程，Desktop直接选择单个项目文件的入口仍待平台Provider拆分时接入
3. 支持原项目的FastOpen快速打开操作，即通过原项目对应相关流程，构造出EditorFileAccessContext以及EditorContext
