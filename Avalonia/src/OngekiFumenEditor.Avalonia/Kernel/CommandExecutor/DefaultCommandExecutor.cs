using CommunityToolkit.Mvvm.ComponentModel;
using OngekiFumenEditor.Avalonia.Base;
using OngekiFumenEditor.Avalonia.Kernel.Audio;
using OngekiFumenEditor.Avalonia.Kernel.CommandExecutor.Attributes;
using OngekiFumenEditor.Avalonia.Kernel.ProgramUpdater;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter;
using OngekiFumenEditor.Avalonia.Modules.FumenConverter.Kernel;
using OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor;
using OngekiFumenEditor.Avalonia.Modules.PreviewSvgGenerator;
using OngekiFumenEditor.Avalonia.Parser;
using OngekiFumenEditor.Avalonia.Assets.Languages;
using OngekiFumenEditor.Avalonia.Utils;
using OngekiFumenEditor.Avalonia.Utils.Logs.DefaultImpls;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using Injectio.Attributes;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static OngekiFumenEditor.Avalonia.Modules.FumenVisualEditor.Base.EditorProjectDataUtils;
using OngekiFumenEditor.Avalonia.Avalonia;

namespace OngekiFumenEditor.Avalonia.Kernel.CommandExecutor
{
    [RegisterSingleton<ICommandExecutor>]
    internal class DefaultCommandExecutor : ICommandExecutor
    {
        private readonly RootCommand rootCommand;

        public DefaultCommandExecutor()
        {
            rootCommand = new RootCommand("CommandLine for OngekiFumenEditor")
            {
                GenerateVerbCommands<UpdaterOption>("updater", string.Empty, ProcessUpdaterCommand)
            };

            var verbosityOption = new Option<bool>("--verbose", "-v")
            {
                Description = Lang.ProgramOptionDescriptionVerbose
            };
            verbosityOption.Validators.Add(res =>
            {
                if (res.GetValueOrDefault<bool>())
                    Log.Instance.AddOutputIfNotExist<ConsoleLogOutput>();
            });

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
                    throw new NotSupportedException($"{Lang.DeserializeFumenFileFail}{opt.InputFumenFilePath}");
                var fumen = await fumenDeserializer.DeserializeAsync(fumenFileStream);

                //calculate duration
                if (File.Exists(opt.AudioFilePath))
                {
                    var audioPlayer = await IoC.Get<IAudioManager>().LoadAudioAsync(opt.AudioFilePath);
                    opt.Duration = audioPlayer.Duration;
                }
                else
                {
                    //鍙兘閫氳繃璋遍潰鏉ヨ绠?                    var maxTGrid = fumen.GetAllDisplayableObjects().OfType<ITimelineObject>().Max(x => x.TGrid);
                    maxTGrid += new GridOffset(5, 0);
                    var duration = TGridCalculator.ConvertTGridToAudioTime(maxTGrid, fumen.BpmList);
                    opt.Duration = duration;
                }

                _ = await IoC.Get<IPreviewSvgGenerator>().GenerateSvgAsync(fumen, opt);
                Log.LogInfo(Lang.GenerateSvgSuccess);
            }
            catch (Exception e)
            {
                Log.LogError(Lang.CallGenerateSvgAsyncFail, e);
                return -2;
            }

            return 0;
        }

        private bool CheckRelativePaths(params string[] paths)
        {
            if (paths.Any(path => !Path.IsPathRooted(path)))
            {
                Console.Error.WriteLineAsync(Lang.CliArgumentNotAbsolutePath);
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
                    var name = $"--{attrbuteBase.Name}";
                    if (command.Options.FirstOrDefault(x => x.Name == name) is Option opt)
                    {
                        var valResult = result.GetResult(opt);
                        var val = valResult.GetValueOrDefault<object>();
                        prop.SetValue(obj, val);
                    }
                }
            }

            return obj;
        }
        #endregion

    }
}





