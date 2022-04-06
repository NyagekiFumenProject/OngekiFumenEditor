using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Gemini.Framework
{
    public abstract class PersistedDocument : Document, IPersistedDocument
    {
        private bool _isDirty;

        public bool IsNew { get; private set; }
        public string FileName { get; private set; }

        private string _filePath = null;
        public string FilePath
        {
            get { return _filePath; }
            private set
            {
                _filePath = value;
                NotifyOfPropertyChange(() => FilePath);
                UpdateToolTip();
            }
        }

        public bool IsDirty
        {
            get { return _isDirty; }
            set
            {
                if (value == _isDirty)
                    return;

                _isDirty = value;
                NotifyOfPropertyChange(() => IsDirty);
                UpdateDisplayName();
            }
        }

        public override async Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            if (IsDirty)
            {
                //问问是否保存
                var result = MessageBox.Show("文档还有没保存的变更,是否立即保存?", "提醒",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel);

                switch (result)
                {
                    case MessageBoxResult.Yes:
                        return await SaveInternal();
                    case MessageBoxResult.No:
                        return true; //close directly
                    default:
                        return false;
                }
            }

            return true;
        }

        private void UpdateDisplayName()
        {
            DisplayName = IsDirty ? FileName + "*" : FileName;
        }

        private void UpdateToolTip()
        {
            ToolTip = FilePath;
        }

        public async Task New(string fileName)
        {
            FileName = fileName;
            UpdateDisplayName();

            IsNew = true;
            IsDirty = false;

            await DoNew();
        }

        protected abstract Task DoNew();

        public async Task Load(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            UpdateDisplayName();

            IsNew = false;
            IsDirty = false;

            await DoLoad(filePath);
        }

        protected abstract Task DoLoad(string filePath);

        public async Task Save(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            UpdateDisplayName();

            await DoSave(filePath);

            IsDirty = false;
            IsNew = false;
        }

        protected abstract Task DoSave(string filePath);
    }
}
