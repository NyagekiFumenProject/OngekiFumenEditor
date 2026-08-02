using Microsoft.Extensions.DependencyInjection;
using OngekiFumenEditor.Avalonia.CommandLine;

var services = new ServiceCollection();
services.AddOngekiFumenEditorCommandLine();

using var serviceProvider = services.BuildServiceProvider();
var executor = serviceProvider.GetRequiredService<ICommandExecutor>();
return await executor.ExecuteAsync(args);
