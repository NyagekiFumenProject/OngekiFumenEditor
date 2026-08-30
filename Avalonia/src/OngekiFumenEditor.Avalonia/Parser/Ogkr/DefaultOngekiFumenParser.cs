using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Base;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Avalonia.Parser.Ogkr
{
	[RegisterSingleton<IFumenDeserializable>]
	public class DefaultOngekiFumenParser : IFumenDeserializable
	{
		public Dictionary<string, ICommandParser> CommandParsers { get; } = new();
		private readonly IReadOnlyList<IArgValueConverter> argValueConverters;

		public static readonly string[] FumenFileExtensions = new[] { ".ogkr" };

		public const string FormatName = "Ongeki Fumen File";

		public string[] SupportFumenFileExtensions => FumenFileExtensions;

		public string FileFormatName => FormatName;

		public DefaultOngekiFumenParser(IEnumerable<ICommandParser> commandParsers)
			: this(commandParsers, CommandArgs.CreateDefaultConverters())
		{
		}

		public DefaultOngekiFumenParser(
			IEnumerable<ICommandParser> commandParsers,
			IEnumerable<IArgValueConverter> argValueConverters)
		{
			foreach (var pair in commandParsers.GroupBy(x => x.CommandLineHeader))
			{
				CommandParsers[pair.Key] = pair.FirstOrDefault();
			}

			this.argValueConverters = argValueConverters.ToArray();
		}

		public async Task<OngekiFumen> DeserializeAsync(Stream stream)
		{
			var reader = new StreamReader(stream);
			var genObjList = new List<(OngekiObjectBase obj, ICommandParser parser)>();
			var fumen = new OngekiFumen();

			var commandArg = new CommandArgs(argValueConverters);

			while (true)
			{
				var line = await reader.ReadLineAsync();
				if (line is null)
					break;

				commandArg.Line = line;

				var cmdName = commandArg.GetData<string>(0)?.Trim();
				if (cmdName != null && CommandParsers.TryGetValue(cmdName, out var parser))
				{
					if (parser.Parse(commandArg, fumen) is OngekiObjectBase obj)
					{
						genObjList.Add((obj, parser));
						fumen.AddObject(obj);
					}
				}
			}

			foreach (var pair in genObjList)
			{
				pair.parser.AfterParse(pair.obj, fumen);
			}

			fumen.Setup();

			return fumen;
		}
	}
}


