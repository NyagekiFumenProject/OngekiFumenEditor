using System;
using System.Windows;
using System.Windows.Input;
using Caliburn.Micro;
using Gemini.Framework.Commands;
using Microsoft.Xaml.Behaviors;

namespace OngekiFumenEditor.Modules.FumenVisualEditor.Base;

public class CommandActionMessage : TriggerAction<FrameworkElement>
{
    public Type CommandDefinition
    {
        get => (Type)GetValue(CommandDefinitionProperty);
        set => SetValue(CommandDefinitionProperty, value);
    }
    
    protected override void Invoke(object parameter)
    {
        var commandService = IoC.Get<ICommandService>();
        var definition = commandService.GetCommandDefinition(CommandDefinition);
        var command = commandService.GetTargetableCommand(commandService.GetCommand(definition));
        if (command.CanExecute(parameter))
            command.Execute(parameter);
    }
    
    public static readonly DependencyProperty CommandDefinitionProperty = DependencyProperty.Register("CommandDefinition", typeof(Type), typeof(CommandActionMessage), null);
}