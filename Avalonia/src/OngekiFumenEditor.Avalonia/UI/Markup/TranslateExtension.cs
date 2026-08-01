using System;
using Avalonia.Data;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using SimpleTypedLocalizer;

namespace OngekiFumenEditor.Avalonia.UI.Markup;

/// <summary>
/// 本地化标记扩展，替代 WPF 版 OngekiFumenEditor.UI.Markup.TranslateExtension。
/// 沿用旧写法 `{markup:Translate [ResourceKey]}`（构造参数为资源名，可带方括号），
/// 返回绑定 ILocalizedTextSource.Text 的 OneWay Binding，语言切换时自动刷新。
/// </summary>
public class TranslateExtension
{
    private string resourceName;

    public TranslateExtension() : this(default)
    {
    }

    public TranslateExtension(string resourceName)
    {
        this.resourceName = resourceName?.Trim('[', ']');
    }

    /// <summary>
    /// 资源名（可带方括号）。供元素写法 <markup:Translate Path="[ResourceKey]" /> 使用，
    /// 等价于构造参数写法 {markup:Translate [ResourceKey]}。
    /// </summary>
    public string Path
    {
        get => resourceName;
        set => resourceName = value?.Trim('[', ']');
    }

    /// <summary>
    /// 可选的复合格式字符串，对应 WPF 版继承自 Binding 的 StringFormat 属性。
    /// </summary>
    public string StringFormat { get; set; }

    public object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            return "<i18n:null-resource-name>";

        var textSource = Lang.LocalizerManager.GetLocalizedTextSource(resourceName);
        return new Binding(nameof(ILocalizedTextSource.Text))
        {
            Source = textSource,
            Mode = BindingMode.OneWay,
            StringFormat = StringFormat
        };
    }
}
