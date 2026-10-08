using Caliburn.Micro;
using Gemini.Framework.Results;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Base;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OngekiFumenEditor.Utils
{
    internal static partial class DocumentOpenHelper
    {
        [GeneratedRegex(@"(\d+)_\d+")]
        private static partial Regex MusicIdFromFileNameRegex();

        private static readonly object inFlightOpensLock = new();
        private static readonly Dictionary<string, Task<bool>> inFlightOpens = new();

        /// <summary>
        /// 同一目标的并发打开共享同一笔在途加载：先到者真正执行，后来者 await 同一结果。
        /// 否则第二次打开会各自 Begin 一个加载会话，随后因文档/编辑器冲突半途搁置——
        /// 它的 Completion 永不兑现（调用方挂死），加载对话框也没人收尾。
        /// </summary>
        private static async Task<bool> RunSharedOpen(string key, Func<Task<bool>> open)
        {
            Task<bool> pending;
            var owner = false;
            lock (inFlightOpensLock)
            {
                if (inFlightOpens.TryGetValue(key, out pending))
                {
                    Log.LogInfo($"Join the in-flight open for '{key}'.");
                }
                else
                {
                    pending = open();
                    inFlightOpens[key] = pending;
                    owner = true;
                }
            }

            try
            {
                return await pending;
            }
            finally
            {
                if (owner)
                {
                    lock (inFlightOpensLock)
                        inFlightOpens.Remove(key);
                }
            }
        }

        public static Task<bool> TryOpenAsDocument(string filePath) =>
            RunSharedOpen($"doc:{Path.GetFullPath(filePath)}", () => OpenAsDocumentCore(filePath));

        private static async Task<bool> OpenAsDocumentCore(string filePath)
        {
            if (IoC.GetAll<IEditorProvider>().FirstOrDefault(x => x.Handles(filePath)) is IEditorProvider provider)
            {
                Log.LogInfo($"通过命令行快速打开文档:({provider}) {filePath}");
                await Dispatcher.Yield();
                var openDocument = Show.Document(filePath);
                await Coroutine.ExecuteAsync(new IResult[] { openDocument }.AsEnumerable().GetEnumerator());
                return true;
            }
            else if (filePath.EndsWith(".ogkr") || filePath.EndsWith(".nyageki"))
            {
                return await TryOpenOgkrFileAsDocument(filePath);
            }

            return false;
        }

        public static Task<bool> TryOpenOgkrFileAsDocument(string ogkrFilePath) =>
            RunSharedOpen($"ogkr:{Path.GetFullPath(ogkrFilePath)}", () => OpenOgkrFileAsDocumentCore(ogkrFilePath));

        private static async Task<bool> OpenOgkrFileAsDocumentCore(string ogkrFilePath)
        {
            var docName = await TryFormatOpenFileName(ogkrFilePath);
            using var session = EditorLoadingSession.Begin(EditorLoadingStep.Preparing, docName);
            try
            {
                var newProj = await TryCreateEditorProjectDataModel(ogkrFilePath, session);
                if (newProj is null)
                    return false;
                session.CancellationToken.ThrowIfCancellationRequested();

                var editor = IoC.Get<IFumenVisualEditorProvider>().Create();
                editor.DisplayName = docName;

                var viewAware = (IViewAware)editor;
                viewAware.ViewAttached += (sender, e) =>
                {
                    var frameworkElement = (FrameworkElement)e.View;

                    RoutedEventHandler loadedHandler = null;
                    loadedHandler = async (sender2, e2) =>
                    {
                        frameworkElement.Loaded -= loadedHandler;
                        await IoC.Get<IFumenVisualEditorProvider>().Open(editor, newProj, session);

                        IoC.Get<IEditorRecentFilesManager>().PostRecord(new(ogkrFilePath, docName, RecentOpenType.CommandOpen));
                    };
                    frameworkElement.Loaded += loadedHandler;
                };

                await IoC.Get<IShell>().OpenDocumentAsync(editor);
                var outcome = await session.Completion;
                return outcome == EditorLoadingOutcome.Ready;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public static Task<bool> TryOpenProject(EditorProjectDataModel proj) =>
            RunSharedOpen(BuildProjectOpenKey(proj), () => OpenProjectCore(proj));

        private static string BuildProjectOpenKey(EditorProjectDataModel proj)
        {
            var fumen = string.IsNullOrWhiteSpace(proj?.FumenFilePath) ? "(new)" : Path.GetFullPath(proj.FumenFilePath);
            var audio = string.IsNullOrWhiteSpace(proj?.AudioFilePath) ? "(none)" : Path.GetFullPath(proj.AudioFilePath);
            return $"proj:{fumen}|{audio}";
        }

        private static async Task<bool> OpenProjectCore(EditorProjectDataModel proj)
        {
            var targetName = string.IsNullOrWhiteSpace(proj?.FumenFilePath)
                ? Resources.EditorLoadingNewProjectName
                : Path.GetFileName(proj.FumenFilePath);
            using var session = EditorLoadingSession.Begin(EditorLoadingStep.Preparing, targetName);
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
                    await fumenProvider.Open(editor, proj, session);
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await IoC.Get<IShell>().OpenDocumentAsync(editor);
            var outcome = await session.Completion;
            return outcome == EditorLoadingOutcome.Ready;
        }

        public static async Task<EditorProjectDataModel> TryCreateEditorProjectDataModel(string ogkrFilePath, EditorLoadingSession session)
        {
            (var audioFile, var audioDuration) = await GetAudioFilePath(ogkrFilePath);
            session.CancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(audioFile))
            {
                session.ReportStep(EditorLoadingStep.SelectingAudio);
                audioFile = FileDialogHelper.OpenFile(Resources.SelectAudioFileManually, IoC.Get<IAudioManager>().SupportAudioFileExtensionList);
                session.CancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(audioFile))
                    return null;
                audioDuration = await IoC.Get<IAudioManager>().GetAudioDurationAsync(audioFile, session.CancellationToken);
            }

            session.ReportStep(EditorLoadingStep.Parsing);
            session.CancellationToken.ThrowIfCancellationRequested();
            using var fs = File.OpenRead(ogkrFilePath);
            var fumen = await IoC.Get<IFumenParserManager>().GetDeserializer(ogkrFilePath).DeserializeAsync(fs, session.CancellationToken);
            Log.LogInfo($"Fumen file loaded: {ogkrFilePath}");

            var newProj = new EditorProjectDataModel();
            newProj.FumenFilePath = ogkrFilePath;
            newProj.Fumen = fumen;
            newProj.AudioFilePath = audioFile;
            newProj.AudioDuration = audioDuration;

            return newProj;
        }

        public static async Task<string> TryFormatOpenFileName(string ogkrFilePath)
        {
            var result = Path.GetFileName(ogkrFilePath);

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

            return $"[{Resources.FastOpen}] " + result;
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
                var match = MusicIdFromFileNameRegex().Match(Path.GetFileNameWithoutExtension(ogkrFilePath));
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

            var musicSourceFolder = Path.GetFullPath(Path.Combine(ogkrFileDir, "..", "..", "musicsource", $"musicsource{musicIdStr}"));
            var audioExts = IoC.Get<IAudioManager>().SupportAudioFileExtensionList.Select(x => x.fileExt.TrimStart('.')).ToArray();
            var audioFile = "";

            if (!Directory.Exists(musicSourceFolder))
            {
                var idx = ogkrFileDir.LastIndexOf("/package");
                idx = idx < 0 ? ogkrFileDir.LastIndexOf("\\package") : idx;
                //check if ogkr file is in hdd folder.
                if (idx >= 0)
                {
                    var packageFolder = ogkrFilePath.Substring(0, "/package".Length + idx);
                    musicSourceFolder = Directory.GetDirectories(packageFolder, $"musicsource{musicIdStr}", SearchOption.AllDirectories).FirstOrDefault();
                }
            }

            if (Directory.Exists(musicSourceFolder))
            {
                //去对应的musicsource文件夹检查
                audioFile = Directory.GetFiles(musicSourceFolder, $"music{musicIdStr}.*").Where(x => audioExts.Any(t => x.EndsWith(t))).FirstOrDefault();
            }

            if (!File.Exists(audioFile))
                return default;

            return (audioFile, await IoC.Get<IAudioManager>().GetAudioDurationAsync(audioFile));
        }
    }
}
