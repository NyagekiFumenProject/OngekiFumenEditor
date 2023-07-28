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
    public static class JacketGenerateProgram
    {
        private static bool isInit;
        private static string jacketGenExePath;

        public static async Task<bool> CheckOrInit()
        {
            if (!isInit)
            {
                //dump program to tmp
                try
                {
                    if (string.IsNullOrWhiteSpace(jacketGenExePath) || !File.Exists(jacketGenExePath))
                    {
                        using var fs = typeof(AcbGenerateProgram).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.JacketGenerator.exe");
                        var tmp = TempFileHelper.GetTempFilePath("program", "jacketGen", ".exe");
                        using var os = File.OpenWrite(tmp);
                        await fs.CopyToAsync(os);
                        jacketGenExePath = tmp;
                    }

                    isInit = true;
                }
                catch (Exception e)
                {
                    Log.LogError($"Init JacketGenerateProgram failed:{e.Message}");
                    isInit = false;
                }
            }

            return isInit;
        }

        public static async Task<GenerateResult> Generate(JacketGenerateOption option)
        {
            if (isInit)
                return new(false, "需要先调用JacketGenerateProgram.CheckOrInit()");

            if (!File.Exists(option.InputImageFilePath))
                return new(false, "需要生成的图片文件不存在");

            if (option.MusicId < 0)
                return new(false, $"MusicId({option.MusicId})不合法");

            if (string.IsNullOrWhiteSpace(option.OutputAssetbundleFilePath))
                return new(false, "输出文件夹为空");
            try
            {

                var jacketName = $"ui_jacket_{option.MusicId.ToString().PadLeft(4, '0')}";
                var tempFolder = TempFileHelper.GetTempFolderPath("JacketGen", jacketName);

                //generate normal
                var tmpInputImageFilePath = Path.Combine(tempFolder, jacketName);
                var tmpOutputPath = Path.Combine(tempFolder, "output");
                File.Copy(option.InputImageFilePath, tmpInputImageFilePath, true);

                var result = await GenerateInternal(tmpInputImageFilePath, option.Width, option.Height, tmpOutputPath);
                if (!result.IsSuccess)
                    return result;

                //generate small
                tmpInputImageFilePath += "_s";
                File.Copy(option.InputImageFilePath, tmpInputImageFilePath, true);
                result = await GenerateInternal(tmpInputImageFilePath, option.WidthSmall, option.HeightSmall, tmpOutputPath);
                if (!result.IsSuccess)
                    return result;

                if (option.UpdateAssetBytesFile)
                {
                    result = await UpdateAssetBytesFile(jacketName, jacketName + "_s");
                    if (!result.IsSuccess)
                        return result;
                }

                //copy two assetbundle files
                Directory.GetFiles(tmpOutputPath).ForEach(x => File.Copy(x, Path.Combine(option.OutputAssetbundleFilePath, Path.GetFileNameWithoutExtension(x)), true));

                return new(true);
            }
            catch (Exception e)
            {
                Log.LogError($"AcbGenerateProgram.Generate() throw exception:{e.Message}\n{e.StackTrace}");
                return new(false, $"执行时抛出异常:{e.Message}");
            }
        }

        private static Task<GenerateResult> UpdateAssetBytesFile(string assetBytesFilePath, params string[] names)
        {
            var bundlesCount = 0;
            var bundlesList = new List<(int id, string name, int[] dependencies)>();

            var tmpFile = TempFileHelper.GetTempFilePath("assets.bytes", "assets", ".bytes");
            using var dstFileStream = File.OpenRead(tmpFile);
            using var writer = new BinaryWriter(dstFileStream);

            if (File.Exists(assetBytesFilePath))
            {
                using var srcFileStream = File.OpenRead(assetBytesFilePath);
                using var reader = new BinaryReader(srcFileStream);

                bundlesCount = reader.ReadInt32();
                for (int i = 0; i < bundlesCount; i++)
                {
                    var id = reader.ReadInt32();
                    var name = reader.ReadString();
                    var dependencies = Enumerable.Range(0, reader.ReadInt32()).Select(x => reader.ReadInt32()).ToArray();

                    bundlesList.Add((id, name, dependencies));
                }

                Log.LogInfo($"load exist assets.bytes file : {bundlesList.Count} bundle records");
            }

            var needInsertList = names.Except(bundlesList.Select(x => x.name)).ToList();

            if (needInsertList.Count > 0)
            {
                Log.LogInfo($"there are {needInsertList.Count} entries to append/update: {string.Join(", ", needInsertList)}");
            }
            else
            {
                Log.LogInfo($"no new entries to append, skipped.");
            }

            bundlesCount += needInsertList.Count;
            writer.Write(bundlesCount);
            var idx = 0;
            bundlesList.ForEach(x =>
            {
                writer.Write(x.id);
                writer.Write(x.name);
                writer.Write(x.dependencies.Length);
                foreach (var d in x.dependencies)
                    writer.Write(d);
                idx++;
            });

            needInsertList.ForEach(name =>
            {
                writer.Write(idx++);
                writer.Write(name);
                writer.Write(0);
            });
            writer.Flush();
            writer.Close();

            File.Copy(tmpFile, assetBytesFilePath);
            return Task.FromResult<GenerateResult>(new(true));
        }

        private static async Task<GenerateResult> GenerateInternal(string inputFileName, int width, int height, string outputPath)
        {

            var arg = $"--inputFiles \"{inputFileName}\" --outputFolder \"{outputPath}\" --width {width} --height {height} ";
            arg += "--gameType SDDT --mode GenerateJacket --addWatermark --watermarkProof \"nyagekiProj\" --noPause";

            var startInfo = new ProcessStartInfo(jacketGenExePath)
            {
                Arguments = arg,
                CreateNoWindow = true,
                //RedirectStandardOutput = true,
                //RedirectStandardError = true,
            };

            var process = Process.Start(startInfo);
            await process.WaitForExitAsync();

            //check file
            var fileName = Path.GetFileNameWithoutExtension(inputFileName);
            var isSuccess = File.Exists(Path.Combine(outputPath, fileName));
            if (isSuccess)
                return new(true);
            return new(false, "执行Exe生成封面失败:找不到文件");
        }
    }
}
