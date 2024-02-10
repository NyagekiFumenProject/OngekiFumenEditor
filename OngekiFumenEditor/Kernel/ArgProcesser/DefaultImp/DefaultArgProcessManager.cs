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
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Expression = System.Linq.Expressions.Expression;

namespace OngekiFumenEditor.Kernel.ArgProcesser.DefaultImp
{
    [Export(typeof(IProgramArgProcessManager))]
    internal class DefaultArgProcessManager : IProgramArgProcessManager
    {
        void Exit() => ErrorExit(string.Empty, true);

        void ErrorExit(string message, bool noDialog)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (noDialog)
                    Log.LogError(message);
                else
                    MessageBox.Show(message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Stop);
            }

            Application.Current.Shutdown();
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

                    _ = Application.Current.Dispatcher.Invoke(async () =>
                    {
                        if (await DocumentOpenHelper.TryOpenAsDocument(filePath))
                            Application.Current?.MainWindow?.Focus();
                    });

                    return;
                }
            }

            var rootCommand = new RootCommand("CommandLine for OngekiFumenEditor");
            rootCommand.AddCommand(GenerateVerbCommands<GenerateOption>("svg", "生成预览谱面.svg文件", ProcessSvgCommand));
            await rootCommand.InvokeAsync(args);
            Exit();
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

        private Command GenerateVerbCommands<T>(string verb, string description, Func<T, Task> callbackFunc) where T : new()
        {
            var command = new Command(verb, description);
            foreach (var option in GenerateOptionsByAttributes<T>())
                command.AddOption(option);

            command.SetHandler(async ctx =>
            {
                var opt = Generate<T>(command, ctx.ParseResult);
                await callbackFunc(opt);
            });
            return command;
        }

        private async Task ProcessSvgCommand(GenerateOption opt)
        {
            Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();

            try
            {
                using var fumenFileStream = File.OpenRead(opt.InputFumenFilePath);
                var fumenDeserializer = IoC.Get<IFumenParserManager>().GetDeserializer(opt.InputFumenFilePath);
                if (fumenDeserializer is null)
                    throw new NotSupportedException($"{Resources.DeserializeFumenFileFail}{opt.InputFumenFilePath}");
                var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);

                //calculate duration
                if (File.Exists(opt.AudioFilePath))
                {
                    var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(opt.AudioFilePath);
                    opt.Duration = audioPlayer.Duration;
                }
                else
                {
                    //只能通过谱面来计算
                    var maxTGrid = fumen.GetAllDisplayableObjects().OfType<ITimelineObject>().Max(x => x.TGrid);
                    maxTGrid += new GridOffset(5, 0);
                    var duration = TGridCalculator.ConvertTGridToAudioTime(maxTGrid, fumen.BpmList);
                    opt.Duration = duration;
                }

                _ = await IoC.Get<IPreviewSvgGenerator>().GenerateSvgAsync(fumen, opt);
                Log.LogInfo(Resources.GenerateSvgSuccess);
            }
            catch (Exception e)
            {
                Log.LogError(Resources.CallGenerateSvgAsyncFail, e);
            }

            Exit();
        }
    }
}
