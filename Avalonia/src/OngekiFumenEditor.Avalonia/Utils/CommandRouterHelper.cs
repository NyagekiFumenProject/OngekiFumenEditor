using Gekimini.Avalonia.Framework.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace OngekiFumenEditor.Avalonia.Utils;

public static class CommandRouterHelper
{
    public static Task ExecuteCommand(Command command) =>
        ExecuteCommand(IoC.Get<IServiceProvider>(), command);

    public static async Task ExecuteCommand(IServiceProvider serviceProvider, Command command)
    {
        var commandRouter = serviceProvider.GetService<ICommandRouter>();
        var handler = commandRouter?.GetCommandHandler(command.CommandDefinition);
        if (handler is null)
            return;

        await handler.Update(command);
        if (command.Enabled)
            await handler.Run(command);
    }
}
