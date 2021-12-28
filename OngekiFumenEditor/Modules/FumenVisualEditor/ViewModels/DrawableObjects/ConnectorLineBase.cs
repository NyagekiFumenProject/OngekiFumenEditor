using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels.DrawableObjects
{
    public abstract class ConnectorLineViewModelBase : PropertyChangedBase
    {
        private FumenVisualEditorViewModel editorViewModel;
        public FumenVisualEditorViewModel EditorViewModel
        {
            get
            {
                return editorViewModel;
            }
            set
            {
                editorViewModel = value;
                NotifyOfPropertyChange(() => EditorViewModel);
            }
        }
    }

    public abstract class ConnectorLineViewModelBase<T> : ConnectorLineViewModelBase
    {
        private T fromObject;
        public T FromObject
        {
            get
            {
                return fromObject;
            }
            set
            {
                fromObject = value;
                NotifyOfPropertyChange(() => FromObject);
            }
        }

        private T toObject;
        public T ToObject
        {
            get
            {
                return toObject;
            }
            set
            {
                toObject = value;
                NotifyOfPropertyChange(() => ToObject);
            }
        }
    }
}
