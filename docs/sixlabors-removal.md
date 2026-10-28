# 移除 SixLabors：WPF 字体枚举与图片处理替换（2026-10-25）

## 结论

WPF 树（`OngekiFumenEditor/`）不再引用 `SixLabors.Fonts 2.1.3` 与 `SixLabors.ImageSharp 3.1.12`。
字体家族枚举改用 SkiaSharp 的 `SKFontManager.Default.FontFamilies`；图片解码/拉伸/垂直翻转/PNG 编码改用自研 `Utils/ImageUtils`
（同样基于项目已有的 SkiaSharp `3.119.2`）。删包后 `project.assets.json`、`dotnet list package`、Release 产物均无 SixLabors 条目。

这也消除了 ImageSharp 3.1.12 的 Moderate 漏洞（GHSA-gwg2-r3hj-4w44）——此前因 4.x 的 Six Labors Split License
要求发布时提供 license key，无法直接升级（见 `Avalonia/WPF-MIGRATION-SYNC.md` 2026-10-25「NuGet 依赖审计与升级」）。

## 原使用点与替换

| 使用点 | 原 API | 用途 | 替换 |
| --- | --- | --- | --- |
| `Kernel/Graphics/Skia/Drawing/StringDrawing/DefaultSkiaStringDrawing.cs` | `SystemFonts.Collection.Families`、`FontFamily.TryGetPaths` | 枚举系统字体家族名与 `.ttf` 路径，供 `IFontHandle.FamilyName` 使用 | `SystemFontFamilyProvider.GetFamilyNames()`；删除 `FontHandle.FilePath`（Skia 侧从未消费） |
| `Kernel/SettingPages/Program/ViewModels/ProgramSettingViewModel.cs` | `SystemFonts.Families` | 设置页字体下拉框的家族名列表 | 同一 provider；后续去重/排序/当前值补齐逻辑不变 |
| `Modules/OptionGeneratorTools/Kernel/JacketGenerateWrapper.cs` | `Image.Load<Rgba32>`、`Mutate(Resize/Flip)`、`DangerousTryGetSinglePixelMemory` | 加载封面图、拉伸到目标尺寸、垂直翻转、取连续 RGBA32 交给 `TextureEncoderDecoder.Encode` | `ImageUtils.LoadRgba32(path, w, h)` + `FlipVertical`；失败直接抛异常，由既有 `catch` 转成 `GenerateResult(false, ...)` |
| `Utils/ImageLoader.cs` | `Image.LoadPixelData<Rgba32>`、`Mutate(Flip)`、`SaveAsPng` | 把 `.ab` 解出的纹理像素转成 PNG 字节流（供 `BitmapImage` 消费） | `ImageUtils.FlipVertical` + `EncodePng` |
| `Modules/OptionGeneratorTools/ValueConverters/LoadCardImageConverter.cs` | 同上 | 同上，供卡片图片显示 | 同上；`BitmapImage` 增加 `CacheOption = OnLoad` 后在 `Task.Run` 内 `Freeze` |

## 为什么用 SkiaSharp 而不是 WPF 字体枚举或其它库

1. **与渲染侧同源**：`DefaultSkiaStringDrawing.GetTypeface` 最终调用 `SKTypeface.FromFamilyName`。家族名直接取自 Skia 自己的字体管理器，
   避免 SixLabors 解析出的显示名与 Skia 实际可解析名不一致（别名/本地化名差异）。
2. **无线程/Dispatcher 约束**：`SKFontManager` 可在任意线程枚举；不依赖 WPF `Fonts.SystemFontFamilies` 的 UI 线程亲和性。
3. **无新增依赖**：SkiaSharp 已是项目直接依赖（`3.119.2`），`ImageUtils` 也只用到 Skia 的解码、`SKBitmap.ScalePixels`、
   `SKBitmap.InstallPixels` + `SKImage.Encode`，没有引入新包。

`SystemFontFamilyProvider` 的契约：首次调用枚举并缓存（`Lazy`，`ExecutionAndPublication`）；去空白、按
`OrdinalIgnoreCase` 去重与升序；枚举失败返回空数组并只记一条 warn。

## 像素语义

- 统一为**未预乘 RGBA8888**，与 Unity 纹理数据字节序一致（`SKAlphaType.Unpremul`）。
- `LoadRgba32` 的拉伸是 Stretch（忽略宽高比），采样器 `SKCubicResampler.Mitchell`；仅指定单边尺寸会抛 `ArgumentException`，
  非正尺寸抛 `ArgumentOutOfRangeException`，解码失败抛 `InvalidDataException`。
- `SKBitmap.Decode` 按内容识别格式（不依赖扩展名），且不自动应用 EXIF 方向——与原 `Image.Load` 默认行为一致。
- `FlipVertical` 是纯行交换（原地、无分配额外整图缓冲）。
- `EncodePng` 用 `InstallPixels` 包裹调用方缓冲后编码，不复制整图；PNG 保留 alpha。

**方向约定**：AB 纹理第 0 行是图像底部。`JacketGenerateWrapper` 在编码前翻转一次（把正常图片转成纹理方向），
`ImageLoader` / `LoadCardImageConverter` 在解码后再翻转一次（把纹理转回视觉方向）。两端行为与替换前的 ImageSharp 实现一致，
由 `tests/sixlabors-removal` 的 `CheckJacketGenerateRoundTrip` / `CheckImageLoaderLoadsAb` 固定。

## `TexturePlugin.dll` 与 ImageSharp

`OngekiFumenEditor/Dependencies/JacketGenerator/TexturePlugin.dll` 的 `TextureImportExport` 类型引用 ImageSharp，
但主工程只调用同一程序集的 `TextureEncoderDecoder.Encode/Decode`——它们只依赖 `AssetsTools.NET`、`textoolwrap` P/Invoke
与托管 DXT/BC7 解码器，不引用 ImageSharp。删包后 CLR 不会 eager 加载 ImageSharp；只要继续不调用 `TextureImportExport`，
jacket 生成/读取路径不受影响。

这条假设由测试进程硬验证：`tests/sixlabors-removal` 完整跑通 `Generate` → `GetMainImageDataAsync` → `ImageLoader` →
`LoadCardImageConverter`（过程中 `TexturePlugin.dll`、`TexToolWrap.dll`、`libSkiaSharp.dll` 均已加载），随后
`AppDomain.CurrentDomain.GetAssemblies()` 中不存在 `SixLabors.*` 程序集。若未来需要自研图片导入/导出，必须实现替代方案，
不得重新引入 SixLabors 包。

## 如何复跑

```bash
# 主工程构建（0 error）
dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Release

# 移除检查（退出码 0 = 全部通过；临时目录在 %TEMP%\sixlabors-removal-check 下并自动清理）
dotnet run --project tests/sixlabors-removal/SixLaborsRemovalCheck.csproj -c Release

# 依赖核验（输出应无 SixLabors 行）
dotnet list OngekiFumenEditor/OngekiFumenEditor.csproj package
```

`tests/sixlabors-removal` 不入 `OngekiFumenEditor.sln`，按 csproj 单独调用。

## 检查覆盖（47 项，全部通过）

1. `SystemFontFamilyProvider` 在 worker 线程首次初始化；结果非空、无空白项、忽略大小写去重且升序、第二次调用返回同一实例、含常见 Windows 字体。
2. `ImageUtils.EncodePng` 输出 PNG 签名；WPF `PngBitmapDecoder` + `FormatConvertedBitmap(Bgra32)` 解码后 2x2 像素逐通道一致（含 alpha=128）。
3. `ImageUtils.FlipVertical`：4x3 行交换、1x1 无变化、3x3 中间行不变。
4. `LoadRgba32`：原尺寸与 4x6 拉伸均为纯红（无颜色偏差）；半透明 PNG 的 alpha 与未预乘红色保留；JPEG（无 alpha）可转换。
5. `LoadRgba32` 错误路径：不存在文件、非图片内容、宽高未配对、非正尺寸分别抛对应异常。
6. `JacketGenerateWrapper.Generate` 端到端：生成主/小封面 AB；主图 8x8、RGBA 长度正确；纹理第 0 行偏蓝、第 7 行偏红（翻转正确）。
7. `ImageLoader.LoadImage` 对生成的 AB 输出 8x8 PNG；输出图像第 0 行偏红、第 7 行偏蓝（再翻转回视觉方向）。
8. `LoadCardImageConverter.Convert` 在 10 秒内产出 8x8 `BitmapImage`。
9. 主程序集引用列表与测试进程已加载程序集中都没有 `SixLabors.*`。

## 最终验证记录（2026-10-25）

- `dotnet build OngekiFumenEditor/OngekiFumenEditor.csproj -c Release`：0 error（清理 `obj/Release`、`bin/Release` 后重建）。
- `dotnet run --project tests/sixlabors-removal/SixLaborsRemovalCheck.csproj -c Release`：`通过 47 项 SixLabors 移除检查。`，退出码 0。
- `dotnet list ... package`：无 SixLabors；`OngekiFumenEditor/obj/project.assets.json` 中 `SixLabors` 计数 0；
  `bin/Release/net10.0-windows/` 下无 `SixLabors.*.dll`。
- 真实启动冒烟（自构建 Release 实例，`Start-Process -WindowStyle Minimized` + 立即 `ShowWindow(SW_MINIMIZE)`，
  仅按 `F:\Source\OngekiFumenEditor\*` 路径操作）：窗口出现、6 秒后进程仍响应（标题 `Ongeki Fumen Editor`）、随后强制停止；
  覆盖 `DefaultSkiaStringDrawing` 静态初始化（`SystemFontFamilyProvider`）、设置页字体枚举与 Skia 渲染初始化路径。

## 未处理

- `Avalonia/` 树仍使用 SixLabors，本次未改动，不改变同步点 `8b2940785`。
- `TexturePlugin.dll` 内部的 ImageSharp 引用（无源码，不可修改）；主工程不调用相关入口，运行时不会加载。
