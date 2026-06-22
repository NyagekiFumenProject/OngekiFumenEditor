using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.RecentFiles;
using OngekiFumenEditor.Modules.EditorScriptExecutor;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents;
using OngekiFumenEditor.Modules.EditorScriptExecutor.Documents.ViewModels;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Modules.EditorScriptExecutor.Services
{
    [Export(typeof(IEmbeddedRecommendedScriptService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DefaultEmbeddedRecommendedScriptService : IEmbeddedRecommendedScriptService
    {
        private const string ResourcePrefix = "OngekiFumenEditor.Resources.Scripts.EmbbedRecommended.";
        private const string ScriptExtension = EditorScriptDocumentProvider.FILE_EXTENSION_NAME;

        private readonly IShell shell;
        private readonly IEditorScriptDocumentProvider scriptDocumentProvider;
        private readonly IEditorRecentFilesManager recentFilesManager;
        private readonly Assembly assembly = typeof(DefaultEmbeddedRecommendedScriptService).Assembly;

        [ImportingConstructor]
        public DefaultEmbeddedRecommendedScriptService(
            IShell shell,
            IEditorScriptDocumentProvider scriptDocumentProvider,
            IEditorRecentFilesManager recentFilesManager)
        {
            this.shell = shell;
            this.scriptDocumentProvider = scriptDocumentProvider;
            this.recentFilesManager = recentFilesManager;
        }

        public IEnumerable<EmbeddedRecommendedScriptInfo> GetScripts()
        {
            return assembly
                .GetManifestResourceNames()
                .Where(IsRecommendedScriptResourceName)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(CreateInfo);
        }

        public EmbeddedRecommendedScriptInfo GetScript(string resourceName)
        {
            return GetScripts().FirstOrDefault(x => string.Equals(x.ResourceName, resourceName, StringComparison.Ordinal));
        }

        public bool Contains(string resourceName)
        {
            return !string.IsNullOrWhiteSpace(resourceName) && assembly.GetManifestResourceNames().Any(x => string.Equals(x, resourceName, StringComparison.Ordinal));
        }

        public async Task<string> ReadScriptAsync(string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                throw new FileNotFoundException(ScriptMenuResources.EmbeddedRecommendedScriptNotFound, resourceName);

            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            return await reader.ReadToEndAsync();
        }

        public async Task OpenScriptAsync(string resourceName)
        {
            var info = GetScript(resourceName);
            if (info is null)
                throw new FileNotFoundException(ScriptMenuResources.EmbeddedRecommendedScriptNotFound, resourceName);

            var content = await ReadScriptAsync(resourceName);
            var document = scriptDocumentProvider.Create() as EditorScriptDocumentViewModel;
            if (document is null)
                throw new InvalidOperationException(Resources.ErrorEditorNotSupport);

            var viewAware = (IViewAware)document;
            viewAware.ViewAttached += (sender, e) =>
            {
                var frameworkElement = (FrameworkElement)e.View;

                RoutedEventHandler loadedHandler = null;
                loadedHandler = async (sender2, e2) =>
                {
                    frameworkElement.Loaded -= loadedHandler;
                    await document.LoadEmbeddedRecommendedScript(resourceName, info.FileName, content);

                    recentFilesManager.PostRecord(new(
                        resourceName,
                        string.Format(ScriptMenuResources.EmbeddedRecommendedScriptRecentDisplayNameFormat, info.FileName),
                        RecentOpenType.OpenEmbeddedRecommendedScript));
                };
                frameworkElement.Loaded += loadedHandler;
            };

            await shell.OpenDocumentAsync(document);
        }

        private static bool IsRecommendedScriptResourceName(string resourceName)
        {
            return resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                   && resourceName.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static EmbeddedRecommendedScriptInfo CreateInfo(string resourceName)
        {
            var fileName = resourceName.Substring(ResourcePrefix.Length);
            var displayName = fileName.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - ScriptExtension.Length)
                : fileName;

            return new(resourceName, fileName, displayName);
        }
    }
}
