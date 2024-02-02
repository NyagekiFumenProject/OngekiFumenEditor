using Caliburn.Micro;
using NWaves.Audio.Interfaces;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.AudioAdjustWindow;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Base.EditorProjectDataUtils;
using Expression = System.Linq.Expressions.Expression;

namespace OngekiFumenEditor.Kernel.ArgProcesser.DefaultImp
{

    [Export(typeof(IProgramArgProcessManager))]
    internal class DefaultArgProcessManager : IProgramArgProcessManager
    {
        private ParentConsole console;

        public DefaultArgProcessManager()
        {
            console = new();
        }

        void ErrorExit(string message, bool noDialog)
        {
            if (noDialog)
                console.WriteLine(message);
            else
                MessageBox.Show(message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Stop);
        }

        public async Task ProcessArgs(string[] args)
        {
            if (args.Length == 0)
                return;

            if (args.Length == 1)
            {
                var filePath = args[0];

                if (File.Exists(filePath))
                {
                    Log.LogInfo($"arg.filePath: {filePath}");

                    if (await DocumentOpenHelper.TryOpenAsDocument(filePath))
                        Application.Current?.MainWindow?.Focus();

                    return;
                }
            }

            var rootCommand = GenerateVerbCommands();
            await rootCommand.InvokeAsync(args, console);
        }

        private class ParentConsole : IConsole
        {
            private class Writer : IStandardStreamWriter
            {
                const uint parentProcessId = 0x0ffffffff;

                [DllImport("kernel32.dll")]
                static extern bool AttachConsole(uint dwProcessId);

                public void Write(string value)
                {
                    var currentProcessId = (uint)Environment.ProcessId;
                    AttachConsole(parentProcessId);

                    using var writer = new StreamWriter(Console.OpenStandardOutput());

                    writer.Write(value);
                    writer.Flush();

                    AttachConsole(currentProcessId);

                    Log.LogDebug(value.Replace("\n", string.Empty).Replace("\r", string.Empty));
                }
            }

            public IStandardStreamWriter Out { get; } = new Writer();

            public bool IsOutputRedirected => false;

            public IStandardStreamWriter Error { get; } = new Writer();

            public bool IsErrorRedirected => false;

            public bool IsInputRedirected => false;
        }

        IEnumerable<Option> GenerateOptionsByAttributes<T>()
        {
            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.GetCustomAttribute<OptionBindingAttrbuteBase>() is OptionBindingAttrbuteBase attrbuteBase)
                {
                    var funcType = typeof(Func<>).MakeGenericType(attrbuteBase.Type);
                    var valParam = Expression.Constant(attrbuteBase.DefaultValue, attrbuteBase.Type);
                    var lambda = Expression.Lambda(funcType, valParam);
                    var func = lambda.Compile();

                    var optionType = typeof(Option<>).MakeGenericType(attrbuteBase.Type);
                    var optName = $"--{attrbuteBase.Name}";

                    var option = (Option)LambdaActivator.CreateInstance(optionType, optName, func, attrbuteBase.Description);
                    option.IsRequired = attrbuteBase.Require;

                    yield return option;
                }
            }
        }

        T Generate<T>(Command command, ParseResult result) where T : new()
        {
            var obj = new T();

            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.GetCustomAttribute<OptionBindingAttrbuteBase>() is OptionBindingAttrbuteBase attrbuteBase)
                {
                    var name = $"{attrbuteBase.Name}";
                    if (command.Options.FirstOrDefault(x => x.Name == name) is Option opt)
                    {
                        var val = result.GetValueForOption(opt);
                        prop.SetValue(obj, val);
                    }
                }
            }

            return obj;
        }

        private Command GenerateVerbCommands()
        {
            var root = new RootCommand("CommandLine for OngekiFumenEditor");

            #region svg

            var svgCommand = new Command("svg", "生成预览谱面.svg文件");
            var options = GenerateOptionsByAttributes<GenerateOption>();
            foreach (var option in options)
                svgCommand.AddOption(option);

            svgCommand.SetHandler(async ctx =>
            {
                var opt = Generate<GenerateOption>(svgCommand, ctx.ParseResult);
                await ProcessSvgCommand(opt);
            });

            root.AddCommand(svgCommand);

            #endregion

            return root;
        }

        private async Task ProcessSvgCommand(GenerateOption opt)
        {
            using var fumenFileStream = File.OpenRead(opt.InputFumenFilePath);
            var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(opt.InputFumenFilePath);
            if (fumenDeserializer is null)
                throw new NotSupportedException($"{Resources.DeserializeFumenFileFail}{opt.InputFumenFilePath}");
            var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);

            //calculate duration
            if (File.Exists(opt.AudioFilePath))
            {
                try
                {
                    var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(opt.AudioFilePath);
                    opt.Duration = audioPlayer.Duration;
                }
                catch (Exception e)
                {
                    ErrorExit($"无法处理verb命令:{e.Message}", true);
                }
            }
            else
            {
                //只能通过谱面来计算
                var maxTGrid = fumen.GetAllDisplayableObjects().OfType<ITimelineObject>().Max(x => x.TGrid);
                maxTGrid += new GridOffset(5, 0);
                var duration = TGridCalculator.ConvertTGridToAudioTime(maxTGrid, fumen.BpmList);
                opt.Duration = duration;
            }

            try
            {
                _ = await IoC.Get<IPreviewSvgGenerator>().GenerateSvgAsync(fumen, opt);
                console.WriteLine($"生成谱面预览.svg文件成功");
                Application.Current.Shutdown(0);
            }
            catch (Exception e)
            {
                console.Error.WriteLine($"执行GenerateSvgAsync()出错:{e.Message}");
                Application.Current.Shutdown(-1);
            }
        }
    }
}
