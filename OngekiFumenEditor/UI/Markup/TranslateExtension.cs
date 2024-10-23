using Caliburn.Micro;
using Gemini.Framework.Languages;
using Gemini.Framework.Markup;
using OngekiFumenEditor.Properties;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace OngekiFumenEditor.UI.Markup
{
    public class TranslateExtension : TranslateExtensionBase
    {
        public TranslateExtension() : this(default) //Pass default value at first, and then we assign actual resource name to Path
        {

        }

        public TranslateExtension(string resourceName) : base(resourceName, Resources.ResourceManager.GetString)
        {

        }
    }
}
