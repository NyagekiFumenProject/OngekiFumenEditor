using Caliburn.Micro;
using Gemini.Framework.Services;
using Gemini.Modules.Output;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor
{
    public class AppBootstrapper : Gemini.AppBootstrapper
    {
        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);

            IoC.Get<IShell>().ToolBars.Visible = true;
            IoC.Get<WindowTitleHelper>().TitleContent = "";

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");
        }
    }
}
