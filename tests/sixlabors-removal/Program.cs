using OngekiFumenEditor.Kernel.Graphics.Text;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Utils;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static int passed;
    private static string workDir = "";

    private static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception("FAIL: " + name);
        passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void CheckThrows<T>(Action action, string name) where T : Exception
    {
        try
        {
            action();
        }
        catch (T e) when (e.GetType() == typeof(T))
        {
            passed++;
            Console.WriteLine("PASS: " + name);
            return;
        }
        catch (Exception e)
        {
            throw new Exception($"FAIL: {name}（实际抛出 {e.GetType().Name}: {e.Message}）");
        }
        throw new Exception($"FAIL: {name}（未抛出异常）");
    }

    [STAThread]
    private static int Main()
    {
        workDir = Path.Combine(Path.GetTempPath(), "sixlabors-removal-check", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);

        try
        {
            CheckFontFamiliesOnWorkerThread();
            CheckFontFamilies();
            CheckImagePixelRoundTrip();
            CheckFlipVertical();
            CheckLoadRgba32Resize();
            CheckLoadRgba32AlphaAndJpeg();
            CheckLoadRgba32Errors();
            var jacketAbPath = CheckJacketGenerateRoundTrip();
            CheckImageLoaderLoadsAb(jacketAbPath);
            CheckCardImageConverter(jacketAbPath);
            CheckNoSixLaborsReference();

            Console.WriteLine($"通过 {passed} 项 SixLabors 移除检查。");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(workDir, true);
            }
            catch
            {
                // 临时目录清理失败不影响检查结论。
            }
        }
    }

    private static void CheckFontFamiliesOnWorkerThread()
    {
        var names = Task.Run(() => SystemFontFamilyProvider.GetFamilyNames()).GetAwaiter().GetResult();
        Check(names.Count > 0, "worker 线程首次枚举得到非空字体家族列表");
        Check(names.All(x => !string.IsNullOrWhiteSpace(x)), "worker 线程枚举结果无空白项");
    }

    private static void CheckFontFamilies()
    {
        var names = SystemFontFamilyProvider.GetFamilyNames();
        Check(names.Count > 0, "字体家族列表非空");
        Check(names.All(x => !string.IsNullOrWhiteSpace(x)), "字体家族列表无空白项");
        Check(names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == names.Count, "字体家族列表已忽略大小写去重");
        Check(names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).SequenceEqual(names), "字体家族列表按忽略大小写升序");
        Check(ReferenceEquals(names, SystemFontFamilyProvider.GetFamilyNames()), "重复调用返回同一缓存实例");

        string[] commonWindowsFonts = ["Arial", "Segoe UI", "Consolas"];
        Check(commonWindowsFonts.Any(x => names.Contains(x, StringComparer.OrdinalIgnoreCase)),
            "字体家族列表包含至少一个常见 Windows 字体");
    }

    private static void CheckImagePixelRoundTrip()
    {
        byte[] rgba =
        [
            255, 0, 0, 255, 0, 255, 0, 255,
            0, 0, 255, 255, 255, 255, 255, 128,
        ];
        var png = ImageUtils.EncodePng(rgba, 2, 2);
        Check(png.Length > PngSignature.Length && png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature),
            "PNG 编码输出带正确签名");

        var decoded = DecodeBgra32(png);
        Check(decoded.PixelWidth == 2 && decoded.PixelHeight == 2, "PNG 解码尺寸为 2x2");

        var pixels = new byte[2 * 2 * 4];
        decoded.CopyPixels(pixels, 2 * 4, 0);
        for (var i = 0; i < 4; i++)
        {
            var r = rgba[i * 4 + 0];
            var g = rgba[i * 4 + 1];
            var b = rgba[i * 4 + 2];
            var a = rgba[i * 4 + 3];
            Check(pixels[i * 4 + 0] == b && pixels[i * 4 + 1] == g && pixels[i * 4 + 2] == r && pixels[i * 4 + 3] == a,
                $"像素 {i} 逐通道一致（R={r},G={g},B={b},A={a}）");
        }
    }

    private static void CheckFlipVertical()
    {
        var pixels = new byte[4 * 3 * 4];
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 4; x++)
                pixels[(y * 4 + x) * 4] = (byte)(y + 1);

        ImageUtils.FlipVertical(pixels, 4, 3);
        Check(RowMarker(pixels, 4, 0) == 3, "翻转后第 0 行来自原第 2 行");
        Check(RowMarker(pixels, 4, 1) == 2, "翻转后中间行不变");
        Check(RowMarker(pixels, 4, 2) == 1, "翻转后第 2 行来自原第 0 行");

        var single = new byte[] { 9, 8, 7, 6 };
        ImageUtils.FlipVertical(single, 1, 1);
        Check(single.SequenceEqual(new byte[] { 9, 8, 7, 6 }), "1x1 翻转无效果");

        var odd = new byte[3 * 3 * 4];
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                odd[(y * 3 + x) * 4] = (byte)(y + 1);

        ImageUtils.FlipVertical(odd, 3, 3);
        Check(RowMarker(odd, 3, 1) == 2, "3x3 中间行翻转后不变");
    }

    private static void CheckLoadRgba32Resize()
    {
        var path = Path.Combine(workDir, "red2x2.png");
        WritePng(path, 2, 2, (_, _) => (255, 0, 0, 255));

        var original = ImageUtils.LoadRgba32(path);
        Check(original.Width == 2 && original.Height == 2, "LoadRgba32 保持原尺寸");
        Check(AllRgba(original, 255, 0, 0, 255), "2x2 纯红像素正确");

        var scaled = ImageUtils.LoadRgba32(path, 4, 6);
        Check(scaled.Width == 4 && scaled.Height == 6, "LoadRgba32 拉伸到 4x6");
        Check(AllRgba(scaled, 255, 0, 0, 255), "拉伸后仍为纯红（无颜色偏差）");
    }

    private static void CheckLoadRgba32AlphaAndJpeg()
    {
        var alphaPath = Path.Combine(workDir, "alpha.png");
        WritePng(alphaPath, 1, 1, (_, _) => (255, 0, 0, 128));
        var alpha = ImageUtils.LoadRgba32(alphaPath);
        Check(alpha.Data[3] is >= 120 and <= 136, $"半透明 alpha 保留（实际 {alpha.Data[3]}）");
        Check(alpha.Data[0] > 200, $"半透明红未预乘（实际 R={alpha.Data[0]}）");

        var jpegPath = Path.Combine(workDir, "green.jpg");
        WriteJpeg(jpegPath, 3, 2);
        var jpeg = ImageUtils.LoadRgba32(jpegPath);
        Check(jpeg.Width == 3 && jpeg.Height == 2, "JPEG 解码尺寸正确");
        Check(jpeg.Data.Where((_, index) => index % 4 == 1).All(x => x > 200), "JPEG 无 alpha 格式也能转换为绿色主导");
    }

    private static void CheckLoadRgba32Errors()
    {
        var missing = Path.Combine(workDir, "missing.png");
        CheckThrows<FileNotFoundException>(() => ImageUtils.LoadRgba32(missing), "不存在的文件抛 FileNotFoundException");

        var garbage = Path.Combine(workDir, "garbage.bin");
        File.WriteAllText(garbage, "not an image");
        CheckThrows<InvalidDataException>(() => ImageUtils.LoadRgba32(garbage), "非图片内容抛 InvalidDataException");

        var valid = Path.Combine(workDir, "valid.png");
        WritePng(valid, 2, 2, (_, _) => (0, 0, 0, 255));
        CheckThrows<ArgumentException>(() => ImageUtils.LoadRgba32(valid, 4, null), "宽高未同时指定抛 ArgumentException");
        CheckThrows<ArgumentOutOfRangeException>(() => ImageUtils.LoadRgba32(valid, 0, 4), "非正目标尺寸抛 ArgumentOutOfRangeException");
    }

    private static string CheckJacketGenerateRoundTrip()
    {
        var inputPath = Path.Combine(workDir, "jacket.png");
        WritePng(inputPath, 8, 8, (_, y) => y < 4 ? (255, 0, 0, 255) : (0, 0, 255, 255));

        var outDir = Path.Combine(workDir, "out");
        Directory.CreateDirectory(outDir);

        var result = JacketGenerateWrapper.Generate(new JacketGenerateOption
        {
            InputImageFilePath = inputPath,
            OutputAssetbundleFolderPath = outDir,
            MusicId = 1234,
            Width = 8,
            Height = 8,
            WidthSmall = 4,
            HeightSmall = 4,
            UpdateAssetBytesFile = false,
        }).GetAwaiter().GetResult();

        Check(result.IsSuccess, $"JacketGenerateWrapper.Generate 成功（{result.Message ?? "无消息"}）");

        var mainAbPath = Path.Combine(outDir, "ui_jacket_1234");
        var smallAbPath = Path.Combine(outDir, "ui_jacket_1234_s");
        Check(File.Exists(mainAbPath), "主封面 assetbundle 已生成");
        Check(File.Exists(smallAbPath), "小封面 assetbundle 已生成");

        var main = JacketGenerateWrapper.GetMainImageDataAsync(File.ReadAllBytes(mainAbPath), mainAbPath)
            .GetAwaiter().GetResult();
        Check(main.Width == 8 && main.Height == 8, $"主封面尺寸 8x8（实际 {main.Width}x{main.Height}）");
        Check(main.Data.Length == 8 * 8 * 4, "主封面 RGBA 数据长度正确");

        var row0 = AverageRowRgba(main.Data, main.Width, 0);
        var row7 = AverageRowRgba(main.Data, main.Width, main.Height - 1);
        Check(row0.Blue > row0.Red, $"主封面第 0 行偏蓝（R={row0.Red:F1}, B={row0.Blue:F1}）");
        Check(row7.Red > row7.Blue, $"主封面第 7 行偏红（R={row7.Red:F1}, B={row7.Blue:F1}）");

        return mainAbPath;
    }

    private static void CheckImageLoaderLoadsAb(string abPath)
    {
        var png = new ImageLoader().LoadImage(abPath, CancellationToken.None).GetAwaiter().GetResult();
        Check(png is { Length: > 8 }, "ImageLoader 返回非空 PNG 数据");
        Check(png.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature), "ImageLoader 输出是 PNG");

        var bitmap = DecodeBgra32(png);
        Check(bitmap.PixelWidth == 8 && bitmap.PixelHeight == 8, "ImageLoader PNG 为 8x8");

        var pixels = new byte[8 * 8 * 4];
        bitmap.CopyPixels(pixels, 8 * 4, 0);
        // AB 纹理第 0 行是图像底部（Generate 侧已翻转一次），ImageLoader 再翻转回视觉方向：
        // 输出 PNG 第 0 行应为原图顶部的红色，第 7 行为原图底部的蓝色。
        var row0 = AverageRowBgra(pixels, 8, 0);
        var row7 = AverageRowBgra(pixels, 8, 7);
        Check(row0.Red > row0.Blue, $"ImageLoader 图像第 0 行偏红（R={row0.Red:F1}, B={row0.Blue:F1}）");
        Check(row7.Blue > row7.Red, $"ImageLoader 图像第 7 行偏蓝（R={row7.Red:F1}, B={row7.Blue:F1}）");
    }

    private static void CheckCardImageConverter(string abPath)
    {
        var assembly = typeof(ImageUtils).Assembly;
        var type = assembly.GetType(
            "OngekiFumenEditor.Modules.OptionGeneratorTools.ValueConverters.LoadCardImageConverter",
            throwOnError: true)!;
        var converter = Activator.CreateInstance(type)!;
        var convertMethod = type.GetMethod("Convert")!;
        var task = convertMethod.Invoke(converter, [abPath, typeof(BitmapImage), null, CultureInfo.InvariantCulture])!;

        var property = task.GetType().GetProperty("AsyncValue")!;
        BitmapImage? bitmap = null;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (property.GetValue(task) is BitmapImage value)
            {
                bitmap = value;
                break;
            }
            Thread.Sleep(50);
        }

        Check(bitmap is not null, "卡片图片转换器在 10 秒内产出 BitmapImage");
        var cardBitmap = bitmap!;
        Check(cardBitmap.PixelWidth == 8 && cardBitmap.PixelHeight == 8,
            $"卡片图片尺寸 8x8（实际 {cardBitmap.PixelWidth}x{cardBitmap.PixelHeight}）");
    }

    private static void CheckNoSixLaborsReference()
    {
        var referenced = typeof(ImageUtils).Assembly.GetReferencedAssemblies()
            .Where(x => x.Name?.StartsWith("SixLabors.", StringComparison.OrdinalIgnoreCase) == true)
            .Select(x => x.Name)
            .ToArray();
        Check(referenced.Length == 0, $"主程序集不再引用 SixLabors（实际：{string.Join(", ", referenced)}）");

        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(x => x.GetName().Name?.StartsWith("SixLabors.", StringComparison.OrdinalIgnoreCase) == true)
            .Select(x => x.GetName().Name)
            .ToArray();
        Check(loaded.Length == 0, $"测试进程未加载 SixLabors 程序集（实际：{string.Join(", ", loaded)}）");
    }

    private static BitmapSource DecodeBgra32(byte[] png)
    {
        using var stream = new MemoryStream(png);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
    }

    private static void WritePng(string path, int width, int height, Func<int, int, (int R, int G, int B, int A)> pixel)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
            CreateBgraPixels(width, height, pixel), width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void WriteJpeg(string path, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null,
            CreateBgraPixels(width, height, (_, _) => (0, 255, 0, 255)), width * 4);
        var encoder = new JpegBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static byte[] CreateBgraPixels(int width, int height, Func<int, int, (int R, int G, int B, int A)> pixel)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                var index = (y * width + x) * 4;
                pixels[index + 0] = (byte)b;
                pixels[index + 1] = (byte)g;
                pixels[index + 2] = (byte)r;
                pixels[index + 3] = (byte)a;
            }
        }
        return pixels;
    }

    private static int RowMarker(byte[] pixels, int width, int row) => pixels[row * width * 4];

    private static bool AllRgba(ImageUtils.RgbaImage image, byte r, byte g, byte b, byte a)
    {
        for (var i = 0; i + 3 < image.Data.Length; i += 4)
            if (image.Data[i] != r || image.Data[i + 1] != g || image.Data[i + 2] != b || image.Data[i + 3] != a)
                return false;
        return true;
    }

    private static (double Red, double Green, double Blue, double Alpha) AverageRowRgba(byte[] rgba, int width, int row)
    {
        double red = 0, green = 0, blue = 0, alpha = 0;
        for (var x = 0; x < width; x++)
        {
            var index = (row * width + x) * 4;
            red += rgba[index + 0];
            green += rgba[index + 1];
            blue += rgba[index + 2];
            alpha += rgba[index + 3];
        }
        return (red / width, green / width, blue / width, alpha / width);
    }

    private static (double Red, double Green, double Blue, double Alpha) AverageRowBgra(byte[] bgra, int width, int row)
    {
        double red = 0, green = 0, blue = 0, alpha = 0;
        for (var x = 0; x < width; x++)
        {
            var index = (row * width + x) * 4;
            blue += bgra[index + 0];
            green += bgra[index + 1];
            red += bgra[index + 2];
            alpha += bgra[index + 3];
        }
        return (red / width, green / width, blue / width, alpha / width);
    }
}
