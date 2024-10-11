using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.ArgProcesser.Attributes;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Parser;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using OngekiFumenEditor.Utils.Logs.DefaultImpls;
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
using OngekiFumenEditor.Modules.FumenConverter;
using OngekiFumenEditor.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using Expression = System.Linq.Expressions.Expression;

namespace OngekiFumenEditor.Kernel.ArgProcesser.DefaultImp
{
    [Export(typeof(IProgramArgProcessManager))]
    internal class DefaultArgProcessManager : IProgramArgProcessManager
    {
        void Exit(int code = 0) => ErrorExit(string.Empty, true, code);

        void ErrorExit(string message, bool noDialog, int code = 0)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (noDialog) 
                {
                    var prevColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"{Resources.CliInputErrorHeader}: {message}");
                    Console.ForegroundColor = prevColor;
                }
                else {
                    MessageBox.Show(message, Resources.Error, MessageBoxButton.OK, MessageBoxImage.Stop);
                }
            }

            Application.Current.Shutdown(code);
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
            rootCommand.AddCommand(GenerateVerbCommands<GenerateOption>("svg", Resources.ProgramCommandDescriptionSvg, ProcessSvgCommand));
            rootCommand.AddCommand(GenerateVerbCommands<FumenConvertOption>("convert", Resources.ProgramCommandConvert, ProcessConvertCommand));
            rootCommand.AddCommand(GenerateVerbCommands<JacketGenerateOption>("jacket", Resources.ProgramCommandJacket, ProcessJacketCommand));
            rootCommand.AddCommand(GenerateVerbCommands<AcbGenerateOption>("acb", Resources.ProgramCommandAcb, ProcessAcbCommand));

            var verbosityOption = new Option<bool>(new[] {"--verbose", "-v"}, Resources.ProgramOptionDescriptionVerbose);
            verbosityOption.AddValidator(res =>
            {
                if (res.GetValueOrDefault<bool>())
                    Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();
            });
            rootCommand.AddGlobalOption(verbosityOption);

            await rootCommand.InvokeAsync(args);
            
            Exit();
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
            if (CheckRelativePaths(opt.AudioFilePath, opt.InputFumenFilePath, opt.OutputFilePath))
                return;
            
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
                Exit(1);
            }

            Exit();
        }
        
        private async Task ProcessConvertCommand(FumenConvertOption opt)
        {
            if (CheckRelativePaths(opt.InputFumenFilePath, opt.OutputFumenFilePath))
                return;
            
            var result = await FumenConverterWrapper.Generate(opt);
            if (!result.IsSuccess) {
                await Console.Error.WriteLineAsync($"{Resources.ConvertFail} {result.Message}");
                Exit(1);
                return;
            }

            Exit();
        }

        private async Task ProcessJacketCommand(JacketGenerateOption arg)
        {
            if (CheckRelativePaths(arg.InputImageFilePath, arg.OutputAssetbundleFolderPath))
                return;
            
            GenerateResult result;
            try {
                result = await JacketGenerateWrapper.Generate(arg);
            }
            catch (Exception e) {
                result = new(false, e.Message);
            }
            
            if (!result.IsSuccess) {
                await Console.Error.WriteLineAsync($"{Resources.GenerateJacketFileFail} {result.Message}");
                Exit(1);
                return;
            }

            Exit();
        }

        private async Task ProcessAcbCommand(AcbGenerateOption arg)
        {
            if (CheckRelativePaths(arg.InputAudioFilePath, arg.OutputFolderPath))
                return;
            
            GenerateResult result;
            try {
                result = await AcbGeneratorFuckWrapper.Generate(arg);
            }
            catch (Exception e) {
                result = new(false, e.Message);
            }
            
            if (!result.IsSuccess) {
                await Console.Error.WriteLineAsync($"{Resources.GenerateAudioFileFail} {result.Message}");
                Exit(1);
                return;
            }
            
            Exit();
        }

        private bool CheckRelativePaths(params string[] paths)
        {
            if (paths.Any(path => !Path.IsPathRooted(path))) {
                ErrorExit(Resources.CliArgumentNotAbsolutePath, true, 2);
                return true;
            }

            return false;
        }
        
        #region Option generation
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
        #endregion

    }
}