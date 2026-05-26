using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Numerics;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

/// <summary>
/// 性能热点 #3 - LaneCurvePathControlDrawingTarget.DrawBatch 每帧多次枚举 + GroupBy.SelectMany.yield + Index.ToString()
/// 对照: LaneCurvePathControlDrawingTarget.cs:46,55-79,82-83,88
///
/// 原版每帧:
///   1) Where + Select + ToListWithObjectPool 实化 list
///   2) list.GroupBy(...).SelectMany(item => gen()) 用 yield 生成 LineVertex,被 builder 消费一次
///   3) gen 内部还做 item.OrderBy(Index).Reverse()
///   4) list.Where(...).Select(...) 给 DrawHighlightBatchTexture(再枚举一次 list)
///   5) list.Select(...) 给 DrawTexture(再枚举一次 list)
///   6) foreach list 调用 RegisterSelectableObject
///   7) foreach list 调用 Index.ToString() + DrawString
///
/// 优化版用单次扫描 + PooledList 收集 + IndexCache 替代以上 6+ 次枚举。
///
/// 用独立 DTO 模拟,避免引用 OngekiFumenEditor 内部 Caliburn/Gemini 依赖
/// (类型加载先于 GlobalSetup,无法绕过 Costura.Attach)。
/// 测出的差异仅来自"多次 LINQ 枚举 + yield + ToString"vs"单次 foreach + 缓存"。
/// </summary>
public class LaneCurvePathBenchmarks
{
    private sealed class CurveRef
    {
        public bool IsSelected;
        public bool IsAnyControlSelecting;
        public object ReferenceStartObject = new();
        public float ParentX;
        public float ParentY;
        public float X;
        public float Y;
    }

    private sealed class Obj
    {
        public CurveRef RefCurveObject = null!;
        public int Index;
        public bool IsSelected;
    }

    private readonly record struct CtrlPoint(float Y, float X, Obj Obj);
    private readonly record struct LineV(Vector2 Pos, Vector4 Color);
    private readonly record struct TexInst(Vector2 Size, Vector2 Pos, float Rot, Vector4 Color);

    private static readonly Vector4 Transparent = new(0, 0, 0, 0);
    private static readonly Vector2 TexSize = new(16, 16);

    private CtrlPoint[] inputs = Array.Empty<CtrlPoint>();
    private bool isAlwaysShow = true;

    // 缓存 Index -> string,适用于 Index 取值有限的场景(0..N-1)
    private readonly Dictionary<int, string> indexCache = new();

    [Params(100, 500, 2000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        // 模拟实际场景:每条曲线 ~4 个控制点
        var refsCount = Math.Max(1, N / 4);
        var refs = new List<CurveRef>(refsCount);
        for (var i = 0; i < refsCount; i++)
            refs.Add(new CurveRef
            {
                IsSelected = rng.Next(4) == 0,
                X = i * 5f,
                Y = i * 10f,
                ParentX = i * 5f - 3f,
                ParentY = i * 10f - 5f,
            });

        var list = new List<CtrlPoint>(N);
        for (var i = 0; i < N; i++)
        {
            var r = refs[i % refs.Count];
            var obj = new Obj
            {
                RefCurveObject = r,
                Index = i,
                IsSelected = rng.Next(8) == 0,
            };
            list.Add(new CtrlPoint(i * 1.5f, i * 2f, obj));
        }
        inputs = list.ToArray();
    }

    // ============ 原版:多次 LINQ 枚举 + yield + Index.ToString() ============

    [Benchmark(Baseline = true)]
    public int Original_MultiPassLinq()
    {
        var sink = 0;
        using var list = inputs
            .Where(x => x.Obj.RefCurveObject.IsSelected
                        || x.Obj.RefCurveObject.IsAnyControlSelecting
                        || isAlwaysShow)
            .Select(x => (y: x.Y, x: x.X, obj: x.Obj))
            .ToListWithObjectPool();

        if (list.Count == 0)
            return sink;

        var lineVertices = list.GroupBy(x => x.obj.RefCurveObject).SelectMany(item =>
        {
            IEnumerable<LineV> gen()
            {
                var r = item.Key;
                var hash = r.ReferenceStartObject.GetHashCode();
                var alpha = (byte)((hash >> 24) & 0xFF);
                var color = new Vector4(
                    (((hash >> 16) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                    (((hash >> 8) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                    ((hash & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                    1f);
                yield return new LineV(new(r.X, r.Y), Transparent);
                yield return new LineV(new(r.X, r.Y), color);
                foreach (var curve in item.OrderBy(x => x.obj.Index).Reverse())
                    yield return new LineV(new(curve.x, curve.y), color);
                yield return new LineV(new(r.ParentX, r.ParentY), color);
                yield return new LineV(new(r.ParentX, r.ParentY), Transparent);
            }
            return gen();
        });

        // 模拟 builder.DrawSimpleLines 一次性消费 IEnumerable
        foreach (var v in lineVertices)
            sink += (int)v.Pos.X;

        // builder.DrawHighlightBatchTexture: list.Where(...).Select(...)
        foreach (var t in list.Where(x => x.obj.IsSelected)
                              .Select(x => (TexSize * 1.25f, new Vector2(x.x, x.y), 0f, Vector4.One)))
            sink += (int)t.Item2.X;

        // builder.DrawTexture: list.Select(...)
        foreach (var t in list.Select(x => (TexSize, new Vector2(x.x, x.y), 0f, Vector4.One)))
            sink += (int)t.Item2.X;

        // RegisterSelectableObject 枚举
        foreach (var item in list)
            sink += (int)item.x;

        // DrawString + Index.ToString() 枚举
        foreach (var item in list)
        {
            var s = item.obj.Index.ToString();
            sink += s.Length;
        }

        return sink;
    }

    // ============ 优化:单次 foreach 收集 + IndexCache ============

    [Benchmark]
    public int Optimized_SinglePass()
    {
        var sink = 0;

        using var filtered = ObjectPool.GetPooledList<CtrlPoint>();
        using var allTex = ObjectPool.GetPooledList<TexInst>();
        using var selectedTex = ObjectPool.GetPooledList<TexInst>();
        using var buckets = ObjectPool.GetPooledDictionary<CurveRef, IPooledList<CtrlPoint>>();

        // 一次扫描完成: 过滤 + 收集纹理实例 + 分桶
        for (var i = 0; i < inputs.Length; i++)
        {
            var v = inputs[i];
            var r = v.Obj.RefCurveObject;
            if (!(r.IsSelected || r.IsAnyControlSelecting || isAlwaysShow))
                continue;

            filtered.Add(v);
            allTex.Add(new TexInst(TexSize, new(v.X, v.Y), 0f, Vector4.One));
            if (v.Obj.IsSelected)
                selectedTex.Add(new TexInst(TexSize * 1.25f, new(v.X, v.Y), 0f, Vector4.One));

            if (!buckets.TryGetValue(r, out var bucket))
            {
                bucket = ObjectPool.GetPooledList<CtrlPoint>();
                buckets[r] = bucket;
            }
            bucket.Add(v);
        }

        if (filtered.Count == 0)
            return sink;

        // 第二遍: 按桶生成 lineVertices 到 PooledList
        using var lineVertices = ObjectPool.GetPooledList<LineV>();
        var indexDescCmp = Comparer<CtrlPoint>.Create(static (a, b) => b.Obj.Index.CompareTo(a.Obj.Index));

        foreach (var kv in buckets)
        {
            var r = kv.Key;
            var items = kv.Value;
            items.Sort(indexDescCmp);

            var hash = r.ReferenceStartObject.GetHashCode();
            var alpha = (byte)((hash >> 24) & 0xFF);
            var color = new Vector4(
                (((hash >> 16) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                (((hash >> 8) & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                ((hash & 0xFF) ^ alpha) / 255f / 2 + 0.5f,
                1f);
            lineVertices.Add(new LineV(new(r.X, r.Y), Transparent));
            lineVertices.Add(new LineV(new(r.X, r.Y), color));
            for (var i = 0; i < items.Count; i++)
                lineVertices.Add(new LineV(new(items[i].X, items[i].Y), color));
            lineVertices.Add(new LineV(new(r.ParentX, r.ParentY), color));
            lineVertices.Add(new LineV(new(r.ParentX, r.ParentY), Transparent));

            items.Dispose();
        }

        foreach (var v in lineVertices)
            sink += (int)v.Pos.X;
        foreach (var v in selectedTex)
            sink += (int)v.Pos.X;
        foreach (var v in allTex)
            sink += (int)v.Pos.X;

        // RegisterSelectableObject + DrawString 共用同一遍 foreach filtered
        for (var i = 0; i < filtered.Count; i++)
        {
            var item = filtered[i];
            sink += (int)item.X;
            var s = GetIndexString(item.Obj.Index);
            sink += s.Length;
        }

        return sink;
    }

    private string GetIndexString(int idx)
    {
        if (!indexCache.TryGetValue(idx, out var s))
            indexCache[idx] = s = idx.ToString();
        return s;
    }
}
