using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki;

namespace OngekiFumenEditor.Avalonia.Tests.Corpus;

internal sealed class NyagekiCorpusHarness : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly DefaultNyagekiFumenParser parser;
    private readonly CultureInfo previousCulture;
    private readonly CultureInfo previousUiCulture;

    public IReadOnlyList<INyagekiCommandParser> CommandParsers { get; }
    public IFumenSerializable Formatter { get; }

    public NyagekiCorpusHarness()
    {
        var services = new ServiceCollection();
        services.AddOngekiFumenEditorAvalonia();
        serviceProvider = services.BuildServiceProvider();

        CommandParsers = serviceProvider.GetServices<INyagekiCommandParser>().ToArray();
        parser = new DefaultNyagekiFumenParser(CommandParsers);
        Formatter = serviceProvider.GetServices<IFumenSerializable>().Single(x =>
            x.SupportFumenFileExtensions.Contains(".nyageki", StringComparer.OrdinalIgnoreCase));

        previousCulture = CultureInfo.CurrentCulture;
        previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }

    public async Task<OngekiFumen> ParseFileAsync(string chartPath)
    {
        await using var stream = new FileStream(
            chartPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await parser.DeserializeAsync(stream);
    }

    public Task<OngekiFumen> ParseBytesAsync(byte[] bytes) =>
        parser.DeserializeAsync(new MemoryStream(bytes, writable: false));

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
        serviceProvider.Dispose();
    }
}
