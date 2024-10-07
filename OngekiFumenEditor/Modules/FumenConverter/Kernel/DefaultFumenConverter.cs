using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;

namespace OngekiFumenEditor.Modules.FumenConverter.Kernel
{
    [Export(typeof(IFumenConverter))]
    public class DefaultFumenConverter : IFumenConverter
    {
        public async Task<byte[]> ConvertFumenAsync(OngekiFumen fumen, string savePathOrFormat = "ogkr")
        {
            var parserManager = IoC.Get<IFumenParserManager>();

            if (parserManager.GetSerializer(savePathOrFormat) is not IFumenSerializable serializable) {
                throw new FumenConvertException(Resources.OutputFumenNotSupport);
            }
            
            try {
                return await serializable.SerializeAsync(fumen);
            }
            catch (Exception e) {
                throw new FumenConvertException($"{Resources.ConvertFail}{e.Message}");
            }
        }
    }

    public class FumenConvertException : Exception
    {
        public FumenConvertException() { }
        public FumenConvertException(string message) : base(message) { }
        public FumenConvertException(string message, Exception inner) : base(message, inner) { }
    }
}