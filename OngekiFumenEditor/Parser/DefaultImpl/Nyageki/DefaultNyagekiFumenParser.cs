using OngekiFumenEditor.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki
{
	[Export(typeof(IFumenDeserializable))]
	public class DefaultNyagekiFumenParser : IFumenDeserializable
	{
		public const string FormatName = "Nyageki Fumen File";
		public string FileFormatName => FormatName;

		public static readonly string[] FumenFileExtensions = new[] { ".nyageki" };
		public string[] SupportFumenFileExtensions => FumenFileExtensions;

		Dictionary<string, INyagekiCommandParser> commandParsers;

		[ImportingConstructor]
		public DefaultNyagekiFumenParser([ImportMany] IEnumerable<INyagekiCommandParser> commandParsers)
		{
			this.commandParsers = new Dictionary<string, INyagekiCommandParser>(StringComparer.OrdinalIgnoreCase);
			foreach (var parser in commandParsers)
				this.commandParsers[parser.CommandName.Trim()] = parser;
		}

		public async Task<OngekiFumen> DeserializeAsync(Stream stream)
		{
			using var reader = new StreamReader(stream);

			var fumen = new OngekiFumen();

			while (true)
			{
				var line = await reader.ReadLineAsync();
				if (line is null)
					break;

				var seg = line.Split(':', 2);
				var commandName = seg[0].Trim();

				if (commandParsers.TryGetValue(commandName, out var commandParser))
					commandParser.ParseAndApply(fumen, seg);
			}

			fumen.Setup();
			return fumen;
		}
	}
}
