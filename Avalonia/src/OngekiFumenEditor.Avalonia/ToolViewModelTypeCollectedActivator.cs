using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Utils;

namespace OngekiFumenEditor.Avalonia.Avalonia;

[CollectTypeForActivator(typeof(IToolViewModel))]
public partial class ToolViewModelTypeCollectedActivator : ITypeCollectedActivator<IToolViewModel>
{
}

