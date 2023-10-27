using Caliburn.Micro;
using Gemini.Framework.Commands;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using OngekiFumenEditor.Modules.FumenVisualEditor.Kernel;
using System.Linq;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Commands.OgkrImpl.AppendEndLaneObject
{
	[CommandHandler]
	public class AppendEndLaneObjectCommandHandler : CommandHandlerBase<AppendEndLaneObjectCommandDefinition>
	{
		public override void Update(Command command)
		{
			base.Update(command);
			command.Enabled = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor is not null;
		}

		public override Task Run(Command command)
		{
			var editor = IoC.Get<IEditorDocumentManager>().CurrentActivatedEditor;
			var fumen = editor.Fumen;

			var targetStartObjs = fumen.Lanes.Where(x => x.Children.LastOrDefault() is not ConnectableEndObject).ToArray();

			foreach (var startObj in targetStartObjs)
			{
				var lastObj = startObj.Children.LastOrDefault() ?? startObj as ConnectableObjectBase;
				var endObj = startObj.CreateEndObject();

				endObj.TGrid = lastObj.TGrid;
				endObj.XGrid = lastObj.XGrid;

				startObj.AddChildObject(endObj);
			}

			editor.Toast.ShowMessage($"已补上 {targetStartObjs.Length} 个轨道物件");

			return Task.CompletedTask;
		}
	}
}