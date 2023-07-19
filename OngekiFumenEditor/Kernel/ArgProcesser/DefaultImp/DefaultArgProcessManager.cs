using Caliburn.Micro;
using Gemini.Framework.Results;
using Gemini.Framework.Services;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Ogkr;
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

            if (args.LastOrDefault() is string filePath)
            {
                if (File.Exists(filePath))
                {
                    Log.LogInfo($"arg.filePath: {filePath}");

                    //存在，就检查要提供的文件是什么类型了
                    if (IoC.GetAll<IEditorProvider>().FirstOrDefault(x => x.Handles(filePath)) is IEditorProvider provider)
                    {
                        Log.LogInfo($"通过命令行快速打开文档:({provider}) {filePath}");
                        await Dispatcher.Yield();
                        var openDocument = Show.Document(filePath);
                        await Coroutine.ExecuteAsync(new IResult[] { openDocument }.AsEnumerable().GetEnumerator());
                        return;
                    }
                    else if (filePath.EndsWith(".ogkr"))
                    {
                        if (await FastOpenOgkrFumen.TryOpenAsDocument(filePath))
                        {
                            Log.LogInfo($"通过命令行快速打开ogkr文件:{filePath}");
                            return;
                        }
                    }

                    ErrorExit("提供的文件编辑器无法打开处理");
                }
                else
                {
                    ErrorExit("通过参数提供的文件不存在");
                }
            }
        }
    }
}
