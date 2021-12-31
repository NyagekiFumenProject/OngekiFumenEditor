using Caliburn.Micro;
using Gemini.Framework.Services;
using OngekiFumenEditor.Kernel.Scheduler;
using OngekiFumenEditor.UI.KeyBinding.Input;
using OngekiFumenEditor.Utils;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace OngekiFumenEditor
{
    public class AppBootstrapper : Gemini.AppBootstrapper
    {
        protected async Task InitKernels()
        {
            await IoC.Get<ISchedulerManager>().Init();
        }

        protected override void Configure()
        {
            base.Configure();
            var defaultCreateTrigger = Caliburn.Micro.Parser.CreateTrigger;

            Caliburn.Micro.Parser.CreateTrigger = (target, triggerText) =>
            {
                if (triggerText == null)
                {
                    return defaultCreateTrigger(target, null);
                }

                var triggerDetail = triggerText
                    .Replace("[", string.Empty)
                    .Replace("]", string.Empty);

                var splits = triggerDetail.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

                switch (splits[0])
                {
                    case "Key":
                        var key = (Key)Enum.Parse(typeof(Key), splits[1], true);
                        return new KeyTrigger { Key = key };

                    case "Gesture":
                        var mkg = (MultiKeyGesture)(new MultiKeyGestureConverter()).ConvertFrom(splits[1]);
                        return new KeyTrigger { Modifiers = mkg.KeySequences[0].Modifiers, Key = mkg.KeySequences[0].Keys[0] };
                }

                return defaultCreateTrigger(target, triggerText);
            };
        }

        protected async override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);

            IoC.Get<IShell>().ToolBars.Visible = true;
            IoC.Get<WindowTitleHelper>().TitleContent = "";

            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri("pack://application:,,,/OngekiFumenEditor;component/Resources/logo.png");
            logo.EndInit();
            IoC.Get<WindowTitleHelper>().Icon = logo;

            await InitKernels();

            Log.LogInfo(IoC.Get<CommonStatusBar>().MainContentViewModel.Message = "Application is Ready.");
        }
    }
}
