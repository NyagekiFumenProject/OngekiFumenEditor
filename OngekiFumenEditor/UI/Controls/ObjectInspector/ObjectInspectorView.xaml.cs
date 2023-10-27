using OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector
{
	/// <summary>
	/// FumenMetaInfoBrowserView.xaml 的交互逻辑
	/// </summary>
	public partial class ObjectInspectorView : UserControl
	{
		public object InspectObject
		{
			get { return GetValue(InspectObjectProperty); }
			set { SetValue(InspectObjectProperty, value); }
		}

		// Using a DependencyProperty as the backing store for InspectObject.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty InspectObjectProperty =
			DependencyProperty.Register("InspectObject", typeof(object), typeof(ObjectInspectorView), new PropertyMetadata(null, (s, e) =>
			{
				if (s is ObjectInspectorView ow && ow.HostContent.DataContext is ObjectInspectorViewModel om)
				{
					om.InspectObject = e.NewValue;
				}
			}));


		public ObjectInspectorView()
		{
			InitializeComponent();

			HostContent.DataContext = new ObjectInspectorViewModel();
		}
	}
}
