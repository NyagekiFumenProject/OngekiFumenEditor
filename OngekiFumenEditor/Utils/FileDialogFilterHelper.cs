using Caliburn.Micro;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class FileDialogFilterHelper
    {
        // (".nagekiProj","Nageki谱面文件")[]
        public static string BuildExtensionFilter(IEnumerable<(string ext, string desc)> extParams) => string.Join("|", extParams.Select(x => $"{x.desc} ({x.ext})|*{x.ext}"));
        public static string BuildExtensionFilter(params (string ext, string desc)[] extParams) => BuildExtensionFilter(extParams.AsEnumerable());

        public static string GetSupportFumenFileExtensionFilter()
            => BuildExtensionFilter(IoC.Get<IFumenParserManager>().GetSerializerDescriptions().SelectMany(x => x.fileFormat.Select(y => (y, x.desc))));
        public static string GetSupportAudioFileExtensionFilter()
            => BuildExtensionFilter(IoC.Get<IAudioManager>().SupportAudioFileExtensionList);
    }
}
