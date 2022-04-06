using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Properties;

namespace Gemini.Modules.MainWindow.ViewModels
{
    [Export(typeof(IMainWindow))]
    public class MainWindowViewModel : Conductor<IShell>, IMainWindow, IPartImportsSatisfiedNotification
    {
#pragma warning disable 649
        [Import]
        private IShell _shell;

        [Import]
        private IResourceManager _resourceManager;

        [Import]
        private ICommandKeyGestureService _commandKeyGestureService;
#pragma warning restore 649

        private WindowState _windowState = WindowState.Normal;
        public WindowState WindowState
        {
            get { return _windowState; }
            set
            {
                _windowState = value;
                NotifyOfPropertyChange(() => WindowState);
            }
        }

        private Rect windowRect = new Rect(new Size(1000, 600));

        public Rect WindowRect => windowRect;

        public double Top
        {
            get { return windowRect.Y; }
            set
            {
                windowRect.Y = value;
                NotifyOfPropertyChange(() => Top);
            }
        }

        public double Left
        {
            get { return windowRect.X; }
            set
            {
                windowRect.X = value;
                NotifyOfPropertyChange(() => Left);
            }
        }

        public double Height
        {
            get { return windowRect.Height; }
            set
            {
                windowRect.Height = value;
                NotifyOfPropertyChange(() => Height);
            }
        }

        public double Width
        {
            get { return windowRect.Width; }
            set
            {
                windowRect.Width = value;
                NotifyOfPropertyChange(() => Width);
            }
        }

        private string _title = Resources.MainWindowDefaultTitle;
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value;
                NotifyOfPropertyChange(() => Title);
            }
        }

        private ImageSource _icon;
        public ImageSource Icon
        {
            get { return _icon; }
            set
            {
                _icon = value;
                NotifyOfPropertyChange(() => Icon);
            }
        }

        public IShell Shell
        {
            get { return _shell; }
        }

        void IPartImportsSatisfiedNotification.OnImportsSatisfied()
        {
            if (_icon == null)
                _icon = _resourceManager.GetBitmap("Resources/Icons/Gemini-32.png");
            Execute.OnUIThreadAsync(() => ActivateItemAsync(_shell, CancellationToken.None));
        }

        protected override void OnViewLoaded(object view)
        {
            Left = Properties.Settings.Default.MainWindowRectLeft;
            Top = Properties.Settings.Default.MainWindowRectTop;
            Width = Properties.Settings.Default.MainWindowRectWidth;
            Height = Properties.Settings.Default.MainWindowRectHeight;

            _commandKeyGestureService.BindKeyGestures((UIElement)view);
            base.OnViewLoaded(view);
        }
    }
}
