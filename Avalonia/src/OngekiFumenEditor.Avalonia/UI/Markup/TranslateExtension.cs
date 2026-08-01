using System;
using System.Globalization;
using OngekiFumenEditor.Avalonia.Assets.Languages;

namespace OngekiFumenEditor.Avalonia.UI.Markup;

/// <summary>
/// 本地化标记扩展，替代 WPF 版 OngekiFumenEditor.UI.Markup.TranslateExtension。
/// 沿用旧写法 `{markup:Translate [ResourceKey]}`（构造参数为资源名，可带方括号），
/// 在 XAML 构造时返回当前语言文本。应用的语言切换流程要求重启，因此不需要反射式运行时绑定。
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

        var text = Lang.LocalizerManager.GetLocalizedText(resourceName) ?? $"[{resourceName}]";
        if (string.IsNullOrEmpty(StringFormat))
            return text;

        var format = StringFormat.StartsWith("{}", StringComparison.Ordinal)
            ? StringFormat[2..]
            : StringFormat;

        return format.Contains("{0", StringComparison.Ordinal)
            ? string.Format(CultureInfo.CurrentCulture, format, text)
            : text + format;
    }
}
