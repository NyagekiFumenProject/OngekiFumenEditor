# 文本渲染清晰度分析（OpenGL 与 Skia 后端不一致）

> 文档性质：**问题分析 + 方案评估（分析完成；工作区 1 处未提交修复；替代方案已联网复核，候选见 §3，推荐见 §4）**
> 建立日期：2026-09-29（§3.1 联网检索与 §2.2 源码级根因同日补充）
> 目标工程：`OngekiFumenEditor`（WPF，`net10.0-windows`）
> 后端范围：`Kernel.Graphics.OpenGL`（FontStashSharp 文字路径）与 `Kernel.Graphics.Skia`（`SKFont` 文字路径）对比
> 来源：用户反馈「OpenGL 下字体渲染不清晰、Skia 正常清晰」+ 两张同区域对比截图
> 依赖版本（实测于本仓 `.nuget-packages` / `obj`）：
> `FontStashSharp.PlatformAgnostic 1.5.1`、`FontStashSharp.Rasterizers.FreeType 1.2.2`、`FontStashSharp.Rasterizers.StbTrueTypeSharp 1.2.2`；
> `SkiaSharp 3.119.2`；`SixLabors.Fonts 2.1.3`；`SixLabors.ImageSharp 3.1.12`；`FreeTypeSharp`（在本地 feed）
> 复现工位（临时目录，未改动本仓库文件）：`%TEMP%\ongeki-offscreen-verify`（`OffscreenVerify.csproj`）。
> 模式：`textskia` = Skia 文字样本；`gl` = GL 文字样本（另含图块烘焙像素对比）。
> 注：本机外网已于 2026-09-29 打通（本地 Clash + 终端注入），因此 §3.1 补做了线上检索与包版本核查。

## 0. 结论摘要

| 编号 | 结论 | 状态 |
| --- | --- | --- |
| **A** | 两个后端解析到的**根本不是同一个字体**：GL 用 `consola.ttf`（Consolas），Skia 静默回退到系统默认字体 | **已修（未提交）**，见 §0.2 |
| **B** | GL 的清晰度损失来自**「按 `字号 × FontResolutionFactor` 光栅化 + 双线性缩回」**：hinting 本身有（FreeType 默认开启），但被 2× 超采样抹掉；且 1.5.1 的度量按 `字号 × factor²` 查询，导致「只改 factor」必然错位 | **已定位到源码行**，见 §2.2；只能替换文字实现，见 §3/§4 |
| **C** | 「换 FontStashSharp 的光栅化器/参数」这条路**走不通**：FreeType 已是其上限，stb 更差；mipmap 与 `FontResolutionFactor` 两个方向实测要么无效、要么破坏排版 | 已排除，见 §2.2/表 3（本轮补测，结论强化） |
| **D** | 线上复核：无「免密钥即得 hinting 的 .NET 文字库」捷径；可复用 FontStashSharp 的**自定义光栅化源**（`IFontLoader`/`IFontSource`，官方支持）是唯一能保留现有图集/排版管线的路线 | 见 §3.1 |

## 0.1 实测数据（可复现）

### 表 1 — 字体是否一致（同字号 15，文本 `T[0,0]`，指标为墨迹包围盒）

| 样本 | 墨迹尺寸 | 说明 |
| --- | --- | --- |
| GL（修前＝修后，GL 侧未改动） | `47x14` | `consola.ttf` → Consolas |
| Skia（修前） | `36x13` | `DefaultFont == null` → `SKTypeface.Default` 系统默认字体 |
| Skia（修后） | `48x13` | 与 GL 同一字体文件 |

同一字号下墨迹宽度差 30%，只能是不同字体；修后两者一致。

### 表 2 — 同一字体下的清晰度对比（字号 15，`textskia` / `gl` 样本；2026-09-29 复测）

| 后端 | 墨迹 | 实心像素 `solid` | 半透明像素 `soft` | `softRatio = soft/(solid+soft)` |
| --- | --- | --- | --- | --- |
| Skia（`SKFont`，`Hinting=Full`，`Edging=SubpixelAntialias`，**按目标字号光栅化**） | `48x13` @ (20,0) | **101** | 159 | 0.612 |
| OpenGL（FontStashSharp，`FontResolutionFactor=2`，图集 `Linear`） | `47x14` @ (21,24) | **58** | 101 | 0.635 |

同样的字形、同样的字号，GL 的实心像素只有 Skia 的 57%：笔画被摊到半透明像素上，观感即「发虚」。
（顺带：`gl` 与 `textskia` 的 24pt 样本都会被 300x48 的离屏区域裁切——GL 侧渲染位置更靠下所以未裁，Skia 侧顶部被裁。要比较 24pt 需另开更大的目标区域。）

### 表 3 — 针对 GL 侧做过的四项实验

| 实验 | 改动 | 结果 | 结论 |
| --- | --- | --- | --- |
| 关闭模糊核 | `KernelWidth/Height: 1 → 0` | 指标**完全不变**（`58/101/0.635`） | 该参数对当前 glyph renderer 无效 |
| 图集走 mipmap | `MinFilter: Linear → LinearMipmapLinear` | 文字**整体消失**（`no ink`） | 图集 mipmap 在写入字形**之前**生成（`Texture.cs:49`），而 `SetData`（`Texture.cs:67`）不重建 mip；此路不可行 |
| 去掉 2× 超采样 | `FontResolutionFactor: 2 → 1` | **比原先记录的更差**：墨迹 `47x14 → 43x9`（字形变小）、`solid 58 → 21`、`softRatio 0.635 → 0.804`、整体下移 15px（墨迹 y `24 → 39`）；24pt 样本被裁到只剩 1 行 | 该参数同时决定「光栅化尺寸 / 度量尺寸 / 绘制缩放」三者，单独改会让三者互相错配。**先前记录的"变锐"是误判**，本行为 2026-09-29 复测数据 |
| 换光栅化器 | 去掉 `FreeTypeLoader`（回落到内置 stb） | `solid 58 → 35`、`softRatio 0.635 → 0.803` | **更差**；FreeType 已是 FontStashSharp 的最佳配置 |

### 表 4 — 各文字的 API 能力（DLL 探针 + 线上源码核对）

| 程序集 | 暴露的渲染相关 API | hinting |
| --- | --- | --- |
| `FontStashSharp.PlatformAgnostic 1.5.1` | `IFontLoader / IFontSource / RasterizeGlyphBitmap / GetMetricsForSize / KernelWidth / Antialiasing / GlyphRenderer / PremultiplyAlpha` | ✗（管线层面抹掉，见 §2.2） |
| `FontStashSharp.Rasterizers.FreeType 1.2.2` | `FreeTypeLoader / FreeTypeSource`；`FT_Load_Glyph(..., FT_LOAD_DEFAULT \| FT_LOAD_COLOR)` | ✓（**光栅化器本来就在做**） |
| `FontStashSharp.Rasterizers.StbTrueTypeSharp 1.2.2` | `KernelWidth / Rasterize` | ✗（官方文档明示 "Does not perform TrueType hinting"） |
| `FontStashSharp.Rasterizers.SharpAstro 1.2.9`（**新增候选**） | `SharpAstroLoader / SharpAstroSource`，纯托管、无原生依赖，要求 .NET 10+ | ✗（文档未列 hinting；仅作"去原生依赖"的现代化选项） |
| `SkiaSharp 3.119.2`（最新稳定 4.152.x） | `SKFont.Hinting / Edging / Subpixel / Sdf` | ✓ |
| `SixLabors.Fonts 2.1.3`（最新稳定 3.1.3） | `HintingMode / ClearType / Hinting` | ✓（作用于轮廓，需自备光栅化） |
| `StbTrueTypeSharp` | `Subpixel` | 部分 |

## 0.2 已落地的改动

1. **Skia 默认字体修复：按用户决定已回退**（`DefaultSkiaStringDrawing.cs` 恢复为按 `FamilyName` 匹配 `"consola"`）。
   即 Skia 后端继续使用系统默认字体，两后端的字体差异（根因 A）不再处理。
2. **GL 文字实现替换为 R2（SkiaSharp 字形图集）——已实现并验证**，见 §0.3。

## 0.3 实施记录（R2）

| 变更 | 说明 |
| --- | --- |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/SkiaGlyphAtlas.cs`（新增） | SkiaSharp 在「最终像素尺寸」上带 `Hinting=Full` 光栅化字形（`Edging` 跟随 `DisableStringRendererAntialiasing`），刘海式打包进 1024² RGBA 图集；图集为「白色 RGB + 覆盖率写在 A」，与 `texture(diffuse,uv) * color` 的既有约定天然相容，无需 premultiplied 处理。装满了整体重建。 |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/SkiaGlyphRenderer.cs`（新增） | 单批次动态顶点缓冲（pos.xy + uv + color.rgba），整串文字一次 `DrawArrays`；着色器与 `CommonSpriteShader` 同色约定。 |
| `DefaultStringMeasure.cs`（改写） | 去掉 FontStashSharp/FreeType；保留字体表、`FontStyle`→子字体解析（b/i/z 后缀）、度量缓存的对外契约；新增按像素尺寸建 `SKFont` 与图集持有。 |
| `DefaultStringDrawing.cs`（改写） | 排版数学与 Skia 后端对齐（同一 `MeasureText` + origin/scale/rotate 语义）；1:1 绘制时把字形吸附到整数像素（hinting 只有落在像素网格上才锐利）；下划线/删除线用图集里的 1×1 实心条目绘制。 |
| `String/`（删除 7 个文件） | 旧 FontStashSharp 的 GL 适配层（`Renderer.cs`/`Texture.cs`/`Texture2DManager.cs`/`Shader`/`VAO`/`VBO`/`readme.txt`）已无引用，整体删除。FontStashSharp 包保留（`AudioPlayerToolViewer` 的 RichText、`CommonHorizonalDrawingTarget` 仍在用）。 |

### 实测对比（文本 `T[0,0]`，300×48 离屏目标，2026-09-29 同机复测）

| 样本 | 旧（FontStashSharp，factor=2） | 新（Skia 字形图集 + 像素吸附） |
| --- | --- | --- |
| 15px `origin(0,2)` | `solid=58`, `softRatio=0.635` | `solid=108`, `softRatio=0.440` |
| 16px `origin(0,0.5)` | `solid=97`, `softRatio=0.336` | `solid=119`, `softRatio=0.498` |
| 15px `origin(0.5,0.5)` | `solid=43`, `softRatio=0.753` | `solid=115`, `softRatio=0.431` |

- 实心像素普遍提升 1.9–2.7 倍；更关键的是**旧实现"清晰度随定位相位剧烈波动"（0.336 ↔ 0.753）消失了**，新实现各组合稳定在 0.43–0.50。
- 排版：新实现与 Skia 后端完全一致；与旧 GL 实现在编辑器常见 origin（`(0,0.5)`、`(0.5,0.5)`）下墨迹位置相差 ≤1px，仅在旧样本用的 `origin.Y=2` 这类"越界 hack"下不同（那种写法原本依赖 §2.2 的度量错配）。
- 回归：`gl` 套件 30/30（含图块烘焙与逐像素对比）、`waveblocks`/`skia` 的失败与本改动无关（MEF 引导扫描撞上 `OngekiFumenEditor.Benchmark` 输出中版本不匹配的 `Microsoft.CodeAnalysis.AnalyzerUtilities.dll`，见 §5 备注）。

### 性能对比（新老实现，1920×1080 离屏，200 条编辑器风格标签/帧）

测量方式：同一工位（`gl` 模式内新增 `textperf` 段），先用「仅清屏」帧作基线，再测「清屏 + 200 条标签」帧；各配置预热 4 帧、统计 24 帧均值。老实现 = `HEAD~1` 的 FontStashSharp 路径（临时恢复后构建到独立输出目录，测完已还原），新实现 = 当前 Skia 字形图集。

| 指标 | 旧（FontStashSharp，factor=2） | 新（Skia 字形图集） |
| --- | --- | --- |
| 仅清屏基线 | 5.24 ms/帧 | 6.06 ms/帧 |
| 清屏 + 200 条标签 | 16.09 ms/帧（首帧 26.03 ms） | **15.65 ms/帧**（首帧 **22.03 ms**） |
| 净文字成本 | 10.86 ms/帧，**54.3 µs/标签** | 9.60 ms/帧，**48.0 µs/标签** |

- 净文字成本降低约 **11.6%**；冷启动首帧少 **6.4 ms（约 24%）**——老实现的冷启动同时包含 FontSystem 建立与 2× 超采样图集填充，新实现只按最终尺寸光栅化一次。
- 绘制调用数量两者相同（每条字符串一次 `DrawArrays`，老实现的 FontStashSharp `Renderer` 同样按字符串提交）。
- 说明：两种实现均在独立进程、各测 1 轮（重复轮次因上文 MEF 引导问题未能启动）；「仅清屏」基线两轮间有约 0.8 ms 的进程间波动，故**同帧绝对耗时**（16.09 → 15.65 ms）比差值更能反映结论。清晰度收益见上表（实心像素 +86%）。

## 1. 判定口径

清晰度不靠主观描述，用两个可复现的量化指标（`Program.Text.cs` 中 `DumpText`）：

- 把样本渲染到不透明底色的离屏目标上，取墨迹包围盒；
- `solid` = 亮度 > 220 的像素数；`soft` = 亮度 ∈ [40, 220] 的像素数（抗锯齿摊开的笔画都落在这里）；
- `softRatio = soft / (solid + soft)`：越高越软。同时打印同一区域的 ASCII 亮度图，便于目视比对字形形态。

## 2. 根因

### 2.1 根因 A：字体字段语义错配导致静默回退

| 侧 | 字体表来源 | `FamilyName` 字段含义 | 默认字体匹配 | 实际使用字体 |
| --- | --- | --- | --- | --- |
| GL（`DefaultStringMeasure.GetSupportFonts`） | 系统字体目录扫描 `.ttf` | **文件名**（`consola`） | 命中 | `consola.ttf`（Consolas） |
| Skia（`DefaultSkiaStringDrawing.GetSupportFonts`） | `SixLabors.Fonts.SystemFonts` | **真实家族名**（`Consolas`） | 与 `"consola"` 比对失败 → `null` | 系统默认字体 |

两侧都用 `"consola"` 作为默认字体的判据，但字段语义不同：GL 能命中，Skia 命中不了且没有任何日志，直接回退。**这是 A 的全部原因**（与光栅化质量无关）。

### 2.2 根因 B：hinting 被「2× 光栅化 + 双线性缩回」抹掉（含 1.5.1 的度量错配）

GL 文字链路：`FontStashSharp`（`DefaultStringMeasure.cs:43-73` 创建 `FontSystem`）→ 字形光栅化 → 图集（`String/Texture.cs`）→ `DefaultStringDrawing.Draw`（`:25-42`）→ FontStashSharp 的 `Renderer`（`String/Platform/Renderer.cs`）。

源码级定位（`FontStashSharp 1.5.1`，本轮联网核对）：

- `DynamicSpriteFont.cs:42`：`RenderFontSizeMultiplicator = FontSystem.FontResolutionFactor;`
- `DynamicSpriteFont.cs:104`：字形**按 `FontSize × FontResolutionFactor` 光栅化**（factor=2 ⇒ 30px 图集位图）；
- `DynamicSpriteFont.cs:117`：字形记录 `FontSize = fontSize`（即上式的 30px）；
- `DynamicSpriteFont.cs:183`：度量却按 `FontSize * RenderFontSizeMultiplicator` 查询 ⇒ factor=2 时实际查询的是 **60px** 的 ascent/descent/lineHeight（`FontSize` 已经被乘过一次 factor，这里又乘了一次 ⇒ 1.5.1 的内部缺陷）；
- `FreeTypeSource.cs:117`：`FT_Load_Glyph(..., FT_LOAD_DEFAULT | FT_LOAD_COLOR)` ⇒ **FreeType 侧 hinting 是开着的**，`GetMetricsForSize` 也是 FreeType 的真实度量；
- 结论：**不是"没有 hinting"，而是"在 30px 上做 hinting，再按 15px 双线性采样"** —— 网格吸附在缩放那一步被彻底摊平；同时度量与显示尺寸错配，使得「只把 factor 改成 1」必然引发错位（表 3 第 3 行已实测）。
- 对照：Skia 文字走 `SKFont`（`DefaultSkiaStringDrawing.cs:98-117`）：按**目标字号**光栅化 + `Hinting = Full` + `Edging = SubpixelAntialias` + `Subpixel = true`，笔画被吸附到像素网格，故实心像素多、观感锐利。

附带说明：`DefaultStringDrawing.cs:35-36` 的 `origin.X *= 2; origin *= size;`（以及某些字号下的位置偏移）实际是在为上面的度量错配做补偿，这也解释了为什么现有排版"只在 factor=2 下看起来对"。

### 2.3 代码锚点

| 位置 | 职责 | 与本文关系 |
| --- | --- | --- |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringMeasure.cs:28-31,43-73` | GL 字体表、`FontSystem` 设置、`FreeTypeLoader` | 根因 B（factor 与光栅化尺寸） |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/DefaultStringDrawing.cs:25-42` | GL 文字绘制（`font.DrawText` + `origin` 换算） | 推荐路线需替换；`:35-36` 是 factor 错配的补偿 |
| `Kernel/Graphics/OpenGL/Drawing/StringDrawing/String/Texture.cs:36-49,67` | 图集纹理与采样、mipmap 生成时机 | 表 3 的 mipmap 结论 |
| `Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs:27-34,68-79,98-117` | Skia 默认字体解析、`SKTypeface`/`SKFont` 设置 | 根因 A（已修）；推荐实现的对齐目标 |
| `Kernel/Graphics/OpenGL/DefaultOpenGLRenderManagerImpl.cs:107-112` | GL 混合状态（`BlendFuncSeparate`，见提交 `79cf276f`） | 推荐路线的 premultiplied 约定参考 |
| （第三方）`FontStashSharp 1.5.1` `DynamicSpriteFont.cs:42/104/117/183`、`FontStashSharp.Base` `IFontLoader.cs`、`Rasterizers.FreeType/FreeTypeSource.cs:117` | 光栅化尺寸、度量尺寸、加载标志 | 根因 B 的确切机制；自定义光栅化源的接口依据 |

## 3. 候选替代方案（联网复核后）

| 方案 | 本机可用性 | hinting | 需新增依赖 | 工作量 | 风险 |
| --- | --- | --- | --- | --- | --- |
| **R2 SkiaSharp 光栅化字形图集**（推荐） | ✓ 项目已引用 SkiaSharp（Skia 后端在用） | ✓ `SKFont.Hinting/Edging`，按目标像素尺寸 | 无 | 中（约 300 行：字形缓存 + 图集打包 + 绘制 + 测量） | 中：`MeasureString` 换引擎后排版需复核；premultiplied 约定需处理 |
| **R1 自定义 `IFontLoader`/`IFontSource`**（最小改动路线，新增候选） | ✓ 接口在 `FontStashSharp.Base`（已随 FreeType 光栅化器间接引用） | ✓ 由自己实现（Skia 或 FreeTypeSharp 均可） | 无 | 中（约 150–250 行：只实现 6 个方法，保留图集/打包/绘制/测量管线） | 中高：必须同时设 `FontResolutionFactor=1` 并自行定义「度量按显示尺寸」的语义，否则重蹈 §2.2 的错配；`SpriteFontBase` 的绘制缩放语义需实测确认 |
| R3 SixLabors.Fonts + ImageSharp | ✓ 项目已引用（Skia 侧字体表在用） | ✓ `HintingMode/ClearType`（作用于轮廓） | 无 | 中 | 中：hinting 只到轮廓，仍需自备光栅化/图集/排版 |
| R4 FreeTypeSharp（+ HarfBuzzSharp） | ✓ 在本仓 `.nuget-packages` feed | ✓ 原始 FreeType，`FT_LOAD_*` 自控 | 有（离线可还原） | 大 | 中：底层 API、要自己管字形/图集/排版 |
| R5 WPF 自带（`FormattedText` + `RenderTargetBitmap`） | ✓ 零依赖 | ✓（Grayscale/ClearType，与 UI 观感一致） | 无 | 中 | 中：受 UI 线程约束（需一次性烘焙字形缓存），与 GL 解耦别扭 |
| R6 维持 FontStashSharp | ✓ | ✗ | 无 | 0 | 低：观感维持现状（表 2 的差距） |
| R7 升级/更换 FontStashSharp 光栅化器 | ✓ 仅 Rasterizers 包可换（如 `SharpAstro` 1.2.9，纯托管、net10+） | ✗（官方仅 FreeType 标注 "produces hinted output"，项目已在用） | 视包而定（现已可联网拉取） | 小 | 低：**不能解决清晰度**，只在"去原生依赖"上有价值 |

### 3.1 联网检索结果（2026-09-29）

1. **官方文档确认**（`FontStashSharp` 仓库 `docfx/docs/custom-font-rasterizer.md`）：
   - 默认 `StbTrueTypeSharp`：**"Does not perform TrueType hinting"**；`FreeType`："**Produces hinted output**"；项目当前正是 FreeType ⇒ 光栅化器不是问题所在，与 §2.2 的源码结论一致。
   - 官方**支持自定义光栅化器**：实现 `IFontLoader.Load(byte[]) → IFontSource` 后赋给 `FontSystemDefaults.FontLoader` 即可 ⇒ R1 是官方路径。
2. **`IFontSource` 契约**（`FontStashSharp.Base`）：`GetMetricsForSize / GetGlyphId / GetGlyphMetrics / RasterizeGlyphBitmap / RasterizeGlyphSDF / GetGlyphKernAdvance / CalculateScaleForTextShaper`（前置加 `IDisposable`）——即**度量与位图都由实现方决定**，这正是修复 §2.2 错配所需的控制力。
3. **包版本核查**（nuget.org，本机现可访问）：`FontStashSharp.Rasterizers.FreeType` 最新 **1.2.9**（项目 1.2.2）；`SharpAstro.Fonts` **1.12.901**；`SixLabors.Fonts` **3.1.3**（项目 2.1.3）；`SkiaSharp` 4.152.x（项目用 3.119.2，属预览线）。⇒ **"不能引新包"的前提已解除**，但本轮方案仍优选零新增依赖。
4. **社区做法**：文本渲染综述与实现（LearnOpenGL 的 FreeType 章节、Wildfire Games 的 FreeType+OpenGL 字体系统）一致指出：**小字号清晰度的关键是在最终像素尺寸上做 hinting**，与本文结论一致；字体图集是标准落地方式（每 (字形, 字号) 一项）。
5. **未发现**可直接替换的"免密钥即得 hinting 的 .NET 文字/图集库"：`SharpAstro`（纯托管 OpenType 光栅化）不提供 hinting，`SixLabors.Fonts` 的 hinting 只作用于轮廓。⇒ 要清晰度，只能自己控制「按最终尺寸光栅化」这一步（R1/R2/R4）。

## 4. 推荐路线

**首选 R2（SkiaSharp 光栅化的字形图集）**，理由：

1. 与 Skia 后端**同一光栅化引擎、同一字体文件、同一组 `SKFont` 设置** ⇒ 表 2 的差距从根上消除，且两个后端今后**不会再出现同类不一致**（A 类问题结构性消失）；可以把光栅化部分抽成两个后端共用的一个小类。
2. 不新增依赖（SkiaSharp 已引用）。
3. 接口简单：GL 侧只需实现 `IStringDrawing`/`IStringMeasure`（`Draw`/`MeasureString`/`SupportFonts`），可整体替换并按提交回退。

实施要点：

1. `SKTypeface.FromFile(字体文件路径)`（字体来源与现有一致）+ `SKFont { Hinting = Full, Edging = 与设置一致 }`，**按最终像素尺寸光栅化**：`pixelSize = round(fontSize × |scale.Y|)`，绘制时吸附到 1:1（缩放不再靠拉伸字形位图）⇒ 任意缩放下都清晰，不必再造 2× 超采样。
2. 图集键 = (字形 id, 像素尺寸)；打包可复用 `String/Platform/Texture2DManager.cs` 的 GL 纹理管理；上传注意 **premultiplied 约定**（Skia 位图是 premultiplied：要么上传前 un-premultiply，要么走 `(ONE, OneMinusSrcAlpha)`；`DefaultOpenGLRenderManagerImpl.cs:107-112` 的 `BlendFuncSeparate` 已把 alpha 通道约定理顺）。
3. 暂不做复杂排版：字号为整数、文本以 ASCII/标签为主，按 `SKFont.MeasureText`/推进宽度逐字摆放即可。
4. 替换后需**整体目视复核排版**：编辑器所有标签的宽度/基线会与原先有几个像素的差异（`origin`/`bounds` 换算要对着 Skia 后端的写法对齐；旧实现里 `origin.X *= 2` 这类 factor 补偿应随之删除）。
5. 回退：保留原 `DefaultStringDrawing` 一个提交即可切回（不宜长期双实现并存）。

**备选 R1（自定义 `IFontSource`）**：若评估后希望"改动面最小、保留 FontStashSharp 的图集与排版管线"，R1 可行且有官方示例支撑；但必须同时处理 factor 语义（置 1）并把 `GetMetricsForSize` 定义为**按显示尺寸**返回，且要实测 `SpriteFontBase` 的绘制缩放行为——否则会重演 §2.2 的错配，风险高于 R2。

**R5（WPF 渲染）** 适合"希望与 WPF UI 文字完全一致"的取舍；代价是光栅化必须走 UI 线程并一次性烘焙缓存。

## 5. 复现与验证方法

```powershell
# 构建（不要占用正在运行的编辑器：输出到备用目录）
cd "$env:TEMP\ongeki-offscreen-verify"
dotnet build OffscreenVerify.csproj -p:BaseOutputPath="$env:TEMP\ongeki-alt-bin\"

# 运行（必须用 apphost，bash 直接执行 .exe 会被拒）
cd "$env:TEMP\ongeki-alt-bin\Debug\net10.0-windows"
powershell -NoProfile -Command "& '$PWD\OffscreenVerify.exe' textskia"   # Skia 文字样本
powershell -NoProfile -Command "& '$PWD\OffscreenVerify.exe' gl"        # GL 文字样本 + 图块像素对比（30 项）
powershell -NoProfile -Command "& '$PWD\OffscreenVerify.exe' waveblocks"# 波形图块（15 项）
```

判据：同一行 `text[...]` 汇总里的 `ink` / `solid` / `softRatio`（表 2 的对比方式），以及紧随其后的 ASCII 亮度图。回归基线（2026-09-29 复测）：`gl` 30/30、`waveblocks` 15/15、`textskia` 0 失败。

> 备注（与本文无关的环境问题）：`waveblocks` / `skia` / `textskia` 这些入口会走 Gemini 的 MEF 引导（`AppBootstrapper.PopulateAssemblySourceUsingAssemblyCatalog`），它会扫描并加载相关输出目录下的**所有**程序集。若 `OngekiFumenEditor.Benchmark\bin\...` 里存在与主程序 `Microsoft.CodeAnalysis.*` 版本不匹配的 `Microsoft.CodeAnalysis.AnalyzerUtilities.dll`，引导会以 `FileLoadException` 失败（`gl` 入口不经过引导，因此不受影响）。处理办法：重新构建或清理该 Benchmark 输出目录。此问题与文字渲染改动无关。

## 6. 未决问题

1. ~~是否实施 R2~~ → **已实施并验证**（§0.3）；建议在实际编辑器里目视复核一遍所有标签的排版，再提交。
2. ~~Skia 默认字体修复如何取舍~~ → **按决定已回退**（§0.2），两后端字体差异保留现状。
3. 是否把 §2.2 的 FontStashSharp 度量错配（`FontSize * RenderFontSizeMultiplicator`）反馈给上游？（只影响外部仍在用 FontStashSharp 的模块，与已替换的 GL 文字路径无关）
4. 波形面板刻度文字走的是 `AudioPlayerToolViewer` 内的 FontStashSharp RichText（不经后端文字接口），若同样觉得发虚，需要另行处理。

## 7. 非目标（本文范围外）

- 不评估 Avalonia 侧（`Avalonia/src/...`）的文字渲染。
- 不改动文字排版规则、字号、`FontStyle` 语义（加粗/斜体子字体查找等）。
- 不引入"为了替代而替代"的新依赖：联网已通、可拉新包（§3.1），但 R2/R1 均为零新增依赖方案，优先采用。
