using OngekiFumenEditor.Properties;
using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using OngekiFumenEditor.Utils;
using System.IO;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
	public class FileInfoTypeUIViewModel : CommonUIViewModelBase<FileInfo>
	{
		public FileInfo File
		{
			get => TypedProxyValue;
			set
			{
				TypedProxyValue = value;
				NotifyOfPropertyChange(() => File);
			}
		}

		public FileInfoTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
		{

		}

		public void OnSelectDialogOpen()
		{
			var filePath = FileDialogHelper.OpenFile(Resources.SelectSvgFile, new[] { (".svg", "Svg文件") });
			File = string.IsNullOrWhiteSpace(filePath) ? null : new FileInfo(filePath);
		}
	}
}
