using System.Collections;
using System.Globalization;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Parser.Ogkr;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.Parser;

public sealed class CommandArgsTests
{
    [Fact]
    public void PrimitiveConverters_UseInvariantCultureAndMapInvalidTokensToDefaults()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        try
        {
            var args = new CommandArgs
            {
                Line = "CMD\t1.5\tinvalid"
            };

            Assert.Equal(new[] { 0f, 1.5f, 0f }, args.GetDataArray<float>());
            Assert.Equal(new[] { 0d, 1.5d, 0d }, args.GetDataArray<double>());
            Assert.Equal(new[] { 1.5d, 0d }, ParserUtils.GetDataArray<double>("CMD 1.5 invalid"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void SameLine_CanBeReadAsEveryBuiltInType()
    {
        var args = new CommandArgs
        {
            Line = "CMD\t42\t9000000000\tTRUE\tinvalid\ttext"
        };

        Assert.Equal(new[] { 0, 42, 0, 0, 0, 0 }, args.GetDataArray<int>());
        Assert.Equal(new[] { 0L, 42L, 9_000_000_000L, 0L, 0L, 0L }, args.GetDataArray<long>());
        Assert.Equal(new[] { false, false, false, true, false, false }, args.GetDataArray<bool>());
        Assert.Equal(new[] { "CMD", "42", "9000000000", "TRUE", "invalid", "text" }, args.GetDataArray<string>());
    }

    [Fact]
    public void ChangingLine_InvalidatesTypedAndTokenCaches()
    {
        var args = new CommandArgs
        {
            Line = "FIRST\t1"
        };
        var firstTyped = args.GetDataArray<int>();
        Assert.Equal("FIRST", args.GetRawData(0));

        args.Line = "SECOND\t2";
        var secondTyped = args.GetDataArray<int>();

        Assert.NotSame(firstTyped, secondTyped);
        Assert.Equal(new[] { 0, 2 }, secondTyped);
        Assert.Equal("SECOND", args.GetRawData(0));
        Assert.Equal("2", args.GetRawData(1));
    }

    [Fact]
    public void RawAndStringArrays_DoNotExposeTheTokenCache()
    {
        var args = new CommandArgs
        {
            Line = "CMD\t7"
        };

        var raw = args.GetRawDataArray();
        raw[0] = "CHANGED";
        raw[1] = "99";

        var strings = args.GetDataArray<string>();
        strings[1] = "MUTATED";

        Assert.Equal("CMD", args.GetRawData(0));
        Assert.Equal("7", args.GetRawData(1));
        Assert.Equal(7, args.GetData<int>(1));
    }

    [Fact]
    public void FirstRegisteredCustomConverter_TakesPrecedenceOverPrimitiveFastPath()
    {
        var args = new CommandArgs(new IArgValueConverter[]
        {
            new SentinelIntConverter(73),
            new SentinelIntConverter(99)
        })
        {
            Line = "CMD\t1"
        };

        Assert.Equal(new[] { 73, 73 }, args.GetDataArray<int>());
    }

    [Fact]
    public void UnregisteredType_ThrowsExistingDiagnostic()
    {
        var args = new CommandArgs
        {
            Line = "CMD\t1"
        };

        void Act() => args.GetDataArray<decimal>();

        var exception = Assert.Throws<InvalidOperationException>(Act);
        Assert.Equal("No OGKR argument converter is registered for 'System.Decimal'.", exception.Message);
    }

    private sealed class SentinelIntConverter(int sentinel) : IArgValueConverter
    {
        public Type ConvertType => typeof(int);

        public IEnumerable Parser(IEnumerable<string> inputs)
        {
            foreach (var _ in inputs)
                yield return sentinel;
        }
    }
}
