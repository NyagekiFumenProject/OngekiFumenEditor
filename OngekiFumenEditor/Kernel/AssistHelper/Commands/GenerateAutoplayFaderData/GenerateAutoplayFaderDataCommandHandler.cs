using Gemini.Framework.Commands;
using Gemini.Framework.Threading;
using OngekiFumenEditor.Base.EditorObjects;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using OngekiFumenEditor.Utils;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace OngekiFumenEditor.Kernel.AssistHelper.Commands.GenerateAutoplayFaderData
{
	[CommandHandler]
	public class GenerateAutoplayFaderDataCommandHandler : CommandHandlerBase<GenerateAutoplayFaderDataCommandDefinition>
	{
		[Import(typeof(IEditorDocumentManager))]
		public IEditorDocumentManager EditorDocumentManager { get; set; }

		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = EditorDocumentManager.CurrentActivatedEditor is not null;
		}

		public override async Task Run(Command command)
		{
			try
			{
				var fileSavePath = FileDialogHelper.SaveFile("AutoplayFader文件保存路径", new[] { ("AutoplayFader文件", ".apf") });
				if (fileSavePath == null)
					return;
				var editor = EditorDocumentManager.CurrentActivatedEditor;
				var lanes = editor.Fumen.Lanes.OfType<AutoplayFaderLaneStart>().SelectMany(lane => lane.InterpolateCurve()).ToList();

				var str = JsonSerializer.Serialize(lanes
					.Select(x => x.Children.AsEnumerable<ConnectableObjectBase>().Prepend(x)
					.Select(x => new
					{
						Time = TGridCalculator.ConvertTGridToAudioTime(x.TGrid, editor).TotalMilliseconds,
						XUnit = x.XGrid.TotalUnit
					}).ToArray())
					.ToArray());

				await File.WriteAllTextAsync(fileSavePath, str);
				if (MessageBox.Show("AutoplayFader文件生成成功,是否打开输出文件夹?", "AutoplayFader文件保存", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					ProcessUtils.OpenPath(Path.GetDirectoryName(fileSavePath));
			}
			catch (Exception e)
			{
				MessageBox.Show($"AutoplayFader文件生成失败:{e.Message}");
				throw;
			}
		}
	}
}