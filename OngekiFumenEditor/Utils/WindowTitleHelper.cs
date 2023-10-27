using Gemini.Framework.Services;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
	[Export(typeof(WindowTitleHelper))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	public class WindowTitleHelper
	{
		[Import(typeof(IMainWindow))]
		private IMainWindow window = default;

		public string TitlePrefix { get; set; } = "Ongeki Fumen Editor";

		public string TitleContent
		{
			get
			{
				return window.Title;
			}
			set
			{
				var title = value;
				if (!(title?.StartsWith(TitlePrefix) ?? false))
				{
					title = TitlePrefix + (string.IsNullOrWhiteSpace(title) ? string.Empty : (" - " + title));
				}
				window.Title = title;
			}
		}

		public ImageSource Icon
		{
			get
			{
				return window.Icon;
			}
			set
			{
				window.Icon = value;
			}
		}
	}
}
