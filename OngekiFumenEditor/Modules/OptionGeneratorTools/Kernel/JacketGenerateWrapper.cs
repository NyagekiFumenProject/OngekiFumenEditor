using AssetsTools.NET;
using AssetsTools.NET.Extra;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Base;
using OngekiFumenEditor.Modules.OptionGeneratorTools.Models;
using OngekiFumenEditor.Properties;
using OngekiFumenEditor.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TexturePlugin;

namespace OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel
{
	public static class JacketGenerateWrapper
	{
		const string CHARS = "abcdef1234567890";
		public static async Task<GenerateResult> Generate(JacketGenerateOption option)
		{
			if (!File.Exists(option.InputImageFilePath))
				return new(false, Resources.InputPictureFileNotFound);

			if (option.MusicId < 0)
				return new(false, Resources.MusicIDInvaild);

			if (string.IsNullOrWhiteSpace(option.OutputAssetbundleFolderPath))
				return new(false, Resources.OutputFolderIsEmpty);
			try
			{
				var jacketName = $"ui_jacket_{option.MusicId.ToString().PadLeft(4, '0')}";
				var tempFolder = TempFileHelper.GetTempFolderPath("JacketGen", jacketName);
				Log.LogDebug($"JacketGenerateWrapper.Generate() tempFolder: {tempFolder}");

				//generate normal
				var tmpInputImageFilePath = Path.Combine(tempFolder, jacketName);
				var tmpOutputPath = Path.Combine(tempFolder, "output");
				Directory.CreateDirectory(tmpOutputPath);
				File.Copy(option.InputImageFilePath, tmpInputImageFilePath, true);

				var outputAbFilePath = Path.Combine(tmpOutputPath, jacketName);
				var abFilePath = await GenerateJacketFileAsync(tmpInputImageFilePath, outputAbFilePath, option.MusicId, false, new(option.Width, option.Height));
				if (!File.Exists(abFilePath))
					return new(false, Resources.GenerateABFileFail);
				Log.LogDebug($"Generate ab file to {abFilePath}");

				//generate small
				outputAbFilePath = Path.Combine(tmpOutputPath, jacketName + "_s");
				abFilePath = await GenerateJacketFileAsync(tmpInputImageFilePath, outputAbFilePath, option.MusicId, true, new(option.WidthSmall, option.HeightSmall));
				Log.LogDebug($"Generate small ab file to {abFilePath}");
				if (!File.Exists(abFilePath))
					return new(false, Resources.GenerateABFileFail);

				if (option.UpdateAssetBytesFile)
				{
					var rawAssetsBytesFilePath = Path.Combine(option.OutputAssetbundleFolderPath, "assets.bytes");
					var result = await UpdateAssetBytesFile(rawAssetsBytesFilePath, jacketName, jacketName + "_s");
					if (!result.IsSuccess)
						return result;
				}

				//copy two assetbundle files
				Directory.GetFiles(tmpOutputPath).ForEach(x => File.Copy(x, Path.Combine(option.OutputAssetbundleFolderPath, Path.GetFileNameWithoutExtension(x)), true));

				return new(true);
			}
			catch (Exception e)
			{
				Log.LogError($"AcbGenerateProgram.Generate() throw exception:{e.Message}\n{e.StackTrace}");
				return new(false, $"{Resources.ThrowExceptionWhenConvert}{e.Message}");
			}
		}

		private static Task<GenerateResult> UpdateAssetBytesFile(string assetBytesFilePath, params string[] names)
		{
			var bundlesCount = 0;
			var bundlesList = new List<(int id, string name, int[] dependencies)>();

			var tmpFile = TempFileHelper.GetTempFilePath("assets.bytes", "assets", ".bytes");
			using var dstFileStream = File.OpenWrite(tmpFile);
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

			File.Copy(tmpFile, assetBytesFilePath, true);
			return Task.FromResult<GenerateResult>(new(true));
		}

		private async static Task<string> GenerateJacketFileAsync(string inputJacketFilePath, string outputAbFilePath, int musicId, bool isSmall, Vector2? targetImgSize = null)
		{
			using var resStream = typeof(JacketGenerateWrapper).Assembly.GetManifestResourceStream("OngekiFumenEditor.Resources.ui_jacket_0666");
			var fs = File.Open(Path.GetTempFileName(), FileMode.Truncate);
			await resStream.CopyToAsync(fs);
			fs.Seek(0, SeekOrigin.Begin);

			var assetManager = new AssetsManager();

			var assetBundleFile = assetManager.LoadBundleFile(fs);
			var assetsFile = assetManager.LoadAssetsFileFromBundle(assetBundleFile, 0);
			var assetsTable = assetsFile.table;

			var origName = "UI_Jacket_0666";

			string getName(bool isUpperCase)
			{
				var musicIdStr = musicId.ToString().PadLeft(4, '0');
				var suffix = isSmall ? (isUpperCase ? "_S" : "_s") : "";
				return (isUpperCase ? $"UI_Jacket_" : "ui_jacket_") + musicIdStr + suffix;
			}

			AssetsReplacer ReplaceAssetBundleMisc()
			{
				var name = $"{getName(false)}";
				var miscName = origName;
				var assetInfo = assetsTable.GetAssetInfo(miscName, 142, false); // 0x1C is texture
				var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();

				baseField.Get("m_Name").GetValue().Set(name);

				var assetPath = "assets/assetbundles/option/" + getName(false) + ".png";
				for (int i = 0; i < baseField.Get("m_Container")[0].childrenCount; i++)
				{
					baseField.Get("m_Container")[0].children[i].Get("first").GetValue().Set(assetPath);
					//baseField.Get("m_Container")[0].children[i].Get("second").Get("asset").Get("m_PathID").GetValue().Set(getPathId(i));
				}

				baseField.Get("m_AssetBundleName").GetValue().Set(name);
				//baseField.Get("m_PreloadTable").children[0][0].Get("m_PathID").GetValue().Set(genPathId);

				var newGoBytes = baseField.WriteToByteArray();
				var assetsReplacer = new AssetsReplacerFromMemory(0, assetInfo.index, (int)assetInfo.curFileType,
																  AssetHelper.GetScriptIndex(assetsFile.file, assetInfo),
																  newGoBytes);

				return assetsReplacer;
			}

			AssetsReplacer ReplaceTexture(out int width, out int height)
			{
				width = default;
				height = default;

				var assetInfo = assetsTable.GetAssetInfo(origName, 0x1C, false);
				var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();

				var fmt = (TextureFormat)baseField.Get("m_TextureFormat").GetValue().AsInt();

				using Image<Rgba32> image = Image.Load<Rgba32>(inputJacketFilePath);
				if (targetImgSize is Vector2 targetSize)
				{
					var targetWidth = (int)targetSize.X;
					var targetHeight = (int)targetSize.Y;
					if (image.Width != targetWidth || image.Height != targetHeight)
					{
						Console.WriteLine($"resize image from ({image.Width},{image.Height}) to ({targetWidth},{targetHeight})");
						image.Mutate(x => x.Resize(new ResizeOptions()
						{
							Size = new Size(targetWidth, targetHeight),
							Mode = ResizeMode.Stretch,
						}));
					}
				}
				image.Mutate(i => i.Flip(FlipMode.Vertical));
				if (!image.DangerousTryGetSinglePixelMemory(out var pixelSpan))
				{
					//warn
					return default;
				}
				width = image.Width;
				height = image.Height;
				byte[] encImageBytes = TextureEncoderDecoder.Encode(MemoryMarshal.AsBytes(pixelSpan.Span).ToArray(), width, height, fmt);

				// Format and save the image data
				var streamData = baseField.Get("m_StreamData");
				streamData.Get("offset").GetValue().Set(0);
				streamData.Get("size").GetValue().Set(0);
				streamData.Get("path").GetValue().Set("");

				baseField.Get("m_TextureFormat").GetValue().Set((int)fmt);
				baseField.Get("m_CompleteImageSize").GetValue().Set(encImageBytes.Length);
				baseField.Get("m_Width").GetValue().Set(width);
				baseField.Get("m_Height").GetValue().Set(height);

				baseField.Get("m_Name").GetValue().Set(getName(true));

				var imageData = baseField.Get("image data");
				imageData.GetValue().type = EnumValueTypes.ByteArray;
				imageData.templateField.valueType = EnumValueTypes.ByteArray;
				var byteArray = new AssetTypeByteArray { size = (uint)encImageBytes.Length, data = encImageBytes };

				// Replace the data
				imageData.GetValue().Set(byteArray);
				var newGoBytes = baseField.WriteToByteArray();
				var assetsReplacer = new AssetsReplacerFromMemory(0, assetInfo.index, (int)assetInfo.curFileType,
																  AssetHelper.GetScriptIndex(assetsFile.file, assetInfo),
																  newGoBytes);

				return assetsReplacer;
			}

			AssetsReplacer UpdateSprite(int width, int height)
			{
				var assetInfo = assetsTable.GetAssetsOfType(213).FirstOrDefault();
				if (assetInfo is null)
				{
					Console.WriteLine($".ab file does not contain any Sprite objects.");
					return default;
				}

				var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();

				baseField["m_Name"].GetValue().Set(getName(true));

				baseField["m_Rect"]["width"].GetValue().Set(width);
				baseField["m_Rect"]["height"].GetValue().Set(height);

				baseField["m_RD"]["textureRect"]["width"].GetValue().Set(width);
				baseField["m_RD"]["textureRect"]["height"].GetValue().Set(height);
				baseField["m_RD"]["uvTransform"]["y"].GetValue().Set(width / 2.0f);
				baseField["m_RD"]["uvTransform"]["w"].GetValue().Set(height / 2.0f);

				var shapeDataField = baseField["m_PhysicsShape"][0]["data"][0];
				var verticesDataField = baseField["m_RD"]["m_VertexData"]["m_DataSize"];

				//dump m_DataSize
				//todo 这里还能塞水印~
				var bytes = verticesDataField.children.Select(x => (byte)x.GetValue().AsInt()).ToArray();
				var floatSpan = MemoryMarshal.Cast<byte, float>(bytes);
				//map byte[] to float[]

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void setShapeDataField(int i, float x, float y)
				{
					shapeDataField[i]["x"].GetValue().Set(x);
					shapeDataField[i]["y"].GetValue().Set(y);
				}

				var hw = width / 200.0f;
				var hh = height / 200.0f;

				setShapeDataField(0, -hw, hh);
				setShapeDataField(1, -hw, -hh);
				setShapeDataField(2, hw, -hh);
				setShapeDataField(3, hw, hh);

				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				void setVertexData(Span<float> d, int i, float x, float y)
				{
					d[i * 3 + 0] = x;
					d[i * 3 + 1] = y;
					/*
					d[i * 5 + 2] = 0;
					d[i * 5 + 3] = u;
					d[i * 5 + 3] = v;
					*/
				}

				setVertexData(floatSpan, 0, -hw, hh);
				setVertexData(floatSpan, 1, hw, hh);
				setVertexData(floatSpan, 2, -hw, -hh);
				setVertexData(floatSpan, 3, hw, -hh);

				verticesDataField.GetValue().type = EnumValueTypes.ByteArray;
				verticesDataField.templateField.valueType = EnumValueTypes.ByteArray;
				verticesDataField.GetValue().Set(new AssetTypeByteArray { size = (uint)bytes.Length, data = bytes });

				var assetsReplacer = new AssetsReplacerFromMemory(0, assetInfo.index, (int)assetInfo.curFileType,
																  AssetHelper.GetScriptIndex(assetsFile.file, assetInfo),
																  baseField.WriteToByteArray());

				return assetsReplacer;
			}

			var textureReplacer = ReplaceTexture(out var width, out var height);
			var miscReplacer = ReplaceAssetBundleMisc();
			var spriteReplacer = UpdateSprite(width, height);

			using var stream = new MemoryStream();
			using var writer = new AssetsFileWriter(stream);
			assetsFile.file.Write(writer, 0, new[] { textureReplacer, miscReplacer, spriteReplacer }.OfType<AssetsReplacer>().ToList(), 0);

			var newAssetData = stream.ToArray();
			var newCabName = $"CAB-{string.Concat(Enumerable.Range(0, 32).Select(x => CHARS[RandomHepler.Random(CHARS.Length)]))}";

			var bundleReplacer = new BundleReplacerFromMemory(assetsFile.name, newCabName, true, newAssetData, -1);

			// Save the new output file
			var bunWriter = new AssetsFileWriter(File.OpenWrite(outputAbFilePath));
			assetBundleFile.file.Write(bunWriter, new List<BundleReplacer> { bundleReplacer });
			assetBundleFile.file.Close();
			bunWriter.Close();

			assetManager.UnloadAll();

			return outputAbFilePath;
		}

		public class ImageData
		{
			public ImageData(int width, int height, byte[] data)
			{
				Width = width;
				Height = height;
				Data = data;
			}

			public int Width { get; }
			public int Height { get; }
			public string Name { get; }

			/// <summary>
			/// Pure RGBA32 array
			/// </summary>
			public byte[] Data { get; }
		}

		public static Task<ImageData> GetMainImageDataAsync(byte[] abFileData, string filePath)
		{
			return Task.Run(async () =>
			{
				var assetManager = new AssetsManager();

				if (abFileData is null)
					abFileData = await File.ReadAllBytesAsync(filePath);
				using var ms = new MemoryStream(abFileData);

				var assetBundleFile = assetManager.LoadBundleFile(ms, filePath);
				var assetsFile = assetManager.LoadAssetsFileFromBundle(assetBundleFile, 0);
				var assetsTable = assetsFile.table;

				var assetInfos = assetsTable.GetAssetsOfType(0x1C);
				foreach (var assetInfo in assetInfos)
				{
					var baseField = assetManager.GetTypeInstance(assetsFile.file, assetInfo).GetBaseField();

					var width = baseField["m_Width"].GetValue().AsInt();
					var height = baseField["m_Height"].GetValue().AsInt();
					var format = (TextureFormat)baseField["m_TextureFormat"].GetValue().AsInt();

					var picData = default(byte[]);
					var beforePath = baseField["m_StreamData"]["path"].GetValue().AsString();

					//try get texture data from stream data
					if (!string.IsNullOrWhiteSpace(beforePath))
					{
						string searchPath = beforePath;
						var offset = baseField["m_StreamData"]["offset"].GetValue().AsUInt();
						var size = baseField["m_StreamData"]["size"].GetValue().AsUInt();

						if (searchPath.StartsWith("archive:/"))
							searchPath = searchPath.Substring("archive:/".Length);

						searchPath = Path.GetFileName(searchPath);
						var reader = assetBundleFile.file.reader;
						var dirInf = assetBundleFile.file.bundleInf6.dirInf;

						for (int i = 0; i < dirInf.Length; i++)
						{
							var info = dirInf[i];
							if (info.name == searchPath)
							{
								reader.Position = assetBundleFile.file.bundleHeader6.GetFileDataOffset() + info.offset + offset;
								picData = reader.ReadBytes((int)size);
								break;
							}
						}
					}

					//try get texture data from image data field
					if ((picData?.Length ?? 0) == 0)
					{
						var imageDataField = baseField["image data"];
						var arr = imageDataField.GetValue().value.asByteArray;
						picData = new byte[arr.size];
						Array.Copy(arr.data, picData, arr.size);
					}

					if ((picData?.Length ?? 0) == 0)
						continue;

					byte[] decData = TextureEncoderDecoder.Decode(picData, width, height, format);

					if (decData == null)
						continue;

					return new ImageData(width, height, decData);
				}

				return default;
			});
		}
	}
}