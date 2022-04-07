using OngekiFumenEditor.Base;
using OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.UIGenerator;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenObjectPropertyBrowser.ViewModels
{
    public class FileInfoTypeUIViewModel : CommonUIViewModelBase<FileInfo>
    {
        public FileInfo File
        {
            get => TypedProxyValue;
            set
            {
                if (PropertyInfo is UndoablePropertyInfoWrapper undoable)
                    undoable.ExecuteSubPropertySetAction(nameof(RangeValue.CurrentValue), (val) => TypedProxyValue = val, TypedProxyValue, value);
                else
                    TypedProxyValue = value;

                NotifyOfPropertyChange(() => File);
            }
        }

        public FileInfoTypeUIViewModel(PropertyInfoWrapper wrapper) : base(wrapper)
        {

        }

        public void OnSelectDialogOpen()
        {
            var filePath = FileDialogHelper.OpenFile("选择svg文件", new[] { (".svg", "Svg文件") });
            File = string.IsNullOrWhiteSpace(filePath) ? null : new FileInfo(filePath);
        }
    }
}
