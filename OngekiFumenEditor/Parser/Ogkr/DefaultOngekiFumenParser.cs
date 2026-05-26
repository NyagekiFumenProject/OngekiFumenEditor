using OngekiFumenEditor.Base;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.Ogkr
{
	[Export(typeof(IFumenDeserializable))]
	public class DefaultOngekiFumenParser : IFumenDeserializable
	{
		public Dictionary<string, ICommandParser> CommandParsers { get; } = new();

		public static readonly string[] FumenFileExtensions = new[] { ".ogkr" };

		public const string FormatName = "Ongeki Fumen File";

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

			var commandArg = new CommandArgs();

			// 同步 ReadLine 替代 ReadLineAsync:DeserializeAsync 已在调用线程之外执行,
			// 每行一个 Task 的累计开销在大谱面上不可忽略;且整流读取是顺序的,async 无并发收益。
			// 用 Task.Run 把整段读取调度到线程池,避免阻塞 caller 的同步上下文。
			await Task.Run(() =>
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					commandArg.Line = line;

					var cmdName = commandArg.GetData<string>(0);
					if (!string.IsNullOrEmpty(cmdName)
						&& CommandParsers.TryGetValue(cmdName, out var parser))
					{
						if (parser.Parse(commandArg, fumen) is OngekiObjectBase obj)
						{
							genObjList.Add((obj, parser));
							fumen.AddObject(obj);
						}
					}
				}
			});

			foreach (var pair in genObjList)
			{
				pair.parser.AfterParse(pair.obj, fumen);
			}

			fumen.Setup();

			return fumen;
		}
	}
}
