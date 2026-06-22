using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Services;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Commands
{
    [CommandHandler]
    public sealed class NewScriptCommandHandler : CommandHandlerBase<NewScriptCommandDefinition>
    {
        private int newScriptCounter = 1;

        private readonly IShell shell;
        private readonly IEditorScriptDocumentProvider scriptDocumentProvider;

        [ImportingConstructor]
        public NewScriptCommandHandler(IShell shell, IEditorScriptDocumentProvider scriptDocumentProvider)
        {
            this.shell = shell;
            this.scriptDocumentProvider = scriptDocumentProvider;
        }

        public override async Task Run(Command command)
        {
            var document = scriptDocumentProvider.Create();
            var viewAware = (IViewAware)document;
            viewAware.ViewAttached += (sender, e) =>
            {
                var frameworkElement = (FrameworkElement)e.View;

                RoutedEventHandler loadedHandler = null;
                loadedHandler = async (sender2, e2) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    await scriptDocumentProvider.New(document, $"Untitled{newScriptCounter++}{EditorScriptDocumentProvider.FILE_EXTENSION_NAME}");
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await shell.OpenDocumentAsync(document);
        }
    }

    [CommandHandler]
    public sealed class RecommendedScriptsCommandHandler : CommandHandlerBase<RecommendedScriptsCommandDefinition>
    {
        public override void Update(Command command)
        {
            command.Enabled = true;
        }

        public override Task Run(Command command)
        {
            return Task.CompletedTask;
        }
    }

    [CommandHandler]
    public sealed class RecentScriptsCommandHandler : CommandHandlerBase<RecentScriptsCommandDefinition>
    {
        public override void Update(Command command)
        {
            command.Enabled = true;
        }

        public override Task Run(Command command)
        {
            return Task.CompletedTask;
        }
    }

    [CommandHandler]
    public sealed class OpenRecommendedScriptCommandHandler : ICommandListHandler<OpenRecommendedScriptCommandListDefinition>
    {
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;

        [ImportingConstructor]
        public OpenRecommendedScriptCommandHandler(IEmbeddedRecommendedScriptService embeddedRecommendedScriptService)
        {
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
        }

        public void Populate(Command command, List<Command> commands)
        {
            var scripts = embeddedRecommendedScriptService.GetScripts().ToArray();
            if (!scripts.Any())
            {
                commands.Add(CreateEmptyCommand(command));
                return;
            }

            foreach (var script in scripts)
            {
                commands.Add(new Command(command.CommandDefinition)
                {
                    Text = script.DisplayName,
                    Tag = script,
                });
            }
        }

        public async Task Run(Command command)
        {
            if (command?.Tag is not EmbeddedRecommendedScriptInfo script)
                return;

            try
            {
                await embeddedRecommendedScriptService.OpenScriptAsync(script.ResourceName);
            }
            catch (Exception e)
            {
                var message = $"{ScriptMenuResources.EmbeddedRecommendedScriptOpenFailed}{Environment.NewLine}{e.Message}";
                Log.LogError(message);
                MessageBox.Show(message, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static Command CreateEmptyCommand(Command parentCommand)
        {
            return new Command(parentCommand.CommandDefinition)
            {
                Text = ScriptMenuResources.NoScriptsFound,
                Enabled = false,
            };
        }
    }

    [CommandHandler]
    public sealed class OpenRecentScriptCommandHandler : ICommandListHandler<OpenRecentScriptCommandListDefinition>
    {
        private readonly IEditorRecentFilesManager recentFilesManager;
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;

        [ImportingConstructor]
        public OpenRecentScriptCommandHandler(
            IEditorRecentFilesManager recentFilesManager,
            IEmbeddedRecommendedScriptService embeddedRecommendedScriptService)
        {
            this.recentFilesManager = recentFilesManager;
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
        }

        public void Populate(Command command, List<Command> commands)
        {
            var recentScripts = recentFilesManager.RecentRecordInfos
                .Where(IsScriptRecord)
                .ToArray();

            if (!recentScripts.Any())
            {
                commands.Add(CreateEmptyCommand(command));
                return;
            }

            var ordinaryIndex = 1;
            foreach (var info in recentScripts)
            {
                var isEmbeddedRecommended = info.Type == RecentOpenType.OpenEmbeddedRecommendedScript;
                commands.Add(new Command(command.CommandDefinition)
                {
                    Text = isEmbeddedRecommended ? info.DisplayName : $"_{ordinaryIndex++} {info.DisplayName} ({info.FileName})",
                    Tag = info,
                    Enabled = recentFilesManager.CheckValid(info),
                });
            }
        }

        public async Task Run(Command command)
        {
            if (command?.Tag is not RecentRecordInfo info || !recentFilesManager.CheckValid(info))
                return;

            try
            {
                if (info.Type == RecentOpenType.OpenEmbeddedRecommendedScript)
                {
                    await embeddedRecommendedScriptService.OpenScriptAsync(info.FileName);
                    return;
                }

                var isSuccess = await DocumentOpenHelper.TryOpenAsDocument(info.FileName);
                if (!isSuccess)
                    MessageBox.Show(Resources.ErrorEditorNotSupport, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception e)
            {
                var message = $"{Resources.ErrorOpenRecentFile}{e.Message}";
                Log.LogError(message);
                MessageBox.Show(message, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static bool IsScriptRecord(RecentRecordInfo info)
        {
            return info.Type == RecentOpenType.OpenEmbeddedRecommendedScript
                   || string.Equals(Path.GetExtension(info.FileName), EditorScriptDocumentProvider.FILE_EXTENSION_NAME, StringComparison.OrdinalIgnoreCase);
        }

        private static Command CreateEmptyCommand(Command parentCommand)
        {
            return new Command(parentCommand.CommandDefinition)
            {
                Text = ScriptMenuResources.NoScriptsFound,
                Enabled = false,
            };
        }
    }
}
