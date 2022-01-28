using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    [Export(typeof(IFumenParserManager))]
    internal class DefaultFumenParserManager : IFumenParserManager
    {
        [ImportMany]
        public List<IFumenSerializable> FumenSerializers { get; set; }
        [ImportMany]
        public List<IFumenDeserializable> FumenDeserializers { get; set; }

        public IFumenDeserializable GetDeserializer(string loadFilePath) => FumenDeserializers.FirstOrDefault(x => x.SupportFumenFileExtensions.Any(y => loadFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));
        public IFumenSerializable GetSerializer(string saveFilePath) => FumenSerializers.FirstOrDefault(x => x.SupportFumenFileExtensions.Any(y => saveFilePath.EndsWith(y, StringComparison.InvariantCultureIgnoreCase)));
    }
}
