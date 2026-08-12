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

计划
1. 移植好的FumenVisualEditorProvider内容拆分不需要了，改成不同平台实现不同的IEditorProvider，比如DefaultBrowserEditorProvider和DefaultDesktopBrowserEditorProvider,后面简称后者新实现为新provider，前者老实现为老provider。不同平台的provider有不同的表现和平台特色支持
2. FumenVisualEditorViewModel不再实现New()/Load()/Load(recordInfo)。改回由新provider实现具体前者几个方法的具体业务逻辑
3. FumenVisualEditorViewModel将实现New(EditorContext)/Load(EditorContext), 通过EditorContext对象和它的EditorFileAccessContext对象，获取对应的文件读写对象去进行加载和初始化
3. EditorFileAccessContext和RecentRecordInfo能相互转换，后者转换成前者需要判断是否都保有权限能读写

Browser的EditorProvider具体表现:
1. 支持新建操作，可以参考老Provider对应实现，给出一个Setup对话框要求用户分别提供项目文件夹路径，以及音频文件路径, 以及可选的已有谱面文件路径。由此这些参数可以构造出EditorFileAccessContext以及EditorContext
2. 支持加载谱面文件夹，跟旧provider一样的实现

Desktop的EditorProvider具体表现:
1. 支持新建操作，跟Browser的EditorProvider一样的行为
2. 支持通过项目文件.nyagekiProj打开编辑器，即按照原项目逻辑得到音频文件谱面文件和nyagekiProj文件以及所在的文件夹，构造出EditorFileAccessContext以及EditorContext
3. 支持原项目的FastOpen快速打开操作，即通过原项目对应相关流程，构造出EditorFileAccessContext以及EditorContext
