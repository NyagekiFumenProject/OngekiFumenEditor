using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using Gemini.Framework.Threading;
using Gemini.Properties;
using OngekiFumenEditor.Modules.TextEditor.Views;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.TextEditor.ViewModels
{
    [Export(typeof(TextEditorViewModel))]
    class TextEditorViewModel : PersistedDocument
    {
        private TextEditorView view;
        private string content;

        public const string DEFAULT_TEMPLATE = "empty_ogkr_template.ogkr";

        private void ApplyTitle()
        {
            if (IoC.Get<IMainWindow>() is IMainWindow window)
            {
                window.Title = DisplayName;
            }
        }

        private void ApplyTextAndUpdate()
        {
            view.textBox.Text = content;
            IsDirty = string.Compare(content, view.textBox.Text) != 0;
            ApplyTitle();
            view.textBox.TextChanged += (_, _) =>
            {
                IsDirty = string.Compare(content, view.textBox.Text) != 0;
                ApplyTitle();
            };
        }

        protected override void OnViewLoaded(object view) => this.view = (TextEditorView)view;

        protected override async Task DoNew()
        {
            Log.LogInfo($"TextEditorViewModel DoNew()");
            using var reader = new StreamReader(ResourceUtils.OpenReadFromLocalAssemblyResource(DEFAULT_TEMPLATE));
            content = await reader.ReadToEndAsync();
            ApplyTextAndUpdate();
        }

        protected override async Task DoLoad(string filePath)
        {
            Log.LogInfo($"TextEditorViewModel DoLoad() filePath : {filePath}");
            content = await File.ReadAllTextAsync(filePath);
            var fumen = await IoC.Get<IOngekiFumenParser>().ParseAsync(File.OpenRead(filePath));
            ApplyTextAndUpdate();
        }

        protected override async Task DoSave(string filePath)
        {
            Log.LogInfo($"TextEditorViewModel DoSave() filePath : {filePath}");
            var newText = view.textBox.Text;
            await File.WriteAllTextAsync(filePath, newText);
            content = newText;
            ApplyTextAndUpdate();
        }
    }
}
