using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

/// <summary>
/// Guards PERF-DAT-010 / DAT-13. The 09-09 baseline had two costs in this type:
/// <list type="bullet">
/// <item><c>GetEnumerator()</c> re-sorted an already-ascending backing store on every enumeration
/// (<c>foreach (var item in changedMeterList.OrderBy(x =&gt; x.TGrid))</c>).</item>
/// <item><c>GetMeter</c> / <c>GetPrevMeter</c> / <c>GetNextMeter</c> were
/// <c>this.LastOrDefault(...)</c> / <c>this.FirstOrDefault(...)</c>. <c>MeterChangeList</c> is not
/// <c>IList&lt;T&gt;</c>, so every call re-enumerated the whole (re-sorted) sequence and then scanned it
/// linearly.</item>
/// </list>
/// It now walks the backing store directly and answers the three queries with binary predecessor/successor
/// lookups. The contract that must <b>not</b> move:
/// <list type="number">
/// <item>enumeration order stays <c>[FirstMeter, ...changed ascending by TGrid]</c>;</item>
/// <item>the three queries stay exactly equivalent to the old <c>Last/FirstOrDefault</c> over that sequence —
/// including the case where <c>FirstMeter</c> is <b>not</b> the smallest TGrid in the list.</item>
/// </list>
/// <see cref="Queries_MatchLegacySemantics_AcrossShapesAndProbePoints"/> pins (2) by re-running a verbatim copy
/// of the old query code (<see cref="Legacy"/>) against the same backing-order sequence.
/// </summary>
public sealed class MeterChangeListTests
{
    /// <summary>One TGrid unit == 1920 grid, matching <see cref="TGrid.DEFAULT_RES_T"/>.</summary>
    private const int Unit = (int)TGrid.DEFAULT_RES_T;

    private static MeterChange Meter(int totalGrid, int bunShi = 4, int bunbo = 4) => new()
    {
        TGrid = TGrid.FromTotalGrid(totalGrid),
        BunShi = bunShi,
        Bunbo = bunbo,
    };

    private static TGrid At(int totalGrid) => TGrid.FromTotalGrid(totalGrid);

    private static MeterChangeList Build(MeterChange firstMeter, params MeterChange[] changes)
    {
        var list = new MeterChangeList();
        list.SetFirstMeter(firstMeter);
        foreach (var change in changes)
            list.Add(change);
        return list;
    }

    /// <summary>
    /// Verbatim copy of the 09-09 baseline query code. Kept intentionally "dumb" (LINQ
    /// <c>OrderBy</c> + <c>Last/FirstOrDefault</c>) so it is an independent oracle for the new binary search.
    /// </summary>
    private static class Legacy
    {
        internal static IEnumerable<MeterChange> Sequence(MeterChange firstMeter, IEnumerable<MeterChange> changed)
        {
            yield return firstMeter;
            foreach (var item in changed.OrderBy(x => x.TGrid))
                yield return item;
        }

        internal static MeterChange GetMeter(MeterChange first, IEnumerable<MeterChange> changed, TGrid time)
            => Sequence(first, changed).LastOrDefault(m => m.TGrid <= time);

        internal static MeterChange GetPrevMeter(MeterChange first, IEnumerable<MeterChange> changed, TGrid time)
            => Sequence(first, changed).LastOrDefault(m => m.TGrid < time);

        internal static MeterChange GetNextMeter(MeterChange first, IEnumerable<MeterChange> changed, TGrid time)
            => Sequence(first, changed).FirstOrDefault(m => time < m.TGrid);
    }

    // ---------------------------------------------------------------- shape matrix

    public static IEnumerable<object[]> ShapeNames()
    {
        yield return ["only-first-meter"];
        yield return ["three-distinct"];
        yield return ["duplicate-tgrids"];
        yield return ["change-collides-with-first"];
        yield return ["first-meter-not-smallest"];
        yield return ["negative-tgrids"];
    }

    private static (MeterChange First, MeterChange[] Changes) BuildShape(string shape) => shape switch
    {
        "only-first-meter" => (Meter(0), []),
        "three-distinct" => (Meter(0), [Meter(Unit), Meter(Unit * 3), Meter(Unit * 6)]),
        "duplicate-tgrids" => (Meter(0), [Meter(Unit * 2, bunShi: 3), Meter(Unit * 2, bunShi: 5), Meter(Unit * 2, bunShi: 7)]),
        "change-collides-with-first" => (Meter(0), [Meter(0, bunShi: 7), Meter(Unit)]),
        // FirstMeter is deliberately NOT the minimum: the old enumerator yielded it first unconditionally, so
        // the sequence was not ascending. The queries must still agree with the old linear scan.
        "first-meter-not-smallest" => (Meter(Unit), [Meter(Unit / 4), Meter(Unit * 2), Meter(Unit * 3)]),
        "negative-tgrids" => (Meter(-Unit), [Meter(-Unit * 3), Meter(0), Meter(Unit * 5)]),
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
    };

    /// <summary>Probe points around every element of the shape (exact / ±1 / ± half unit / ± one unit).</summary>
    private static IEnumerable<TGrid> ProbePoints(MeterChange first, IReadOnlyList<MeterChange> changes)
    {
        var totals = new SortedSet<int> { -Unit * 10, -Unit / 2, -1, 0, 1, Unit / 2, Unit * 10 };

        foreach (var meter in changes.Append(first))
        {
            var at = meter.TGrid.TotalGrid;
            totals.Add(at);
            totals.Add(at - 1);
            totals.Add(at + 1);
            totals.Add(at - Unit);
            totals.Add(at - Unit / 2);
            totals.Add(at + Unit / 2);
            totals.Add(at + Unit);
        }

        return totals.Select(At);
    }

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Queries_MatchLegacySemantics_AcrossShapesAndProbePoints(string shape)
    {
        var (first, changes) = BuildShape(shape);
        var list = Build(first, changes);

        // The old code sorted `changedMeterList`, whose content and order is exactly what the enumerator
        // exposes after the first element — feed the oracle that same sequence.
        var backingOrder = list.Skip(1).ToArray();

        var probes = 0;
        foreach (var time in ProbePoints(first, changes))
        {
            probes++;
            Assert.Same(Legacy.GetMeter(first, backingOrder, time), list.GetMeter(time));
            Assert.Same(Legacy.GetPrevMeter(first, backingOrder, time), list.GetPrevMeter(time));
            Assert.Same(Legacy.GetNextMeter(first, backingOrder, time), list.GetNextMeter(time));
        }

        // Sanity: a vacuous sweep would prove nothing, so require the sentinel points **and** every element's
        // exact key to be covered (small shapes legitimately produce only ~9-15 distinct probe points).
        var covered = ProbePoints(first, changes).Select(x => x.TotalGrid).ToHashSet();
        foreach (var meter in changes.Append(first))
            Assert.Contains(meter.TGrid.TotalGrid, covered);
        Assert.True(covered.Count >= 9, $"probe sweep was too small ({covered.Count}/{probes}).");
    }

    [Theory]
    [MemberData(nameof(ShapeNames))]
    public void Queries_AlsoMatchLegacyOverTheInsertionOrder_WhenTGridsAreDistinct(string shape)
    {
        var (first, changes) = BuildShape(shape);

        var distinct = changes.Select(x => x.TGrid.TotalGrid).Distinct().Count() == changes.Length
                       && changes.All(x => x.TGrid.TotalGrid != first.TGrid.TotalGrid);
        if (!distinct)
            return; // ties make the old OrderBy's stability the deciding factor; covered by the backing-order sweep

        var list = Build(first, changes);
        var insertionOrder = changes.ToArray();

        foreach (var time in ProbePoints(first, changes))
        {
            Assert.Same(Legacy.GetMeter(first, insertionOrder, time), list.GetMeter(time));
            Assert.Same(Legacy.GetPrevMeter(first, insertionOrder, time), list.GetPrevMeter(time));
            Assert.Same(Legacy.GetNextMeter(first, insertionOrder, time), list.GetNextMeter(time));
        }
    }

    // ---------------------------------------------------------------- enumeration order

    [Fact]
    public void Enumeration_IsFirstMeterThenAscendingTGrid()
    {
        var first = Meter(0);
        var list = Build(first, Meter(Unit * 6), Meter(Unit), Meter(Unit * 3));

        var enumerated = list.ToArray();

        Assert.Equal(4, list.Count);
        Assert.Equal(4, enumerated.Length);
        Assert.Same(first, enumerated[0]);
        Assert.Equal(
            new[] { Unit, Unit * 3, Unit * 6 },
            enumerated.Skip(1).Select(x => x.TGrid.TotalGrid).ToArray());
    }

    [Fact]
    public void Enumeration_MatchesLegacyOrderByContract_ForShuffledInsertion()
    {
        var first = Meter(0);
        var totals = Enumerable.Range(1, 120).Select(i => i * Unit).ToArray();

        var rng = new Random(20260916);
        for (var i = totals.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (totals[i], totals[j]) = (totals[j], totals[i]);
        }

        var changes = totals.Select(t => Meter(t)).ToArray();
        var list = Build(first, changes);

        // Old contract: `[firstMeter] ++ changedMeterList.OrderBy(x => x.TGrid)`.
        var legacy = Legacy.Sequence(first, changes).Select(x => x.TGrid.TotalGrid).ToArray();
        Assert.Equal(legacy, list.Select(x => x.TGrid.TotalGrid).ToArray());
    }

    // ---------------------------------------------------------------- boundary behaviour, stated explicitly

    [Fact]
    public void GetMeter_ResolvesBoundariesAsLegacyDid()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var list = Build(first, m1, m2);

        Assert.Null(list.GetMeter(At(-1)));            // before FirstMeter
        Assert.Same(first, list.GetMeter(At(0)));      // exactly on FirstMeter
        Assert.Same(first, list.GetMeter(At(Unit - 1))); // between FirstMeter and m1
        Assert.Same(m1, list.GetMeter(At(Unit)));      // exactly on m1
        Assert.Same(m1, list.GetMeter(At(Unit * 3 - 1)));
        Assert.Same(m2, list.GetMeter(At(Unit * 3)));  // exactly on the last change
        Assert.Same(m2, list.GetMeter(At(Unit * 100))); // past the end -> last change, never null
    }

    [Fact]
    public void GetMeter_OnListWithOnlyFirstMeter_OnlyMatchesFirstMeter()
    {
        var first = Meter(0);
        var list = Build(first);

        Assert.Equal(1, list.Count);
        Assert.Null(list.GetMeter(At(-1)));
        Assert.Same(first, list.GetMeter(At(0)));
        Assert.Same(first, list.GetMeter(At(Unit * 50)));
    }

    [Fact]
    public void GetMeter_WithDuplicateTGrid_ReturnsLastMatchInEnumerationOrder()
    {
        var first = Meter(0);
        var list = Build(first, Meter(Unit * 2, bunShi: 3), Meter(Unit * 2, bunShi: 5));

        // Old semantics were `LastOrDefault(m => m.TGrid <= time)` over the enumerated sequence, so the tie is
        // resolved by enumeration order — the query must keep agreeing with the enumerator.
        var expected = list.Where(m => m.TGrid <= At(Unit * 2)).Last();
        Assert.Same(expected, list.GetMeter(At(Unit * 2)));
        Assert.Same(expected, list.GetMeter(At(Unit * 5)));
    }

    [Fact]
    public void GetPrevMeter_IsStrictlyLessThan()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var list = Build(first, m1, m2);

        Assert.Null(list.GetPrevMeter(At(0)));        // nothing strictly before FirstMeter
        Assert.Null(list.GetPrevMeter(At(-5)));
        Assert.Same(first, list.GetPrevMeter(At(1)));
        Assert.Same(first, list.GetPrevMeter(At(Unit)));      // exactly on m1 -> the one before it
        Assert.Same(m1, list.GetPrevMeter(At(Unit * 3)));     // exactly on m2
        Assert.Same(m2, list.GetPrevMeter(At(Unit * 100)));
    }

    [Fact]
    public void GetNextMeter_IsStrictlyGreaterThan_AndIncludesFirstMeter()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var list = Build(first, m1, m2);

        Assert.Same(first, list.GetNextMeter(At(-1))); // enumerator starts at FirstMeter
        Assert.Same(m1, list.GetNextMeter(At(0)));     // exactly on FirstMeter
        Assert.Same(m1, list.GetNextMeter(At(Unit - 1)));
        Assert.Same(m2, list.GetNextMeter(At(Unit)));  // exactly on m1
        Assert.Null(list.GetNextMeter(At(Unit * 3)));
        Assert.Null(list.GetNextMeter(At(Unit * 100)));
    }

    [Fact]
    public void GetPrevAndNextMeter_AcceptAMeterChangeOverload()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var list = Build(first, m1, m2);

        Assert.Same(first, list.GetPrevMeter(m1));
        Assert.Same(m2, list.GetNextMeter(m1));
        Assert.Null(list.GetNextMeter(m2));
    }

    // ---------------------------------------------------------------- mutation

    [Fact]
    public void Queries_ReflectAddAndRemove()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var list = Build(first, m1);

        Assert.Null(list.GetNextMeter(At(Unit)));

        list.Add(m2);
        Assert.Same(m2, list.GetNextMeter(At(Unit)));
        Assert.Same(m2, list.GetMeter(At(Unit * 5)));
        Assert.Equal(3, list.Count);

        Assert.True(list.Remove(m2));
        Assert.Null(list.GetNextMeter(At(Unit)));
        Assert.Same(m1, list.GetMeter(At(Unit * 5)));
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Queries_ReflectSetFirstMeter()
    {
        var list = Build(Meter(0), Meter(Unit * 3));
        var newFirst = Meter(Unit);

        list.SetFirstMeter(newFirst);

        Assert.Same(newFirst, list.FirstMeter);
        Assert.Same(newFirst, list.ToArray()[0]);
        // Unit is now the smallest meter, so nothing precedes it and it becomes the answer for [Unit, 3*Unit).
        Assert.Null(list.GetPrevMeter(At(Unit)));
        Assert.Same(newFirst, list.GetMeter(At(Unit)));
        Assert.Same(newFirst, list.GetNextMeter(At(0)));
    }

    [Fact]
    public void Enumerator_IsLive_MutationDuringEnumerationThrowsInsteadOfBeingSilent()
    {
        var list = Build(Meter(0), Meter(Unit));
        using var enumerator = list.GetEnumerator();

        Assert.True(enumerator.MoveNext()); // FirstMeter
        Assert.True(enumerator.MoveNext()); // first changed -> parked inside the backing enumerator

        // The old enumerator was a snapshot (OrderBy materialised the sequence), so mutating mid-enumeration was
        // silent. The new one is a live enumerator and follows standard .NET collection semantics. Same
        // behaviour change was accepted for PERF-DAT-009 / DAT-12.
        list.Add(Meter(Unit * 2));

        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    // ---------------------------------------------------------------- the range/contains surface must not regress

    [Fact]
    public void BinaryFindRangeAndContains_StillWork()
    {
        var first = Meter(0);
        var m1 = Meter(Unit);
        var m2 = Meter(Unit * 3);
        var m3 = Meter(Unit * 6);
        var list = Build(first, m1, m2, m3);

        Assert.Equal(new[] { Unit, Unit * 3 }, list.BinaryFindRange(At(Unit), At(Unit * 3)).Select(x => x.TGrid.TotalGrid).ToArray());
        Assert.True(list.Contains(m2));
        Assert.False(list.Contains(Meter(Unit * 4)));

        var (minIndex, maxIndex) = list.BinaryFindRangeIndex(At(Unit), At(Unit * 3));
        Assert.True(minIndex < maxIndex);
    }
}
