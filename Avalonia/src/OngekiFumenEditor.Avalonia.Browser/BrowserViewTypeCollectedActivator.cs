using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Views;

namespace OngekiFumenEditor.Avalonia.Browser;

[CollectTypeForActivator(typeof(IView))]
public partial class BrowserViewTypeCollectedActivator : ITypeCollectedActivator<IView>
{
}
