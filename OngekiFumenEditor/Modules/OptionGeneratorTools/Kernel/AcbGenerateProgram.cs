using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
    public static class AcbGenerateProgram
    {
        private static bool isInit = false;
        private static string acbGenExePath;

        public static async Task<bool> CheckOrInit()
        {
            if (!isInit)
            {
                //dump program to tmp
                try
                {
                    if (string.IsNullOrWhiteSpace(acbGenExePath) || !File.Exists(acbGenExePath))
                    {
                        using var fs = typeof(AcbGenerateProgram).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.AcbGenerator.Fuck.exe");
                        var tmp = TempFileHelper.GetTempFilePath("program", "acbgen", ".exe");
                        using var os = File.OpenWrite(tmp);
                        await fs.CopyToAsync(os);
                        acbGenExePath = tmp;
                    }

                    isInit = true;
                }
                catch (Exception e)
                {
                    Log.LogError($"Init AcbGenerateProgram failed:{e.Message}");
                    isInit = false;
                }
            }

            return isInit;
        }

        public static async Task<GenerateResult> Generate(AcbGenerateOption option)
        {
            if (isInit)
                return new(false, "需要先调用AcbGenerateProgram.CheckOrInit()");

            if (!File.Exists(option.InputAudioFilePath))
                return new(false, "需要转换的音频文件不存在");

            if (option.MusicId < 0)
                return new(false, $"MusicId({option.MusicId})不合法");

            if (string.IsNullOrWhiteSpace(option.OutputFolderPath))
                return new(false, "输出文件夹为空");
            try
            {
                var musicSourceName = $"musicsource{option.MusicId}";
                var tempFolder = TempFileHelper.GetTempFolderPath("AcbGen", musicSourceName);

                var arg = $"--defaultOutputFolder \"{tempFolder}\" --inputAudioFiles \"{option.InputAudioFilePath}\" --inputAudioNamePrefixes \"{musicSourceName}\" --noPause";

                var startInfo = new ProcessStartInfo(acbGenExePath)
                {
                    Arguments = arg,
                    CreateNoWindow = true,
                    //RedirectStandardOutput = true,
                    //RedirectStandardError = true,
                };

                var process = Process.Start(startInfo);
                await process.WaitForExitAsync();

                //check
                var genAcbFilePath = Path.Combine(tempFolder, musicSourceName + ".acb");
                var genAwbFilePath = Path.Combine(tempFolder, musicSourceName + ".awb");

                if (!(File.Exists(genAcbFilePath) && File.Exists(genAwbFilePath)))
                    return new(false, "调用exe生成失败");

                Directory.CreateDirectory(option.OutputFolderPath);

                var targetAcbFilePath = Path.Combine(option.OutputFolderPath, musicSourceName + ".acb");
                var targetAwbFilePath = Path.Combine(option.OutputFolderPath, musicSourceName + ".awb");

                File.Copy(genAcbFilePath, targetAcbFilePath, true);
                File.Copy(genAwbFilePath, targetAwbFilePath, true);

                return new(true);
            }
            catch (Exception e)
            {
                Log.LogError($"AcbGenerateProgram.Generate() throw exception:{e.Message}\n{e.StackTrace}");
                return new(false, $"执行时抛出异常:{e.Message}");
            }
        }
    }
}
