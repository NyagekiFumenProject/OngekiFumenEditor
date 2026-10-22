using Caliburn.Micro;
using OngekiFumenEditor.Properties;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.ViewModels
{
    public sealed class SoflanGroupNameDialogViewModel : Screen
    {
        private readonly bool allowEmpty;
        private string name;
        private string message;

        public SoflanGroupNameDialogViewModel(string title, string name, string message = null, bool allowEmpty = false)
        {
            DisplayName = title;
            this.name = name ?? string.Empty;
            this.message = message ?? string.Empty;
            this.allowEmpty = allowEmpty;
        }

        public string Name
        {
            get => name;
            set => Set(ref name, value);
        }

        public string Message
        {
            get => message;
            private set
            {
                if (Set(ref message, value))
                    NotifyOfPropertyChange(() => HasMessage);
            }
        }

        public bool HasMessage => !string.IsNullOrEmpty(Message);

        public Task Confirm()
        {
            var value = Name?.Trim() ?? string.Empty;
            if (!allowEmpty && value.Length == 0)
            {
                Message = Resources.SoflanGroupNameRequired;
                return Task.CompletedTask;
            }

            Name = value;
            return TryCloseAsync(true);
        }

        public Task Cancel() => TryCloseAsync(false);
    }
}
