using Caliburn.Micro;
using Gemini.Framework.Services;
using Gemini.Modules.Output;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor
{
    public class AppBootstrapper : Gemini.AppBootstrapper
    {
        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);

            IoC.Get<IShell>().ToolBars.Visible = true;
            IoC.Get<WindowTitleHelper>().TitleContent = "";

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/logo.png");
            logo.EndInit();
            IoC.Get<WindowTitleHelper>().Icon = logo;

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");
        }
    }
}
