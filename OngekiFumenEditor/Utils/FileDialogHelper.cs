using Caliburn.Micro;
using Microsoft.Win32;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class FileDialogHelper
    {
        // (".nagekiProj","Nageki谱面文件")[]
        public static string BuildExtensionFilter(IEnumerable<(string ext, string desc)> extParams) => string.Join("|", extParams.Select(x => $"{x.desc} ({x.ext})|*{x.ext}"));
        public static string BuildExtensionFilter(params (string ext, string desc)[] extParams) => BuildExtensionFilter(extParams.AsEnumerable());

        private static string BuildExtensionFilterAndAll(IEnumerable<(string ext, string desc)> extParams)
            => $"全支持文件 *.*|{string.Join(";", extParams.Select(x => $"*{x.ext}"))}|" + BuildExtensionFilter(extParams);

        public static string GetSupportFumenFileExtensionFilter()
            => BuildExtensionFilter(IoC.Get<IFumenParserManager>().GetSerializerDescriptions().SelectMany(x => x.fileFormat.Select(y => (y, x.desc))));
        public static string GetSupportAudioFileExtensionFilter()
            => BuildExtensionFilter(IoC.Get<IAudioManager>().SupportAudioFileExtensionList);

        public static string OpenFile(string title, IEnumerable<(string ext, string desc)> extParams)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = title;
            dialog.Filter = BuildExtensionFilterAndAll(extParams);
            dialog.Multiselect = false;
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() == true)
                return dialog.FileName;
            return default;
        }

        public static string SaveFile(string title, IEnumerable<(string ext, string desc)> extParams)
        {
            var dialog = new SaveFileDialog();
            dialog.Title = title;
            dialog.Filter = BuildExtensionFilterAndAll(extParams);

            if (dialog.ShowDialog() == true)
                return dialog.FileName;
            return default;
        }
    }
}
