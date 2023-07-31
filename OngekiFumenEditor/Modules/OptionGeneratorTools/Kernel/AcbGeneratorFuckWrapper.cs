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
    public static class AcbGeneratorFuckWrapper
    {
        public static async Task<GenerateResult> Generate(AcbGenerateOption option)
        {
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

                using var fs = typeof(AcbGeneratorFuckWrapper).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.musicTemplate.acb");
                using var ms = new MemoryStream();
                await fs.CopyToAsync(ms);

                var result = await Task.Run(() => AcbGeneratorFuck.Generator.Generate(
                        option.InputAudioFilePath,
                        musicSourceName,
                        tempFolder,
                        false,
                        new VGAudio.Cli.Options()
                        {
                            Bitrate = 320 * 1024,
                        },
                        0,
                        0,
                        ms.ToArray()
                        ));

                var genFiles = Directory.GetFiles(tempFolder);
                if (genFiles.Length < 2)
                    return new(false, "调用AcbGeneratorFuck.Generator.Generate()失败");

                foreach (var genFile in genFiles)
                {
                    var outputPath = Path.Combine(option.OutputFolderPath, Path.GetFileName(genFile));
                    File.Copy(genFile, outputPath, true);
                }

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
