using Caliburn.Micro;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Kernel.Audio;
using OngekiFumenEditor.Kernel.CommandExecutor.Attributes;
using OngekiFumenEditor.Kernel.ProgramUpdater;
using OngekiFumenEditor.Modules.FumenConverter;
using OngekiFumenEditor.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Modules.FumenVisualEditor;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
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
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Modules.FumenVisualEditor.Base.EditorProjectDataUtils;

namespace OngekiFumenEditor.Kernel.CommandExecutor
{
    [Export(typeof(ICommandExecutor))]
    internal class DefaultCommandExecutor : ICommandExecutor
    {
        private readonly RootCommand rootCommand;

        public DefaultCommandExecutor()
        {
            rootCommand = new RootCommand("CommandLine for OngekiFumenEditor")
            {
                GenerateVerbCommands<SvgGenerateOption>("svg", Resources.ProgramCommandDescriptionSvg, ProcessSvgCommand),
                GenerateVerbCommands<FumenConvertOption>("convert", Resources.ProgramCommandConvert, ProcessConvertCommand),
                GenerateVerbCommands<JacketGenerateOption>("jacket", Resources.ProgramCommandJacket, ProcessJacketCommand),
                GenerateVerbCommands<AcbGenerateOption>("acb", Resources.ProgramCommandAcb, ProcessAcbCommand),
                GenerateVerbCommands<UpdaterOption>("updater", string.Empty, ProcessUpdaterCommand)
            };

            var verbosityOption = new Option<bool>("--verbose", "-v")
            {
                Description = Resources.ProgramOptionDescriptionVerbose
            };
            verbosityOption.Validators.Add(res =>
            {
                if (res.GetValueOrDefault<bool>())
                    Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();
            });

            //rootCommand.AddGlobalOption(verbosityOption);

            void AddGlobalOption(Command c, Option o)
            {
                c.Options.Add(o);
                foreach (var sc in c.Subcommands)
                    AddGlobalOption(sc, o);
            }

            AddGlobalOption(rootCommand, verbosityOption);
        }

        private async Task<int> ProcessUpdaterCommand(UpdaterOption option)
        {
            var (exitCode, message) = IoC.Get<IProgramUpdater>().CommandExecuteUpdate(option);
            if (exitCode != 0)
                await Console.Error.WriteLineAsync(message);
            return exitCode;
        }

        public Task<int> Execute(string[] args)
            => rootCommand.Parse(args).InvokeAsync();

        private Command GenerateVerbCommands<T>(string verb, string description, Func<T, Task<int>> callbackFunc) where T : new()
        {
            var command = new Command(verb, description);
            foreach (var option in GenerateOptionsByAttributes<T>())
                command.Options.Add(option);

            command.SetAction(async parseResult =>
            {
                var opt = Generate<T>(command, parseResult);
                return await callbackFunc(opt);
            });

            return command;
        }

        private async Task<int> ProcessSvgCommand(SvgGenerateOption opt)
        {
            if (CheckRelativePaths(opt.AudioFilePath, opt.InputFumenFilePath, opt.OutputFilePath))
                return -1;

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
                return -2;
            }

            return 0;
        }

        private async Task<int> ProcessConvertCommand(FumenConvertOption opt)
        {
            if (CheckRelativePaths(opt.InputFumenFilePath, opt.OutputFumenFilePath))
                return -3;

            var result = await FumenConverterWrapper.Generate(opt);
            if (!result.IsSuccess)
            {
                await Console.Error.WriteLineAsync($"{Resources.ConvertFail} {result.Message}");
                return -4;
            }

            return 0;
        }

        private async Task<int> ProcessJacketCommand(JacketGenerateOption arg)
        {
            if (CheckRelativePaths(arg.InputImageFilePath, arg.OutputAssetbundleFolderPath))
                return -5;

            GenerateResult result;
            try
            {
                result = await JacketGenerateWrapper.Generate(arg);
            }
            catch (Exception e)
            {
                result = new(false, e.Message);
            }

            if (!result.IsSuccess)
            {
                await Console.Error.WriteLineAsync($"{Resources.GenerateJacketFileFail} {result.Message}");
                return -6;
            }

            return 0;
        }

        private async Task<int> ProcessAcbCommand(AcbGenerateOption arg)
        {
            if (CheckRelativePaths(arg.InputAudioFilePath, arg.OutputFolderPath))
                return -7;

            GenerateResult result;
            try
            {
                result = await AcbGeneratorFuckWrapper.Generate(arg);
            }
            catch (Exception e)
            {
                result = new(false, e.Message);
            }

            if (!result.IsSuccess)
            {
                await Console.Error.WriteLineAsync($"{Resources.GenerateAudioFileFail} {result.Message}");
                return -8;
            }

            return 0;
        }

        private bool CheckRelativePaths(params string[] paths)
        {
            if (paths.Any(path => !Path.IsPathRooted(path)))
            {
                Console.Error.WriteLineAsync(Resources.CliArgumentNotAbsolutePath);
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
                    var funcType = typeof(Func<,>).MakeGenericType(typeof(ArgumentResult), attrbuteBase.Type);
                    var arg = Expression.Parameter(typeof(ArgumentResult), "result");
                    var valParam = Expression.Constant(attrbuteBase.DefaultValue, attrbuteBase.Type);
                    var lambda = Expression.Lambda(funcType, valParam, arg);
                    var func = lambda.Compile();

                    var optionType = typeof(Option<>).MakeGenericType(attrbuteBase.Type);
                    var optName = $"--{attrbuteBase.Name}";

                    var option = (Option)LambdaActivator.CreateInstance(optionType, optName, new string[0]);
                    option.Required = attrbuteBase.Require;
                    option.Description = attrbuteBase.Description;

                    optionType.GetProperty(nameof(Option<>.DefaultValueFactory)).SetValue(option, func);

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
                    if (command.Options.FirstOrDefault(x => x.Name == name) is Option<T> opt)
                    {
                        var val = result.GetValue(opt);
                        prop.SetValue(obj, val);
                    }
                }
            }

            return obj;
        }
        #endregion

    }
}
