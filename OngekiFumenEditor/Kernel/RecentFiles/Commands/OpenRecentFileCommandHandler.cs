using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.RecentFiles.Commands
{
    [CommandHandler]
    public class OpenRecentFileCommandHandler : ICommandListHandler<OpenRecentFileCommandListDefinition>
    {
        private readonly IEditorRecentFilesManager recentOpenedManager;
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;
        private readonly IShell shell;
        private readonly IEditorProvider[] editorProviders;

        [ImportingConstructor]
        public OpenRecentFileCommandHandler(
            IEditorRecentFilesManager recentOpenedManager,
            IEmbeddedRecommendedScriptService embeddedRecommendedScriptService,
            IShell shell,
            [ImportMany] IEditorProvider[] editorProviders)
        {
            this.recentOpenedManager = recentOpenedManager;
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
            this.shell = shell;
            this.editorProviders = editorProviders;
        }

        public void Populate(Command command, List<Command> commands)
        {
            var recentOpened = recentOpenedManager.RecentRecordInfos.ToArray();
            var duplicatedDisplayNames = recentOpened
                .Where(x => x.Type != RecentOpenType.OpenEmbeddedRecommendedScript)
                .GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < recentOpened.Length; i++)
            {
                var item = recentOpened[i];
                commands.Add(new Command(command.CommandDefinition)
                {
                    Text = item.Type == RecentOpenType.OpenEmbeddedRecommendedScript
                        ? item.DisplayName
                        : BuildRecentFileMenuText(i + 1, item, duplicatedDisplayNames),
                    Tag = item,
                    Enabled = recentOpenedManager.CheckValid(item)
                });
            }
        }

        public async Task Run(Command command)
        {
            var info = command.Tag as RecentRecordInfo;
            Log.LogDebug($"OpenRecentFileCommandHandler.Run() try open recent: {info}");

            switch (info.Type)
            {
                case RecentOpenType.NormalDocumentOpen:
                    await OpenRecentFileByEditorDocument(info);
                    break;
                case RecentOpenType.CommandOpen:
                    await OpenRecentFileByCommandOpen(info);
                    break;
                case RecentOpenType.OpenEmbeddedRecommendedScript:
                    await embeddedRecommendedScriptService.OpenScriptAsync(info.FileName);
                    break;
                default:
                    break;
            }
        }

        private async Task OpenRecentFileByCommandOpen(RecentRecordInfo info)
        {
            var filePath = info.FileName;

            try
            {
                var isSuccess = await DocumentOpenHelper.TryOpenAsDocument(filePath);
                if (!isSuccess)
                {
                    MessageBox.Show(Resources.ErrorEditorNotSupport);
                }
            }
            catch (Exception e)
            {
                var msg = $"{Resources.ErrorOpenRecentFile}{e.Message}";
                Log.LogError(msg);
                MessageBox.Show(msg);
            }
        }

        private async Task OpenRecentFileByEditorDocument(RecentRecordInfo info)
        {
            var pickEditorProvider = editorProviders.FirstOrDefault(x => x.Handles(info.FileName));

            if (pickEditorProvider is null)
            {
                MessageBox.Show(Resources.ErrorEditorNotSupport);
                return;
            }

            var doc = pickEditorProvider.Create();
            var docName = info.DisplayName;
            doc.DisplayName = docName;

            var viewAware = (IViewAware)doc;
            viewAware.ViewAttached += (sender, e) =>
            {
                var frameworkElement = (FrameworkElement)e.View;

                RoutedEventHandler loadedHandler = null;
                loadedHandler = async (sender2, e2) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    await pickEditorProvider.Open(doc, info.FileName);

                    IoC.Get<IEditorRecentFilesManager>().PostRecord(new(info.FileName, docName, RecentOpenType.CommandOpen));
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await shell.OpenDocumentAsync(doc);
        }

        private static string BuildRecentFileMenuText(int index, RecentRecordInfo info, ISet<string> duplicatedDisplayNames)
        {
            var displayName = string.IsNullOrWhiteSpace(info.DisplayName)
                ? Path.GetFileName(info.FileName)
                : info.DisplayName;

            return duplicatedDisplayNames.Contains(info.DisplayName)
                ? $"_{index} {displayName} ({info.FileName})"
                : $"_{index} {displayName}";
        }
    }
}
