using System.Collections.Specialized;
using OngekiFumenEditor.Avalonia.Base.Collections;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;
using OngekiFumenEditor.Avalonia.Utils;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Base.Collections;

/// <summary>
/// Guards PERF-DAT-009 / DAT-12: <c>BulletPalleteList</c> used to implement
/// <c>this[int index] =&gt; this[index]</c>, i.e. the int indexer called itself until the process died with
/// an uncatchable <see cref="StackOverflowException"/>. The list now has to satisfy a real
/// <see cref="IReadOnlyList{T}"/> contract: <c>Count</c>, the int indexer and enumeration must all agree on
/// the same (numeric-id ascending) order, and that order must remain the one the old
/// <c>palleteMap.Values.OrderBy(x =&gt; ConvertIdToInt(x.StrID))</c> enumerator produced.
/// </summary>
public sealed class BulletPalleteListTests
{
    public BulletPalleteListTests()
    {
        // AddPallete logs through the IoC-backed Log when it replaces a same-id palette.
        Log.Initialize(new Log([]));
    }

    private static BulletPallete Palette(string strId) => new() { StrID = strId };

    [Fact]
    public void IntIndexer_ReturnsElementInEnumerationOrder()
    {
        var list = new BulletPalleteList();
        foreach (var strId in ShuffledIds(48))
            list.AddPallete(Palette(strId));

        var enumerated = list.ToArray();
        Assert.Equal(enumerated.Length, list.Count);

        for (var i = 0; i < enumerated.Length; i++)
            Assert.Same(enumerated[i], list[i]);
    }

    [Fact]
    public void IntIndexer_OnEmptyList_ThrowsInsteadOfRecursing()
    {
        var list = new BulletPalleteList();

        // The old self-recursive indexer blew the stack here (StackOverflowException cannot be caught).
        Assert.Throws<ArgumentOutOfRangeException>(() => list[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => list[-1]);
    }

    [Fact]
    public void IntIndexer_OutOfRange_Throws()
    {
        var list = new BulletPalleteList();
        list.AddPallete(Palette("2"));

        Assert.Throws<ArgumentOutOfRangeException>(() => list[1]);
    }

    [Fact]
    public void Enumeration_IsNumericIdOrder_NotStringOrder()
    {
        var list = new BulletPalleteList();
        // "10" sorts before "2" as a string, but its numeric id (36) is greater than "2" (2).
        list.AddPallete(Palette("10"));
        list.AddPallete(Palette("2"));

        Assert.Equal(new[] { "2", "10" }, list.Select(x => x.StrID).ToArray());
        Assert.Equal("2", list[0].StrID);
        Assert.Equal("10", list[1].StrID);
    }

    [Fact]
    public void Enumeration_MatchesLegacyOrderByContract_ForShuffledInsertion()
    {
        var list = new BulletPalleteList();
        var legacyMap = new Dictionary<int, BulletPallete>();

        foreach (var strId in ShuffledIds(120))
        {
            var pallete = Palette(strId);
            list.AddPallete(pallete);
            legacyMap[BulletPalleteList.ConvertIdToInt(strId)] = pallete;
        }

        var legacy = legacyMap.Values
            .OrderBy(x => BulletPalleteList.ConvertIdToInt(x.StrID))
            .Select(x => x.StrID)
            .ToArray();

        Assert.Equal(legacy, list.Select(x => x.StrID).ToArray());
    }

    [Fact]
    public void Insertions_KeepCountIndexerAndEnumerationInSync()
    {
        var list = new BulletPalleteList();
        var added = 0;

        foreach (var strId in ShuffledIds(200))
        {
            list.AddPallete(Palette(strId));
            added++;

            var ids = list.Select(x => BulletPalleteList.ConvertIdToInt(x.StrID)).ToArray();
            Assert.Equal(added, list.Count);
            Assert.Equal(ids.OrderBy(x => x).ToArray(), ids);

            var enumerated = list.ToArray();
            for (var i = 0; i < enumerated.Length; i++)
                Assert.Same(enumerated[i], list[i]);
        }
    }

    [Fact]
    public void AddPallete_BlankStrId_AssignsNextId()
    {
        var list = new BulletPalleteList();

        var first = new BulletPallete();
        list.AddPallete(first);
        Assert.Equal("A0", first.StrID);

        var second = new BulletPallete();
        list.AddPallete(second);
        Assert.Equal("A1", second.StrID);

        Assert.Equal(new[] { "A0", "A1" }, list.Select(x => x.StrID).ToArray());
        Assert.Same(first, list[0]);
        Assert.Same(second, list[1]);
    }

    [Fact]
    public void AddPallete_SameInstanceTwice_IsIgnored()
    {
        var list = new BulletPalleteList();
        var pallete = Palette("7");

        var events = 0;
        list.CollectionChanged += (_, _) => events++;

        list.AddPallete(pallete);
        list.AddPallete(pallete);

        Assert.Equal(1, list.Count);
        Assert.Equal(1, events);
        Assert.Same(pallete, list[0]);
    }

    [Fact]
    public void AddPallete_SameIdDifferentInstance_ReplacesOld()
    {
        var list = new BulletPalleteList();
        var old = Palette("7");
        var replacement = Palette("7");

        list.AddPallete(old);
        list.AddPallete(replacement);

        Assert.Equal(1, list.Count);
        Assert.Same(replacement, list[0]);
        Assert.Same(replacement, list["7"]);
        Assert.DoesNotContain(list, x => ReferenceEquals(x, old));
    }

    [Fact]
    public void RemovePallete_KeepsIndexerAndEnumerationConsistent()
    {
        var list = new BulletPalleteList();
        foreach (var strId in ShuffledIds(32))
            list.AddPallete(Palette(strId));

        var target = list[10];
        list.RemovePallete(target);

        var enumerated = list.ToArray();
        Assert.Equal(31, list.Count);
        Assert.DoesNotContain(list, x => ReferenceEquals(x, target));
        for (var i = 0; i < enumerated.Length; i++)
            Assert.Same(enumerated[i], list[i]);
    }

    [Fact]
    public void RemovePallete_UnknownInstance_IsNoOp()
    {
        var list = new BulletPalleteList();
        list.AddPallete(Palette("7"));

        list.RemovePallete(Palette("8"));

        Assert.Equal(1, list.Count);
    }

    [Fact]
    public void StringIndexer_IsCaseInsensitiveAndMissingIdReturnsNull()
    {
        var list = new BulletPalleteList();
        var pallete = Palette("AB");
        list.AddPallete(pallete);

        Assert.Same(pallete, list["AB"]);
        Assert.Same(pallete, list["ab"]);
        Assert.Null(list["ZZ"]);
    }

    [Fact]
    public void CollectionChanged_ReportsAddAndReset()
    {
        var list = new BulletPalleteList();
        var actions = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, e) => actions.Add(e.Action);

        var pallete = Palette("3");
        list.AddPallete(pallete);
        list.RemovePallete(pallete);

        Assert.Equal(
            new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Reset },
            actions);
    }

    /// <summary>
    /// A fixed set of ids in a fixed shuffled order, so the ordering assertions are deterministic and
    /// cover ids whose numeric order differs from their string order (e.g. "2"/"10", "9"/"A").
    /// </summary>
    private static IEnumerable<string> ShuffledIds(int count)
    {
        var ids = new List<int>();
        for (var i = 1; i <= count; i++)
            ids.Add(i);

        // Deterministic shuffle (Fisher-Yates with a fixed seed); ids stay unique so every insert is a
        // fresh add, never a same-id replacement.
        var rng = new Random(20260914);
        for (var i = ids.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        return ids.Select(BulletPalleteList.ConvertIntToId);
    }
}
