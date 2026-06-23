using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Services;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Commands
{
    internal enum ScriptMenuSourceKind
    {
        EmbeddedRecommendedScript,
        FileScript,
    }

    internal enum ScriptMenuRunTargetKind
    {
        CurrentActivingEditor,
        NoEditor,
        Editor,
    }

    internal sealed record ScriptMenuSourceInfo(
        ScriptMenuSourceKind Kind,
        string SourceId,
        string MenuText,
        string ScriptDisplayName,
        string LogSourceId,
        RecentRecordInfo RecentRecord = default,
        EmbeddedRecommendedScriptInfo EmbeddedScript = default);

    internal sealed record ScriptMenuRunTargetInfo(ScriptMenuSourceInfo Script, ScriptMenuRunTargetKind Kind, FumenVisualEditorViewModel Editor = default);

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
    public sealed class OpenRecommendedScriptCommandHandler : IDynamicMenuHandler<OpenRecommendedScriptCommandListDefinition>
    {
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;
        private readonly ICommandService commandService;

        [ImportingConstructor]
        public OpenRecommendedScriptCommandHandler(
            IEmbeddedRecommendedScriptService embeddedRecommendedScriptService,
            ICommandService commandService)
        {
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
            this.commandService = commandService;
        }

        public void Populate(Command command, List<DynamicMenuItem> menuItems)
        {
            var scripts = embeddedRecommendedScriptService.GetScripts().ToArray();
            if (!scripts.Any())
            {
                menuItems.Add(DynamicMenuItem.FromCommand(CreateEmptyCommand(command)));
                return;
            }

            foreach (var script in scripts)
            {
                var source = new ScriptMenuSourceInfo(
                    ScriptMenuSourceKind.EmbeddedRecommendedScript,
                    script.ResourceName,
                    script.DisplayName,
                    script.FileName,
                    script.ResourceName,
                    EmbeddedScript: script);

                var isValid = embeddedRecommendedScriptService.Contains(script.ResourceName);
                var scriptCommand = new Command(command.CommandDefinition)
                {
                    Text = source.MenuText,
                    Tag = source,
                    Enabled = isValid,
                };
                menuItems.Add(isValid
                    ? DynamicMenuItem.FromCommand(scriptCommand, ScriptMenuCommandFactory.CreateActionMenuItems(commandService, source))
                    : DynamicMenuItem.FromCommand(scriptCommand));
            }
        }

        public Task Run(Command command) => Task.CompletedTask;

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
    public sealed class OpenRecentScriptCommandHandler : IDynamicMenuHandler<OpenRecentScriptCommandListDefinition>
    {
        private readonly IEditorRecentFilesManager recentFilesManager;
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;
        private readonly ICommandService commandService;

        [ImportingConstructor]
        public OpenRecentScriptCommandHandler(
            IEditorRecentFilesManager recentFilesManager,
            IEmbeddedRecommendedScriptService embeddedRecommendedScriptService,
            ICommandService commandService)
        {
            this.recentFilesManager = recentFilesManager;
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
            this.commandService = commandService;
        }

        public void Populate(Command command, List<DynamicMenuItem> menuItems)
        {
            var recentScripts = recentFilesManager.RecentRecordInfos
                .Where(IsScriptRecord)
                .ToArray();

            if (!recentScripts.Any())
            {
                menuItems.Add(DynamicMenuItem.FromCommand(CreateEmptyCommand(command)));
                return;
            }

            var duplicatedDisplayNames = recentScripts
                .Where(x => x.Type != RecentOpenType.OpenEmbeddedRecommendedScript)
                .GroupBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ordinaryIndex = 1;
            foreach (var info in recentScripts)
            {
                var isEmbeddedRecommended = info.Type == RecentOpenType.OpenEmbeddedRecommendedScript;
                var isValid = recentFilesManager.CheckValid(info);
                var menuText = isEmbeddedRecommended
                    ? info.DisplayName
                    : BuildRecentScriptMenuText(ordinaryIndex++, info, duplicatedDisplayNames);
                var source = isEmbeddedRecommended
                    ? CreateEmbeddedRecentSource(info)
                    : new ScriptMenuSourceInfo(
                        ScriptMenuSourceKind.FileScript,
                        info.FileName,
                        menuText,
                        Path.GetFileName(info.FileName),
                        info.FileName,
                        info);

                var scriptCommand = new Command(command.CommandDefinition)
                {
                    Text = menuText,
                    Tag = source,
                    Enabled = isValid,
                };
                menuItems.Add(isValid
                    ? DynamicMenuItem.FromCommand(scriptCommand, ScriptMenuCommandFactory.CreateActionMenuItems(commandService, source))
                    : DynamicMenuItem.FromCommand(scriptCommand));
            }
        }

        public Task Run(Command command) => Task.CompletedTask;

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

        private ScriptMenuSourceInfo CreateEmbeddedRecentSource(RecentRecordInfo info)
        {
            var embeddedScript = embeddedRecommendedScriptService.GetScript(info.FileName);
            var scriptDisplayName = embeddedScript?.FileName ?? info.DisplayName;
            return new ScriptMenuSourceInfo(
                ScriptMenuSourceKind.EmbeddedRecommendedScript,
                info.FileName,
                info.DisplayName,
                scriptDisplayName,
                info.FileName,
                info,
                embeddedScript);
        }

        private static string BuildRecentScriptMenuText(int ordinaryIndex, RecentRecordInfo info, ISet<string> duplicatedDisplayNames)
        {
            var displayName = string.IsNullOrWhiteSpace(info.DisplayName)
                ? Path.GetFileName(info.FileName)
                : info.DisplayName;

            return duplicatedDisplayNames.Contains(info.DisplayName)
                ? $"_{ordinaryIndex} {displayName} ({info.FileName})"
                : $"_{ordinaryIndex} {displayName}";
        }
    }

    [CommandHandler]
    public sealed class OpenScriptMenuActionCommandHandler : CommandHandlerBase<OpenScriptMenuActionCommandDefinition>
    {
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;

        [ImportingConstructor]
        public OpenScriptMenuActionCommandHandler(IEmbeddedRecommendedScriptService embeddedRecommendedScriptService)
        {
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
        }

        public override async Task Run(Command command)
        {
            if (command?.Tag is not ScriptMenuSourceInfo source)
                return;

            try
            {
                switch (source.Kind)
                {
                    case ScriptMenuSourceKind.EmbeddedRecommendedScript:
                        await embeddedRecommendedScriptService.OpenScriptAsync(source.SourceId);
                        break;
                    case ScriptMenuSourceKind.FileScript:
                        var isSuccess = await DocumentOpenHelper.TryOpenAsDocument(source.SourceId);
                        if (!isSuccess)
                            MessageBox.Show(Resources.ErrorEditorNotSupport, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
                        break;
                }
            }
            catch (Exception e)
            {
                var message = $"{Resources.ErrorOpenRecentFile}{e.Message}";
                Log.LogError(message);
                MessageBox.Show(message, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [CommandHandler]
    public sealed class RunScriptToCommandHandler : CommandHandlerBase<RunScriptToCommandDefinition>
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
    public sealed class RunScriptToTargetCommandHandler : IDynamicMenuHandler<RunScriptToTargetCommandListDefinition>
    {
        private readonly IEditorDocumentManager editorDocumentManager;
        private readonly IEditorScriptExecutor editorScriptExecutor;
        private readonly IEmbeddedRecommendedScriptService embeddedRecommendedScriptService;

        [ImportingConstructor]
        public RunScriptToTargetCommandHandler(
            IEditorDocumentManager editorDocumentManager,
            IEditorScriptExecutor editorScriptExecutor,
            IEmbeddedRecommendedScriptService embeddedRecommendedScriptService)
        {
            this.editorDocumentManager = editorDocumentManager;
            this.editorScriptExecutor = editorScriptExecutor;
            this.embeddedRecommendedScriptService = embeddedRecommendedScriptService;
        }

        public void Populate(Command command, List<DynamicMenuItem> menuItems)
        {
            if (command?.Tag is not ScriptMenuSourceInfo source)
                return;

            menuItems.Add(DynamicMenuItem.FromCommand(new Command(command.CommandDefinition)
            {
                Text = ScriptMenuResources.CurrentActivingEditor,
                Tag = new ScriptMenuRunTargetInfo(source, ScriptMenuRunTargetKind.CurrentActivingEditor),
                Enabled = ResolveCurrentActivingEditor() is not null,
            }));

            menuItems.Add(DynamicMenuItem.FromCommand(new Command(command.CommandDefinition)
            {
                Text = ScriptMenuResources.NoEditor,
                Tag = new ScriptMenuRunTargetInfo(source, ScriptMenuRunTargetKind.NoEditor),
            }));

            menuItems.Add(DynamicMenuItem.Separator());

            var editors = editorDocumentManager.GetCurrentEditors().ToArray();
            if (!editors.Any())
            {
                menuItems.Add(DynamicMenuItem.FromCommand(new Command(command.CommandDefinition)
                {
                    Text = ScriptMenuResources.NoOpenedEditors,
                    Enabled = false,
                }));
                return;
            }

            foreach (var editor in editors)
            {
                menuItems.Add(DynamicMenuItem.FromCommand(new Command(command.CommandDefinition)
                {
                    Text = GetEditorDisplayName(editor),
                    Tag = new ScriptMenuRunTargetInfo(source, ScriptMenuRunTargetKind.Editor, editor),
                }));
            }
        }

        public async Task Run(Command command)
        {
            if (command?.Tag is not ScriptMenuRunTargetInfo runTarget)
                return;

            var targetEditor = ResolveTargetEditor(runTarget);
            var targetDisplayName = GetTargetDisplayName(runTarget, targetEditor);
            if (runTarget.Kind != ScriptMenuRunTargetKind.NoEditor && targetEditor is null)
            {
                ShowTargetClosed(runTarget.Script, targetDisplayName);
                return;
            }

            var confirmMessage = string.Format(ScriptMenuResources.ConfirmRunScriptFormat, runTarget.Script.ScriptDisplayName, targetDisplayName);
            if (MessageBox.Show(confirmMessage, ScriptMenuResources.Scripts, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            if (targetEditor is not null && !editorDocumentManager.GetCurrentEditors().Contains(targetEditor))
            {
                ShowTargetClosed(runTarget.Script, targetDisplayName);
                return;
            }

            await RunScript(runTarget.Script, targetEditor, targetDisplayName);
        }

        private async Task RunScript(ScriptMenuSourceInfo source, FumenVisualEditorViewModel targetEditor, string targetDisplayName)
        {
            try
            {
                Log.LogInfo($"Script menu run begin. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}");

                var script = await ReadScriptSource(source);
                using var documentContext = await editorScriptExecutor.InitDocumentContext();
                var buildParam = documentContext.CreateBuildParam();
                buildParam.Script = script;
                buildParam.DisplayFileName = source.ScriptDisplayName;

                Log.LogInfo($"Script menu compile begin. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}");
                var buildResult = await editorScriptExecutor.Build(buildParam);
                if (!buildResult.IsSuccess)
                {
                    var errorMsg = string.Join(Environment.NewLine, buildResult.Errors);
                    Log.LogError($"Script menu compile failed. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}{Environment.NewLine}{errorMsg}");
                    MessageBox.Show(
                        string.Format(ScriptMenuResources.ScriptCompileFailedFormat, source.ScriptDisplayName, Environment.NewLine, errorMsg),
                        ScriptMenuResources.Scripts,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                Log.LogInfo($"Script menu compile success. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}");
                Log.LogInfo($"Script menu execute begin. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}");
                var executeResult = await editorScriptExecutor.Execute(buildResult, targetEditor);
                var resultText = executeResult.Success ? Resources.Success : $"{Resources.FailedAndReason}{executeResult.ErrorMessage}";

                if (executeResult.Success)
                    Log.LogInfo($"Script menu execute success. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}");
                else
                    Log.LogError($"Script menu execute failed. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}, reason={executeResult.ErrorMessage}");

                MessageBox.Show(
                    string.Format(ScriptMenuResources.ScriptExecutionResultFormat, source.ScriptDisplayName, Environment.NewLine, targetDisplayName, resultText),
                    ScriptMenuResources.Scripts,
                    MessageBoxButton.OK,
                    executeResult.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception e)
            {
                var message = $"{ScriptMenuResources.ScriptExecutionSourceReadFailed}{Environment.NewLine}{e.Message}";
                Log.LogError($"Script menu run failed. script={source.ScriptDisplayName}, source={source.Kind}:{source.LogSourceId}, target={targetDisplayName}{Environment.NewLine}{e}");
                MessageBox.Show(message, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> ReadScriptSource(ScriptMenuSourceInfo source)
        {
            return source.Kind switch
            {
                ScriptMenuSourceKind.EmbeddedRecommendedScript => await embeddedRecommendedScriptService.ReadScriptAsync(source.SourceId),
                ScriptMenuSourceKind.FileScript => await File.ReadAllTextAsync(source.SourceId, Encoding.UTF8),
                _ => string.Empty,
            };
        }

        private FumenVisualEditorViewModel ResolveTargetEditor(ScriptMenuRunTargetInfo runTarget)
        {
            return runTarget.Kind switch
            {
                ScriptMenuRunTargetKind.CurrentActivingEditor => ResolveCurrentActivingEditor(),
                ScriptMenuRunTargetKind.Editor => runTarget.Editor,
                _ => null,
            };
        }

        private FumenVisualEditorViewModel ResolveCurrentActivingEditor()
        {
            return editorDocumentManager.GetCurrentEditors().FirstOrDefault(x => x.IsActive) ?? editorDocumentManager.CurrentActivatedEditor;
        }

        private static string GetTargetDisplayName(ScriptMenuRunTargetInfo runTarget, FumenVisualEditorViewModel targetEditor)
        {
            return runTarget.Kind == ScriptMenuRunTargetKind.NoEditor
                ? ScriptMenuResources.NoEditor
                : GetEditorDisplayName(targetEditor);
        }

        private static string GetEditorDisplayName(FumenVisualEditorViewModel editor)
        {
            return string.IsNullOrWhiteSpace(editor?.DisplayName)
                ? ScriptMenuResources.UnnamedEditor
                : editor.DisplayName;
        }

        private static void ShowTargetClosed(ScriptMenuSourceInfo source, string targetDisplayName)
        {
            var message = $"{ScriptMenuResources.ScriptExecutionTargetClosed}{Environment.NewLine}{targetDisplayName}";
            Log.LogError($"Script menu run canceled because target editor is closed. script={source.ScriptDisplayName}, target={targetDisplayName}");
            MessageBox.Show(message, ScriptMenuResources.Scripts, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    internal static class ScriptMenuCommandFactory
    {
        public static IReadOnlyList<DynamicMenuItem> CreateActionMenuItems(ICommandService commandService, ScriptMenuSourceInfo source)
        {
            var openCommand = new Command(commandService.GetCommandDefinition(typeof(OpenScriptMenuActionCommandDefinition)))
            {
                Text = ScriptMenuResources.OpenScript,
                Tag = source,
            };
            var targetListCommand = new Command(commandService.GetCommandDefinition(typeof(RunScriptToTargetCommandListDefinition)))
            {
                Tag = source,
            };
            var runToCommand = new Command(commandService.GetCommandDefinition(typeof(RunScriptToCommandDefinition)))
            {
                Text = ScriptMenuResources.RunTo,
                Tag = source,
            };

            return new DynamicMenuItem[]
            {
                DynamicMenuItem.FromCommand(openCommand),
                DynamicMenuItem.FromCommand(runToCommand, new[]
                {
                    DynamicMenuItem.FromCommand(targetListCommand),
                }),
            };
        }
    }
}
