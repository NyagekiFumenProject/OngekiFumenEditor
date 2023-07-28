using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Microsoft.Win32;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Base.OngekiObjects.Lane;
using OngekiFumenEditor.Base.OngekiObjects.Lane.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.OngekiObjects;
using OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.ObjectPool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Base.EditorProjectDataUtils;
using System.Xml.Linq;
using System.Xml.XPath;
using AngleSharp.Browser.Dom;
using DereTore.Exchange.Archive.ACB;
using System.Diagnostics;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Kernel.Audio;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser.ViewModels
{
    [Export(typeof(IOgkiFumenListBrowser))]
    public class OgkiFumenListBrowserViewModel : WindowBase, IOgkiFumenListBrowser
    {
        private readonly static EnumerationOptions enumOpt = new EnumerationOptions()
        {
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        private string rootFolderPath = string.Empty;
        public string RootFolderPath
        {
            get => rootFolderPath;
            set
            {
                Set(ref rootFolderPath, value);
                RefreshList();
            }
        }

        private bool isBusy = false;
        public bool IsBusy
        {
            get => isBusy;
            set
            {
                Set(ref isBusy, value);
            }
        }

        public ObservableCollection<OngekiFumenSet> FumenSets { get; } = new ObservableCollection<OngekiFumenSet>();

        public OgkiFumenListBrowserViewModel()
        {
            DisplayName = "音击谱面库浏览器";
        }

        private async void RefreshList()
        {
            IsBusy = true;
            FumenSets.Clear();
            var resourceMap = new Dictionary<string, string>();
            var folder = await BuildFolder(RootFolderPath, resourceMap);

            IEnumerable<OngekiFumenSet> GetSet(Folder folder) =>
                folder.FumenSets.Concat(folder.SubFolders.SelectMany(x => GetSet(x)));

            Parallel.ForEach(GetSet(folder), s =>
            {
                s.JacketFilePath = resourceMap.TryGetValue("asset_" + s.MusicId, out var j) ? j : s.JacketFilePath;
                s.AudioFilePath = resourceMap.TryGetValue("audio_" + s.MusicSourceId, out var a) ? a : s.AudioFilePath;
            });

            FumenSets.AddRange(GetSet(folder));
            IsBusy = false;
        }

        public void SelectFolder()
        {
            if (!FileDialogHelper.OpenDirectory("选择游戏根目录", out var folderPath))
                return;

            RootFolderPath = folderPath;
        }

        public async Task<Folder> BuildFolder(string folderPath, Dictionary<string, string> resourceMap)
        {
            var folder = new Folder();
            folder.Name = Path.GetFileName(folderPath);

            var subFolderPaths = Directory.GetDirectories(folderPath);
            foreach (var subFolderPath in subFolderPaths)
            {
                var subFolder = await BuildFolder(subFolderPath, resourceMap);
                if (!subFolder.IsEmpty)
                    folder.SubFolders.Add(subFolder);
            }

            var folderName = Path.GetFileName(folderPath).ToLowerInvariant();

            if (folderName == "music")
            {
                var musicFilePaths = Directory.GetFiles(folderPath, "Music.xml", SearchOption.AllDirectories);
                foreach (var musicFilePath in musicFilePaths)
                {
                    if ((await BuildFumenSet(musicFilePath)) is OngekiFumenSet set)
                        folder.FumenSets.Add(set);
                }
            }
            else if (folderName == "musicsource")
            {
                var musicSourceFilePaths = Directory.GetFiles(folderPath, "MusicSource.xml", SearchOption.AllDirectories);
                foreach (var musicSourceFilePath in musicSourceFilePaths)
                {
                    await BuildMusicSource(musicSourceFilePath, resourceMap);
                }
            }
            else if (folderName == "assets")
            {
                var files = Directory.GetFiles(folderPath, "ui_jacket_*");
                foreach (var file in files)
                    BuildAssets(file, resourceMap);
            }

            return folder;
        }

        private void BuildAssets(string file, Dictionary<string, string> resourceMap)
        {
            if (!int.TryParse(Path.GetFileName(file).Replace("ui_jacket_", string.Empty), out var id))
                return;
            resourceMap["asset_" + id] = file;
        }

        private async Task BuildMusicSource(string musicSourceFilePath, Dictionary<string, string> resourceMap)
        {
            using var fs = File.OpenRead(musicSourceFilePath);
            var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

            string GetString(string name, string sub = "str")
            {
                var element = musicXml.XPathSelectElement($@"//{name}[1]/{sub}[1]");
                if (element?.Value is string strValue)
                    return strValue;
                return string.Empty;
            }

            if (!int.TryParse(GetString("Name", "id"), out var id))
                return;

            var acbFileName = GetString("acbFile", "path");
            var acbFilePath = Path.Combine(Path.GetDirectoryName(musicSourceFilePath), acbFileName);

            if (!File.Exists(acbFilePath))
                return;

            resourceMap["audio_" + id] = acbFilePath;
        }

        private async Task<OngekiFumenSet> BuildFumenSet(string musicXmlFilePath)
        {
            if (!File.Exists(musicXmlFilePath))
                return null;

            using var fs = File.OpenRead(musicXmlFilePath);
            var musicXml = await XDocument.LoadAsync(fs, LoadOptions.None, default);

            string GetString(string name)
            {
                var element = musicXml.XPathSelectElement($@"//{name}[1]/str[1]");
                if (element?.Value is string strValue)
                    return strValue;
                return string.Empty;
            }

            int GetId(string name)
            {
                var element = musicXml.XPathSelectElement($@"//{name}[1]/id[1]");
                if (element?.Value is string strValue)
                    return int.Parse(strValue);
                return 0;
            }

            var set = new OngekiFumenSet();
            set.Title = GetString("Name");
            set.Artist = GetString("ArtistName");
            set.Genre = GetString("Genre");
            set.MusicId = GetId("Name");
            set.MusicSourceId = GetId("MusicSourceName");

            var folderPath = Path.GetDirectoryName(musicXmlFilePath);

            foreach ((var fumenDataElement, var idx) in musicXml.XPathSelectElements("/MusicData/FumenData/FumenData").WithIndex())
            {
                string fumenConstIntegerPart = fumenDataElement.Element("FumenConstIntegerPart").Value;
                string fumenConstFractionalPart = fumenDataElement.Element("FumenConstFractionalPart").Value;
                string fumenFileName = fumenDataElement.Element("FumenFile").Element("path")?.Value;

                var fumenFilePath = Path.Combine(folderPath, fumenFileName);
                if (!File.Exists(fumenFilePath))
                    continue;
                var fumenDiff = new OngekiFumenDiff(set);
                fumenDiff.DiffIdx = idx;
                fumenDiff.FilePath = fumenFilePath;
                fumenDiff.Level = (int.TryParse(fumenConstIntegerPart, out var d1) ? d1 : 0) + ((int.TryParse(fumenConstFractionalPart, out var d2) ? d2 : 0) / 100.0f);

                set.Difficults.Add(fumenDiff);
            }

            return set.Difficults.Count > 0 ? set : default;
        }

        public async void LoadFumen(OngekiFumenDiff diff)
        {
            IsBusy = true;
            using var fs = File.OpenRead(diff.FilePath);
            var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(diff.FilePath).DeserializeAsync(fs);

            var newProj = new EditorProjectDataModel();
            newProj.FumenFilePath = diff.FilePath;
            newProj.Fumen = fumen;
            newProj.AudioFilePath = diff.RefSet.AudioFilePath;

            using var audio = await IoC.Get<IAudioManager>().LoadAudioAsync(diff.RefSet.AudioFilePath);
            if (audio is null)
            {
                MessageBox.Show($"无法打开{diff.RefSet.Title},找不到音频文件");
                return;
            }
            newProj.AudioDuration = audio.Duration;

            var fumenProvider = IoC.Get<IFumenVisualEditorProvider>();
            var editor = IoC.Get<IFumenVisualEditorProvider>().Create();
            var viewAware = (IViewAware)editor;
            viewAware.ViewAttached += (sender, e) =>
            {
                var frameworkElement = (FrameworkElement)e.View;

                RoutedEventHandler loadedHandler = null;
                loadedHandler = async (sender2, e2) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    await fumenProvider.Open(editor, newProj);
                    var docName = $"[快速打开] {diff.RefSet.Title}";

                    editor.DisplayName = docName;
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            IsBusy = false;
        }
    }
}
