using Gemini.Framework.Markup;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.UI.Markup
{
	public class TranslateExtension : TranslateExtensionBase
	{
		public TranslateExtension(string member) : base(member, Resources.ResourceManager.GetString)
		{
		}
	}
}
