using System;
using Avalonia.Controls.Primitives;

namespace OngekiFumenEditor.Avalonia.UI.Controls;

/// <summary>
/// WPF GroupBox 的轻量替代：带 Header 的内容容器，样式见 UI/Themes/GroupBox.axaml。
/// </summary>
public class GroupBox : HeaderedContentControl
{
    protected override Type StyleKeyOverride => typeof(GroupBox);
}
