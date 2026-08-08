using Gekimini.Avalonia.Framework;

namespace OngekiFumenEditor.Avalonia.Modules.FumenMetaInfoBrowser
{
	public interface IFumenMetaInfoBrowser : IToolViewModel
    {
		OngekiFumenEditor.Avalonia.Base.OngekiFumen Fumen { get; set; }
	}
}


