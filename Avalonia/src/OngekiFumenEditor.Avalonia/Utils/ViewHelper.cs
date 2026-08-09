using Avalonia.Controls;
using Gekimini.Avalonia.Views;
using OngekiFumenEditor.Avalonia.Utils.Attributes;
using System.Reflection;

namespace OngekiFumenEditor.Avalonia.Utils;

public static class ViewHelper
{
    public static Control CreateViewByViewModelType<T>() where T : new() => CreateViewByViewModelType(() => new T());

    public static Control CreateViewByViewModelType<T>(Func<T> modelGenerator = null)
    {
        var viewModel = modelGenerator is null ? IoC.Get<T>() : modelGenerator();
        return CreateView(viewModel);
    }

    public static Control CreateView(object viewModel)
    {
        var mapToAttr = viewModel.GetType().GetCustomAttribute<MapToViewAttribute>();
        if (mapToAttr?.ViewType is not null && CacheLambdaActivator.CreateInstance(mapToAttr.ViewType) is Control ctrl)
        {
            ctrl.DataContext = viewModel;
            return ctrl;
        }

        return IoC.Get<ViewLocator>().Build(viewModel);
    }
}
