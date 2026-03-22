using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace OngekiFumenEditor.Parser.DefaultImpl.Nyageki
{
	[Export(typeof(IFumenParserManager))]
	internal class DefaultFumenParserManager : IFumenParserManager
	{
		[ImportMany]
		public List<IFumenSerializable> FumenSerializers { get; set; }
		[ImportMany]
		public List<IFumenDeserializable> FumenDeserializers { get; set; }

		public IFumenDeserializable GetDeserializer(string loadFilePath) => FumenDeserializers.FirstOrDefault(x => x.SupportFumenFileExtensions.Any(y => loadFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));

		public IEnumerable<(string desc, string[] fileFormat)> GetDeserializerDescriptions()
			=> FumenDeserializers.Select(x => (x.FileFormatName, x.SupportFumenFileExtensions));

		public IFumenSerializable GetSerializer(string saveFilePath) => FumenSerializers.FirstOrDefault(x => x.SupportFumenFileExtensions.Any(y => saveFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));

		public IEnumerable<(string desc, string[] fileFormat)> GetSerializerDescriptions()
			=> FumenSerializers.Select(x => (x.FileFormatName, x.SupportFumenFileExtensions));
	}
}
