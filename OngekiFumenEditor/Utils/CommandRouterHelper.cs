using Caliburn.Micro;
using Gemini.Framework.Commands;
using Gemini.Framework.Services;
using Gemini.Modules.Shell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Gemini.Modules.Shell.Commands.NewFileCommandHandler;

namespace OngekiFumenEditor.Utils
{
	public static class CommandRouterHelper
	{
		public static async Task ExecuteCommand(Command command)
		{
			var commandRouter = IoC.Get<ICommandRouter>();
			var handler = commandRouter.GetCommandHandler(command.CommandDefinition);
			handler.Update(command);
			if (command.Enabled)
				await handler.Run(command);
		}
	}
}
