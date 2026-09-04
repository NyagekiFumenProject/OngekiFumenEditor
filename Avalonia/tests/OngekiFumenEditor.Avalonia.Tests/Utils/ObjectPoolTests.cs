using OngekiFumenEditor.Avalonia.Utils.ObjectPool;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Utils;

public sealed class ObjectPoolTests
{
    [Fact]
    public void PooledCollections_AreEmptyWhenRentedAgain()
    {
        var list = ObjectPool.GetPooledList<int>();
        list.Add(1);
        list.Dispose();

        var dictionary = ObjectPool.GetPooledDictionary<int, int>();
        dictionary[1] = 2;
        dictionary.Dispose();

        var set = ObjectPool.GetPooledSet<int>();
        set.Add(1);
        set.Dispose();

        using var nextList = ObjectPool.GetPooledList<int>();
        using var nextDictionary = ObjectPool.GetPooledDictionary<int, int>();
        using var nextSet = ObjectPool.GetPooledSet<int>();

        Assert.Empty(nextList);
        Assert.Empty(nextDictionary);
        Assert.Empty(nextSet);
    }

    [Fact]
    public void PooledCollection_DoubleDispose_DoesNotDuplicateWrapperInPool()
    {
        var list = ObjectPool.GetPooledList<WrapperItem>();
        list.Dispose();
        list.Dispose();

        using var first = ObjectPool.GetPooledList<WrapperItem>();
        using var second = ObjectPool.GetPooledList<WrapperItem>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void GenericLease_DoubleDispose_DoesNotDuplicateObjectInPool()
    {
        var lease = ObjectPool<LeaseItem>.GetWithUsingDisposable(out var rented);
        lease.Dispose();
        lease.Dispose();

        var first = ObjectPool<LeaseItem>.Get();
        var second = ObjectPool<LeaseItem>.Get();
        try
        {
            Assert.NotSame(first, second);
        }
        finally
        {
            ObjectPool<LeaseItem>.Return(first);
            ObjectPool<LeaseItem>.Return(second);
        }
    }

    [Fact]
    public void GenericLease_ExceptionalScope_ReturnsObject()
    {
        LeaseItem leased = null!;

        void Act()
        {
            using var lease = ObjectPool<LeaseItem>.GetWithUsingDisposable(out var value);
            leased = value;
            throw new InvalidOperationException("test");
        }

        Assert.Throws<InvalidOperationException>(Act);

        var returned = ObjectPool<LeaseItem>.Get();
        try
        {
            Assert.Same(leased, returned);
        }
        finally
        {
            ObjectPool<LeaseItem>.Return(returned);
        }
    }

    [Fact]
    public async Task GenericPool_ConcurrentHolders_AreDistinct()
    {
        const int holderCount = 32;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allRented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var items = new ConcurrentItem[holderCount];
        var rentedCount = 0;

        var holders = Enumerable.Range(0, holderCount).Select(async index =>
        {
            await start.Task;
            var item = ObjectPool<ConcurrentItem>.Get();
            items[index] = item;
            if (Interlocked.Increment(ref rentedCount) == holderCount)
                allRented.SetResult();

            await release.Task;
            ObjectPool<ConcurrentItem>.Return(item);
        }).ToArray();

        start.SetResult();
        await allRented.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(holderCount, items.Distinct(ReferenceEqualityComparer.Instance).Count());
        }
        finally
        {
            release.SetResult();
            await Task.WhenAll(holders);
        }
    }

    [Fact]
    public void Return_Null_DoesNothing()
    {
        var exception = Record.Exception(() => ObjectPool<ReturnItem>.Return(null!));

        Assert.Null(exception);
    }

    public sealed class WrapperItem { }
    public sealed class LeaseItem { }
    public sealed class ConcurrentItem { }
    public sealed class ReturnItem { }
}
