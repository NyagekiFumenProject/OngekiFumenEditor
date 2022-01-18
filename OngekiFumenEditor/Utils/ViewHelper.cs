using Caliburn.Micro;
using Gemini.Framework;
using OngekiFumenEditor.Utils.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OngekiFumenEditor.Utils
{
    public static class ViewHelper
    {
        public static UIElement CreateViewByViewModelType<T>() where T : new() => CreateViewByViewModelType(() => new T());

        public static UIElement CreateViewByViewModelType<T>(Func<T> modelGenerator = null)
        {
            var viewModel = modelGenerator is null ? IoC.Get<T>() : modelGenerator();
            return CreateView(viewModel);
        }

        public static UIElement CreateView(object viewModel)
        {
            var mapToAttr = viewModel.GetType().GetCustomAttribute<MapToViewAttribute>();
            var view = (mapToAttr is not null ? CacheLambdaActivator.CreateInstance(mapToAttr.ViewType) : ViewLocator.LocateForModel(viewModel, null, null)) as UIElement;
            ViewModelBinder.Bind(viewModel, view, null);
            return view;
        }
    }
}
