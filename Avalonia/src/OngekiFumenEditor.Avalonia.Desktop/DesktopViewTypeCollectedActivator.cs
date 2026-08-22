using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Views;

namespace OngekiFumenEditor.Avalonia.Desktop;

[CollectTypeForActivator(typeof(IView))]
public partial class DesktopViewTypeCollectedActivator : ITypeCollectedActivator<IView>
{
}
