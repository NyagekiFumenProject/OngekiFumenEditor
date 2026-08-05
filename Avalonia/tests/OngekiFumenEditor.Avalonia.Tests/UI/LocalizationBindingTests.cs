using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using OngekiFumenEditor.Avalonia.Kernel.KeyBinding;
using OngekiFumenEditor.Avalonia.Kernel.SettingPages.KeyBinding.Dialogs;
using OngekiFumenEditor.Avalonia.UI.Dialogs.Views;
using SimpleTypedLocalizer;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.UI;

public sealed class LocalizationBindingTests
{
    [AvaloniaFact]
    public async Task TranslateExtension_LangBSource_UpdatesWhenCultureChanges()
    {
        var originalCulture = LocalizerManager.CurrentDefaultCultureInfo;

        try
        {
            LocalizerManager.CurrentDefaultCultureInfo = CultureInfo.GetCultureInfo("ja");
            var view = new AboutWindowView();
            await FlushUiThreadAsync();

            Assert.Equal("このアプリについて", view.Title);

            LocalizerManager.CurrentDefaultCultureInfo = CultureInfo.GetCultureInfo("zh-Hans");
            await FlushUiThreadAsync();

            Assert.Equal("关于", view.Title);
        }
        finally
        {
            LocalizerManager.CurrentDefaultCultureInfo = originalCulture;
            await FlushUiThreadAsync();
        }
    }

    [AvaloniaFact]
    public async Task FormattedTranslation_MultiBinding_CombinesLocalizedTemplateAndArgument()
    {
        var originalCulture = LocalizerManager.CurrentDefaultCultureInfo;

        try
        {
            LocalizerManager.CurrentDefaultCultureInfo = CultureInfo.GetCultureInfo("ja");
            var definition = new KeyBindingDefinition("CommandAbout", Key.A);
            var dialog = new ConfigKeyBindingDialog(definition);
            await FlushUiThreadAsync();

            var displayedTexts = dialog
                .GetLogicalDescendants()
                .OfType<TextBlock>()
                .Select(static textBlock => textBlock.Text)
                .ToArray();

            Assert.Contains("「このアプリケーションについて」にキーを割り当ててください", displayedTexts);
        }
        finally
        {
            LocalizerManager.CurrentDefaultCultureInfo = originalCulture;
            await FlushUiThreadAsync();
        }
    }

    private static async Task FlushUiThreadAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { });
    }
}
