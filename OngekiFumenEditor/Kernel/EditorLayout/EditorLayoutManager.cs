using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Modules.Shell.Services;
using Gemini.Modules.Shell.ViewModels;
using Gemini.Modules.Shell.Views;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.EditorLayout
{
    [Export(typeof(IEditorLayoutManager))]
    public class EditorLayoutManager : IEditorLayoutManager
    {
        [Import]
        private ILayoutItemStatePersister layoutItemStatePersister;

        public bool TryGetDependices(out IShell shell, out IShellView shellView)
        {
            shell = IoC.Get<IShell>();
            shellView = default;

            if (shell is ShellViewModel)
            {
                if (shell.GetType().GetField("_shellView", BindingFlags.NonPublic | BindingFlags.Instance)
                    is FieldInfo fieldInfo)
                    shellView = fieldInfo.GetValue(shell) as IShellView;
                else
                    Log.LogError($"shell object is ShellViewModel but can not locate IShellView object");
            }

            return shell != null && shellView != null;
        }

        public async Task<bool> LoadLayout(Stream intputLayoutDataStream)
        {
            var tempFilePath = TempFileHelper.GetTempFilePath("layout", "layout");
            {
                using var fs = File.OpenWrite(tempFilePath);
                await intputLayoutDataStream.CopyToAsync(fs);
            }

            try
            {
                if (!TryGetDependices(out var shell, out var shellView))
                    return false;

                var r = layoutItemStatePersister.LoadState(shell, shellView, tempFilePath);
                if (!r)

                    return false;

                if (shell.Documents.FirstOrDefault() is IDocument document)
                    await shell.OpenDocumentAsync(document);

                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"Can't load and apply program layout:{e.Message}", e);
                return false;
            }
        }

        public async Task<bool> SaveLayout(Stream outputLayoutDataStream)
        {
            var tempFilePath = TempFileHelper.GetTempFilePath("layout", "layout");

            try
            {
                if (!TryGetDependices(out var shell, out var shellView))
                    return false;

                var r = layoutItemStatePersister.SaveState(shell, shellView, tempFilePath);
                if (!r)
                    return false;
                using var fs = File.OpenRead(tempFilePath);
                await fs.CopyToAsync(outputLayoutDataStream);
                return true;
            }
            catch (Exception e)
            {
                Log.LogError($"Can't save current program layout:{e.Message}", e);
                return false;
            }
        }

        public Task<bool> ApplyDefaultSuggestEditorLayout()
        {
            var stream = ResourceUtils.OpenReadFromLocalAssemblyResourcesFolder("suggestLayout.bin");
            return LoadLayout(stream);
        }
    }
}
