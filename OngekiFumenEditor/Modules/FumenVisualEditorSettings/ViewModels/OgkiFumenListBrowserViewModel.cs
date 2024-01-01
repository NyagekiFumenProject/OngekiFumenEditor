using Caliburn.Micro;
using Gemini.Framework;
using Gemini.Framework.Services;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor.Models;
using OngekiFumenEditor.Modules.FumenVisualEditor.ViewModels;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using System.ComponentModel;
using System.ComponentModel.Composition;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Models.EditorSetting;

namespace OngekiFumenEditor.Modules.FumenVisualEditorSettings.ViewModels
{
	[Export(typeof(IFumenVisualEditorSettings))]
	public class FumenVisualEditorSettingsViewModel : Tool, IFumenVisualEditorSettings
	{
		public double[] UnitCloseSizeValues { get; } = new[]
		{
			1d,
			2,
			3,
			4,
			5,
			6,
			7,
			8,
			9,
			10,
			11,
			12,
		};

		public string[] SupportTimeFormats { get; } = new[]
		{
			nameof(TimeFormat.TGrid),
			nameof(TimeFormat.AudioTime)
		};

		public override PaneLocation PreferredLocation => PaneLocation.Right;

		private FumenVisualEditorViewModel editor = default;
		public FumenVisualEditorViewModel Editor
		{
			get => editor;
			set
			{
				Set(ref editor, value);
				Setting = Editor?.Setting;


				if (Editor is null)
					DisplayName = Resources.FumenVisualEditorSettings;
				else
					DisplayName = $"{Resources.FumenVisualEditorSettings} - " + Editor.FileName;
			}
		}

		private EditorSetting setting;
		public EditorSetting Setting
		{
			get => setting;
			set => Set(ref setting, value);
		}

		public FumenVisualEditorSettingsViewModel()
		{
			DisplayName = Resources.FumenVisualEditorSettings;
			IoC.Get<IEditorDocumentManager>().OnActivateEditorChanged += OnActivateEditorChanged;
			Editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
		}

		private void OnActivateEditorChanged(FumenVisualEditorViewModel @new, FumenVisualEditorViewModel old)
		{
			Editor = @new;
			this.RegisterOrUnregisterPropertyChangeEvent(old, @new, OnEditorPropertyChanged);
		}

		private void OnEditorPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(FumenVisualEditorViewModel.Setting))
				Setting = Editor?.Setting;
		}
	}
}
