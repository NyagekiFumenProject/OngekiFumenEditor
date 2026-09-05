using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki.CommandImpl.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Parser.DefaultImpl.Nyageki
{
	[RegisterSingleton<IFumenDeserializable>]
	public class DefaultNyagekiFumenParser : IFumenDeserializable
	{
		public const string FormatName = "Nyageki Fumen File";
		public string FileFormatName => FormatName;

		public static readonly string[] FumenFileExtensions = new[] { ".nyageki" };
		public string[] SupportFumenFileExtensions => FumenFileExtensions;

    private readonly Dictionary<string, INyagekiCommandParser> commandParsers;

    public DefaultNyagekiFumenParser(IEnumerable<INyagekiCommandParser> commandParsers)
    {
        this.commandParsers = new Dictionary<string, INyagekiCommandParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in commandParsers)
            this.commandParsers[parser.CommandName.Trim()] = parser;
    }

		public async Task<OngekiFumen> DeserializeAsync(Stream stream)
		{
			using var reader = new StreamReader(stream);

			var fumen = new OngekiFumen();
			var replacedImplicitDefaultSoflan = false;

			while (true)
			{
				var line = await reader.ReadLineAsync();
				if (line is null)
					break;

                var seg = line.Split(':', 2);
                var commandName = seg[0].Trim();

				if (commandParsers.TryGetValue(commandName, out var commandParser))
				{
					// A new fumen contains one timing sentinel. Explicit file entries replace it instead of accumulating beside it.
					if (!replacedImplicitDefaultSoflan && commandParser is SoflanCommandParser)
					{
						foreach (var implicitSoflan in fumen.SoflansMap.DefaultSoflanList.ToArray())
							fumen.SoflansMap.Remove(implicitSoflan);
						replacedImplicitDefaultSoflan = true;
					}

					commandParser.ParseAndApply(fumen, seg);
				}
			}

			fumen.Setup();
			return fumen;
		}
	}
}


