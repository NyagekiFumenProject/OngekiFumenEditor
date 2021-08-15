using Caliburn.Micro;
using Gemini.Framework.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    }
}
