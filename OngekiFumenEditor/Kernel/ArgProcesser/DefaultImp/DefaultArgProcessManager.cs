using Caliburn.Micro;
using Gemini.Framework.Results;
using Gemini.Framework.Services;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OngekiFumenEditor.Kernel.ArgProcesser.DefaultImp
{
    [Export(typeof(IProgramArgProcessManager))]
    internal class DefaultArgProcessManager : IProgramArgProcessManager
    {
        public async Task ProcessArgs(string[] args)
        {
            void ErrorExit(string message)
            {
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Stop);
                Application.Current.Shutdown(-1);
            }

            if (args.LastOrDefault() is string projPath)
            {
                if (File.Exists(projPath))
                {
                    if (IoC.GetAll<IEditorProvider>().FirstOrDefault(x => x.Handles(projPath)) is IEditorProvider provider)
                    {
                        Log.LogInfo($"通过命令行快速打开:({provider}) {projPath}");
                        await Dispatcher.Yield();
                        var openDocument = Show.Document(projPath);
                        await Coroutine.ExecuteAsync(new IResult[] { openDocument }.AsEnumerable().GetEnumerator());
                    }
                    else
                    {
                        ErrorExit("不支持加载参数打开的项目路径");
                    }
                }
                else
                {
                    ErrorExit("参数打开的项目路径不存在");
                }
            }
        }
    }
}
