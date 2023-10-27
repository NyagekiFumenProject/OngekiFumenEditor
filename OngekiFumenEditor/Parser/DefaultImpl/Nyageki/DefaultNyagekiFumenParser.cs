using OngekiFumenEditor.Base;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
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
			this.commandParsers = commandParsers.ToDictionary(x => x.CommandName.Trim().ToLower(), x => x);
		}

		public async Task<OngekiFumen> DeserializeAsync(Stream stream)
		{
			using var reader = new StreamReader(stream);

			var fumen = new OngekiFumen();

			while (!reader.EndOfStream)
			{
				var line = await reader.ReadLineAsync();
				var seg = line.Split(':', 2);
				var commandName = seg[0].ToLower().Trim();

				if (commandParsers.TryGetValue(commandName, out var commandParser))
					commandParser.ParseAndApply(fumen, seg);
			}

			fumen.Setup();
			return fumen;
		}
	}
}
