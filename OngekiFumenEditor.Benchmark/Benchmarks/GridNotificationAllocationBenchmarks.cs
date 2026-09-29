using BenchmarkDotNet.Attributes;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils;
using System.ComponentModel;

namespace OngekiFumenEditor.Benchmark.Benchmarks;

[MemoryDiagnoser]
public class GridNotificationAllocationBenchmarks
{
    private const uint Radix = 1920;
    private const int OpsPerInvoke = 1000;

    private int notificationCount;

    private LegacyRadixGrid legacyAttached = null!;
    private TGrid fixedAttached = null!;

    private int unitCursor;

    private FakeObject legacyOwner = null!;
    private LegacyRadixGrid legacyOld = null!;
    private LegacyRadixGrid legacyNew = null!;
    private FakeObject fixedOwner = null!;
    private TGrid fixedOld = null!;
    private TGrid fixedNew = null!;
    private bool subscriptionFlip;

    private void OnGridChanged(object? sender, PropertyChangedEventArgs e) => notificationCount++;

    [GlobalSetup]
    public void Setup()
    {
        legacyAttached = new LegacyRadixGrid(0, 0) { GridRadix = Radix };
        fixedAttached = new TGrid(0, 0);
        legacyAttached.PropertyChanged += OnGridChanged;
        fixedAttached.PropertyChanged += OnGridChanged;

        legacyOwner = new FakeObject();
        fixedOwner = new FakeObject();
        legacyOld = new LegacyRadixGrid(1, 5) { GridRadix = Radix };
        legacyNew = new LegacyRadixGrid(2, 7) { GridRadix = Radix };
        fixedOld = new TGrid(1, 5);
        fixedNew = new TGrid(2, 7);

        ValidateEquivalence();
    }

    private static void ValidateEquivalence()
    {
        var cases = new (float unit, int grid)[]
        {
            (0, 0), (5, 0), (5.5f, 0), (0, 1920), (0, -5), (3, 2000), (-2, -1),
            (18.25f, 73727), (0.9999f, 1919), (-0.5f, 0), (12345.678f, -987655),
        };

        foreach (var (unit, grid) in cases)
        {
            var legacy = new LegacyRadixGrid(unit, grid) { GridRadix = Radix };
            var current = new TGrid(unit, grid);
            legacy.NormalizeSelf();
            current.NormalizeSelf();

            if (legacy.Unit != current.Unit || legacy.Grid != current.Grid
                || legacy.TotalGrid != current.TotalGrid || legacy.TotalUnit != current.TotalUnit)
                throw new InvalidOperationException(
                    $"NormalizeSelf 结果不一致: input=({unit},{grid}) legacy=({legacy.Unit},{legacy.Grid},{legacy.TotalGrid}) current=({current.Unit},{current.Grid},{current.TotalGrid})");
        }
    }

    private float NextUnit()
    {
        unitCursor ^= 1;
        return unitCursor == 0 ? 12.5f : 13.75f;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = OpsPerInvoke)]
    public double SetUnit_Legacy()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            legacyAttached.Unit = NextUnit();
            acc += legacyAttached.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public double SetUnit_Fixed()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            fixedAttached.Unit = NextUnit();
            acc += fixedAttached.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int NormalizeSelf_AlreadyNormalized_Legacy()
    {
        notificationCount = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
            legacyAttached.NormalizeSelf();
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int NormalizeSelf_AlreadyNormalized_Fixed()
    {
        notificationCount = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
            fixedAttached.NormalizeSelf();
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public double NormalizeSelf_Fractional_Legacy()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            legacyAttached.Unit = (i & 1) == 0 ? 6.5f : 7.5f;
            legacyAttached.NormalizeSelf();
            acc += legacyAttached.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public double NormalizeSelf_Fractional_Fixed()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            fixedAttached.Unit = (i & 1) == 0 ? 6.5f : 7.5f;
            fixedAttached.NormalizeSelf();
            acc += fixedAttached.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public double NormalizeSelf_Fresh_Legacy()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            var grid = new LegacyRadixGrid(18.5f, 0) { GridRadix = Radix };
            grid.NormalizeSelf();
            acc += grid.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public double NormalizeSelf_Fresh_Fixed()
    {
        double acc = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            var grid = new TGrid(18.5f, 0);
            grid.NormalizeSelf();
            acc += grid.TotalUnit;
        }
        return acc;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int Notify_ByString_Legacy()
    {
        notificationCount = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
            legacyAttached.NotifyOfPropertyChange(nameof(LegacyRadixGrid.Unit));
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int Notify_ByString_Fixed()
    {
        notificationCount = 0;
        for (var i = 0; i < OpsPerInvoke; i++)
            fixedAttached.NotifyOfPropertyChange(nameof(TGrid.Unit));
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int Notify_ByCachedArgs_Fixed()
    {
        notificationCount = 0;
        var args = CommonPropertyChangedBase.ArgsOf(nameof(TGrid.Unit));
        for (var i = 0; i < OpsPerInvoke; i++)
            fixedAttached.NotifyOfPropertyChange(args);
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int Subscribe_ForwardingHelper_Legacy()
    {
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            subscriptionFlip = !subscriptionFlip;
            legacyOwner.RegisterOrUnregisterPropertyChangeEvent(
                subscriptionFlip ? legacyOld : legacyNew,
                subscriptionFlip ? legacyNew : legacyOld);
        }
        return notificationCount;
    }

    [Benchmark(OperationsPerInvoke = OpsPerInvoke)]
    public int Subscribe_ForwardingHelper_Fixed()
    {
        for (var i = 0; i < OpsPerInvoke; i++)
        {
            subscriptionFlip = !subscriptionFlip;
            fixedOwner.RegisterOrUnregisterPropertyChangeEvent(
                subscriptionFlip ? fixedOld : fixedNew,
                subscriptionFlip ? fixedNew : fixedOld);
        }
        return notificationCount;
    }

    private sealed class FakeObject : PropertyChangedBase
    {
    }

    private sealed class LegacyRadixGrid : PropertyChangedBase
    {
        private int grid;
        private float unit;

        private readonly uint gridRadix;
        public uint GridRadix
        {
            get => gridRadix;
            init
            {
                gridRadix = value;
                RecalculateTotalValues();
            }
        }

        public int TotalGrid { get; private set; }
        public double TotalUnit { get; private set; }

        private void RecalculateTotalValues()
        {
            TotalGrid = (int)(Unit * GridRadix + Grid);
            TotalUnit = Unit + Grid * 1.0 / GridRadix;
        }

        public LegacyRadixGrid(float unit, int grid)
        {
            Grid = grid;
            Unit = unit;
        }

        public int Grid
        {
            get => grid;
            set
            {
                grid = value;
                RecalculateTotalValues();
                NotifyOfPropertyChange(nameof(Grid));
            }
        }

        public float Unit
        {
            get => unit;
            set
            {
                unit = value;
                RecalculateTotalValues();
                NotifyOfPropertyChange(nameof(Unit));
            }
        }

        public void NormalizeSelf()
        {
            var addUnit = Grid / GridRadix;
            Unit += addUnit;
            Grid = (int)(Grid % GridRadix);

            var diff = Unit - (int)Unit;
            Unit = (int)Unit;
            Grid += (int)(diff * GridRadix);

            if (Grid < 0)
            {
                Grid += (int)GridRadix;
                Unit--;
            }
        }
    }
}
