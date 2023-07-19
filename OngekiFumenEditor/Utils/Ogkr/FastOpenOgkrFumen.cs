using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Windows;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;

namespace OngekiFumenEditor.Utils.Ogkr
{
    public static class FastOpenOgkrFumen
    {
        public static async Task<bool> TryOpenAsDocument(string ogkrFilePath)
        {
            (var audioFile, var audioDuration) = await GetAudioFilePath(ogkrFilePath);

            if (!File.Exists(audioFile))
            {
                audioFile = FileDialogHelper.OpenFile("手动选择音频文件", IoC.Get<IAudioManager>().SupportAudioFileExtensionList);
                if (!File.Exists(audioFile))
                    return false;
                audioDuration = await CalcAudioDuration(audioFile);
            }

            using var fs = File.OpenRead(ogkrFilePath);
            var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(ogkrFilePath).DeserializeAsync(fs);

            var newProj = new EditorProjectDataModel();
            newProj.FumenFilePath = ogkrFilePath;
            newProj.Fumen = fumen;
            newProj.AudioFilePath = audioFile;
            newProj.AudioDuration = audioDuration;

            var provider = IoC.Get<IFumenVisualEditorProvider>();
            var editor = IoC.Get<IFumenVisualEditorProvider>().Create();
            var viewAware = (IViewAware)editor;
            viewAware.ViewAttached += (sender, e) =>
            {
                var frameworkElement = (FrameworkElement)e.View;

                RoutedEventHandler loadedHandler = null;
                loadedHandler = async (sender2, e2) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    await provider.Open(editor, newProj);
                    var docName = await TryFormatOpenFileName(ogkrFilePath);

                    if (editor is FumenVisualEditorViewModel e)
                        e.SpecifyDefaultName = docName;
                    else
                        editor.DisplayName = docName;
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            return true;
        }


        private static  async Task<string> TryFormatOpenFileName(string ogkrFilePath)
        {
            var result = Path.GetFileName(ogkrFilePath);

            if (ogkrFilePath.EndsWith(".ogkr"))
            {
                var ogkrFileDir = Path.GetDirectoryName(ogkrFilePath);
                var musicXmlFilePath = Path.Combine(ogkrFileDir, "Music.xml");

                //从Music.xml读取musicId
                if (File.Exists(musicXmlFilePath))
                {
                    var musicXml = await XDocument.LoadAsync(File.OpenRead(musicXmlFilePath), LoadOptions.None, default);
                    var element = musicXml.XPathSelectElement(@"//Name[1]/str[1]");
                    if (element?.Value is string name)
                        result = name;
                }
            }

            return "[快速打开] " + result;
        }

        private static async Task<(string, TimeSpan)> GetAudioFilePath(string ogkrFilePath)
        {
            var ogkrFileDir = Path.GetDirectoryName(ogkrFilePath);
            var musicXmlFilePath = Path.Combine(ogkrFileDir, "Music.xml");
            var musicId = -2857;

            if (File.Exists(musicXmlFilePath))
            {
                //从Music.xml读取musicId
                var musicXml = await XDocument.LoadAsync(File.OpenRead(musicXmlFilePath), LoadOptions.None, default);
                var element = musicXml.XPathSelectElement(@"//MusicSourceName[1]/id[1]");
                if (element != null)
                {
                    musicId = int.Parse(element.Value);
                }
            }

            if (musicId < 0)
            {
                //从文件名读取musicId
                var match = new Regex(@"(\d+)_\d+").Match(Path.GetFileNameWithoutExtension(ogkrFilePath));
                if (match.Success)
                {
                    musicId = int.Parse(match.Groups[1].Value);
                }
            }

            if (musicId < 0)
            {
                return default;
            }

            var musicIdStr = musicId < 1000 ? string.Concat("0".Repeat(4 - musicId.ToString().Length)) + musicId : musicId.ToString();

            var musicSourcePath = Path.GetFullPath(Path.Combine(ogkrFileDir, "..", "..", "musicsource", $"musicsource{musicIdStr}"));
            var audioExts = IoC.Get<IAudioManager>().SupportAudioFileExtensionList.Select(x => x.fileExt.TrimStart('.')).ToArray();
            var audioFile = "";

            if (Directory.Exists(musicSourcePath))
            {
                //去对应的musicsource文件夹检查
                audioFile = Directory.GetFiles(musicSourcePath, $"music{musicIdStr}.*").Where(x => audioExts.Any(t => x.EndsWith(t))).FirstOrDefault();
            }

            if (!File.Exists(audioFile))
            {
                return default;
            }

            return (audioFile, await CalcAudioDuration(audioFile));
        }

        private static async Task<TimeSpan> CalcAudioDuration(string audioFilePath)
        {
            using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(audioFilePath);
            return audio.Duration;
        }
    }
}
