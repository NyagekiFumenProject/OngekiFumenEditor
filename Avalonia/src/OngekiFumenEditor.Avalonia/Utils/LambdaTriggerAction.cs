namespace OngekiFumenEditor.Avalonia.Utils;

public class LambdaTriggerAction(Action<object> action)
{
    private readonly Action<object> action = action;

    public void Invoke(object parameter)
    {
        action.Invoke(parameter);
    }
}
