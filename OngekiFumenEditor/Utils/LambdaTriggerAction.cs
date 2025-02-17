using System;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Xaml.Behaviors;

namespace OngekiFumenEditor.Utils;

public class LambdaTriggerAction : TriggerAction<UIElement>
{
    private readonly Action<object> Action;

    public LambdaTriggerAction(System.Action<object> action)
    {
        Action = action;
    }
    
    protected override void Invoke(object parameter)
    {
        Action.Invoke(parameter);
    }
}