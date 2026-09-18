using BenchmarkDotNet.Attributes;
using System.ComponentModel;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 性能热点 #17 - ParserUtils.GetDataArray 每行反射 + 双 ToArray + LINQ
/// 对照: OngekiFumenEditor/Parser/ParserUtils.cs:11-25
///
/// 原版每行做:
///   1) SplitEmptyChar 内 line.Trim().Split(...).ToArray()  // ToArray 把 string[] 又拷贝一份
///   2) TypeDescriptor.GetConverter(typeof(T))              // 每行反射查表
///   3) .Skip(1).Select(x => converter.IsValid(x) ? converter.ConvertFromString(x) : default).ToArray()
///      // LINQ 链 + ConvertFromString 内部 string -> object 装箱
///
/// 递增对照(每条只改一点,看每一步贡献):
///   - Optimized_CachedConverter:           缓存 TypeConverter,去掉每行反射查表
///   - Optimized_NoLinqNoExtraToArray:      + 去掉 LINQ + 去掉重复 ToArray (直接索引 string[])
///   - Optimized_FastPathInt:               + T=int 走 int.TryParse(ReadOnlySpan&lt;char&gt;) 完全绕过 TypeDescriptor
///
/// 测 T=int 的常见场景(ogkr 里大多数命令是 int tokens)。模拟 [Params(100, 1000, 5000)] 行命令。
/// </summary>
public class ParserUtilsBenchmarks
{
    private static readonly char[] SplitEmptyCharArray = [' ', '\t'];
    private static readonly Dictionary<Type, TypeConverter> converterCache = new();

    [Params(100, 1000, 5000)]
    public int Lines;

    private string[] lines = Array.Empty<string>();

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        lines = new string[Lines];
        // 模拟典型 ogkr 命令行: "CMD i1 i2 i3 i4 i5" 6 个 token,首个是命令名跳过。
        for (var i = 0; i < Lines; i++)
        {
            lines[i] = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"CMD {rng.Next(1000)} {rng.Next(100)} {rng.Next(1000)} {rng.Next(10)} {rng.Next(10)}");
        }
    }

    // ============ Original ============

    [Benchmark(Baseline = true)]
    public int Original()
    {
        var sum = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var arr = GetDataArray_Original<int>(lines[i]);
            for (var j = 0; j < arr.Length; j++) sum += arr[j];
        }
        return sum;
    }

    private static T[] GetDataArray_Original<T>(string line)
    {
        var converter = TypeDescriptor.GetConverter(typeof(T));
        return SplitEmptyChar_Original(line).Skip(1).Select(x =>
        {
            if (converter.IsValid(x))
                return (T)converter.ConvertFromString(x);
            return default;
        }).ToArray();
    }

    private static string[] SplitEmptyChar_Original(string line)
    {
        return line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries).ToArray();
    }

    // ============ Optimized A: CachedConverter ============

    [Benchmark]
    public int Optimized_CachedConverter()
    {
        var sum = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var arr = GetDataArray_CachedConverter<int>(lines[i]);
            for (var j = 0; j < arr.Length; j++) sum += arr[j];
        }
        return sum;
    }

    private static T[] GetDataArray_CachedConverter<T>(string line)
    {
        var converter = GetCachedConverter(typeof(T));
        return line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(x =>
            {
                if (converter.IsValid(x))
                    return (T)converter.ConvertFromString(x);
                return default;
            })
            .ToArray();
    }

    private static TypeConverter GetCachedConverter(Type t)
    {
        if (!converterCache.TryGetValue(t, out var c))
            converterCache[t] = c = TypeDescriptor.GetConverter(t);
        return c;
    }

    // ============ Optimized B: NoLinqNoExtraToArray ============

    [Benchmark]
    public int Optimized_NoLinqNoExtraToArray()
    {
        var sum = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var arr = GetDataArray_NoLinq<int>(lines[i]);
            for (var j = 0; j < arr.Length; j++) sum += arr[j];
        }
        return sum;
    }

    private static T[] GetDataArray_NoLinq<T>(string line)
    {
        var converter = GetCachedConverter(typeof(T));
        var parts = line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return Array.Empty<T>();
        var result = new T[parts.Length - 1];
        for (var i = 1; i < parts.Length; i++)
        {
            var x = parts[i];
            if (converter.IsValid(x))
                result[i - 1] = (T)converter.ConvertFromString(x);
        }
        return result;
    }

    // ============ Optimized C: FastPathInt (int.TryParse on Span) ============

    [Benchmark]
    public int Optimized_FastPathInt()
    {
        var sum = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var arr = GetDataArray_FastPathInt(lines[i]);
            for (var j = 0; j < arr.Length; j++) sum += arr[j];
        }
        return sum;
    }

    private static int[] GetDataArray_FastPathInt(string line)
    {
        var parts = line.Trim().Split(SplitEmptyCharArray, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1)
            return Array.Empty<int>();
        var result = new int[parts.Length - 1];
        for (var i = 1; i < parts.Length; i++)
        {
            // ReadOnlySpan<char> 重载,无 box、无 TypeDescriptor、无反射
            if (int.TryParse(parts[i].AsSpan(), out var v))
                result[i - 1] = v;
        }
        return result;
    }
}
