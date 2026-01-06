using Gekimini.Avalonia.Attributes;
using Gekimini.Avalonia.Utils;
using Gekimini.Avalonia.Views;

namespace OngekiFumenEditor.Avalonia;

[CollectTypeForActivator(typeof(IView))]
public partial class ViewTypeCollectedActivator : ITypeCollectedActivator<IView>
{
}