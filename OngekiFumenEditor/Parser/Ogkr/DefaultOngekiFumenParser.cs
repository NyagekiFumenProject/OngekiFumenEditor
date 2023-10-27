using OngekiFumenEditor.Base;
using OngekiFumenEditor.Utils.ObjectPool;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl.Ogkr
{
	[Export(typeof(IFumenDeserializable))]
	public class DefaultOngekiFumenParser : IFumenDeserializable
	{
		public Dictionary<string, ICommandParser> CommandParsers { get; } = new();

		public static readonly string[] FumenFileExtensions = new[] { ".ogkr" };

		public const string FormatName = "OngekiFumenFile";

		public string[] SupportFumenFileExtensions => FumenFileExtensions;

		public string FileFormatName => FormatName;

		[ImportingConstructor]
		public DefaultOngekiFumenParser([ImportMany] IEnumerable<ICommandParser> commandParsers)
		{
			foreach (var pair in commandParsers.GroupBy(x => x.CommandLineHeader))
			{
				CommandParsers[pair.Key] = pair.FirstOrDefault();
			}
		}

		public async Task<OngekiFumen> DeserializeAsync(Stream stream)
		{
			var reader = new StreamReader(stream);
			var genObjList = new List<(OngekiObjectBase obj, ICommandParser parser)>();
			var fumen = new OngekiFumen();

			var commandArg = ObjectPool<CommandArgs>.Get();

			while (!reader.EndOfStream)
			{
				var line = await reader.ReadLineAsync();
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

			ObjectPool<CommandArgs>.Return(commandArg);

			foreach (var pair in genObjList)
			{
				pair.parser.AfterParse(pair.obj, fumen);
			}

			fumen.Setup();

			return fumen;
		}
	}
}
