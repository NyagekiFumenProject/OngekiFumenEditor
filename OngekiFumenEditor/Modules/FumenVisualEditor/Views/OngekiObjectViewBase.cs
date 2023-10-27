using OngekiFumenEditor.UI.ValueConverters;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Views
{
	public class OngekiObjectViewBase : UserControl
	{
		private static DropShadowEffect SelectEffect = new DropShadowEffect() { ShadowDepth = 0, Color = Colors.Yellow, BlurRadius = 25 };
		private readonly static LambdaConverter<bool, Effect> isSelectConverter = new(o => o ? SelectEffect : default);

		public OngekiObjectViewBase()
		{
			SetBinding(EffectProperty, new Binding("ReferenceOngekiObject.IsSelected")
			{
				Converter = isSelectConverter
			});
		}
	}
}
