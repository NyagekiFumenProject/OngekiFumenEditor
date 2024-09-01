using System.Collections.Generic;
using System.Threading.Tasks;
using Gemini.Framework;
using OngekiFumenEditor.Modules.OgkiFumenListBrowser.Models;

namespace OngekiFumenEditor.Modules.OgkiFumenListBrowser
{
	public interface IOgkiFumenListBrowser : IWindow
	{
		Task<IEnumerable<OngekiFumenSet>> SearchFumenSet(string searchFolder);
	}
}
