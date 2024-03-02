using OngekiFumenEditor.Modules.OptionGeneratorTools.Kernel;
using SharpVectors.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;

namespace OngekiFumenEditor.Utils
{
	[Export(typeof(ImageLoader))]
	public class ImageLoader
	{
		private readonly byte[] abMagic = "UnityFS"u8.ToArray();

		private const int ParallelCount = 2;
		private readonly ConcurrentDictionary<string, WeakReference<byte[]>> cacheMap = new();
		private readonly ConcurrentStack<LoadTask> tasks = new();

		public ImageLoader()
		{
			Task.Run(PrcessQueue);
		}

		public Task<byte[]> LoadImage(string url, CancellationToken cancellationToken)
		{
			var taskCompleteSource = new TaskCompletionSource<byte[]>();
			tasks.Push(new LoadTask(taskCompleteSource, url));
			return taskCompleteSource.Task;
		}

		private async void PrcessQueue()
		{
			var currentRunningTasks = new List<Task>(ParallelCount);
			while (true)
			{
				for (var i = 0; i < ParallelCount; i++)
					if (tasks.TryPop(out var task))
						currentRunningTasks.Add(Task.Run(() =>
						{
							var url = task.url;
							var taskSource = task.TaskSource;

							return ProcessTask(url, taskSource);
						}));

				if (currentRunningTasks.Count > 0)
					await Task.WhenAll(currentRunningTasks);
				currentRunningTasks.Clear();
				await Task.Delay(0);
			}
		}

		private async ValueTask ProcessTask(string path, TaskCompletionSource<byte[]> taskSource)
		{
			using var md5 = MD5.Create();
			var hash = Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(path)));

			var isNetworkLoad = path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase);

			var data = await LoadFromInMemory(path);
			if (data != null)
			{
				taskSource.SetResult(data);
				return;
			}

			if (isNetworkLoad)
			{
				data = await LoadCache(hash);
				if (data != null)
				{
					taskSource.SetResult(data);
					return;
				}
			}

			data = await Load(path, isNetworkLoad);
			if (data == null)
			{
				taskSource.SetResult(null);
				return;
			}
			taskSource.SetResult(data);

			if (isNetworkLoad)
				await SaveCache(hash, data);
			await SaveFromInMemory(hash, data);
		}

		private string GetCacheFile(string hash) => TempFileHelper.GetTempFilePath("images", hash, "img.cache", false);

		private async Task SaveCache(string hash, byte[] data)
		{
			var filePath = GetCacheFile(hash);
			await File.WriteAllBytesAsync(filePath, data);
		}

		private async Task<byte[]> LoadCache(string hash)
		{
			var filePath = GetCacheFile(hash);
			if (File.Exists(filePath))
				return await File.ReadAllBytesAsync(filePath);
			return null;
		}

		private ValueTask SaveFromInMemory(string hash, byte[] data)
		{
			cacheMap[hash] = new WeakReference<byte[]>(data);
			return ValueTask.CompletedTask;
		}

		private ValueTask<byte[]> LoadFromInMemory(string hash)
		{
			if (cacheMap.TryGetValue(hash, out var weakReference))
				if (weakReference.TryGetTarget(out var data))
					return ValueTask.FromResult(data);

			return ValueTask.FromResult(default(byte[]));
		}

		private async ValueTask<byte[]> Load(string path, bool isNetworkLoad)
		{
			async ValueTask<byte[]> GetRaw()
			{
				try
				{
					if (isNetworkLoad)
					{
						using var httpClient = new HttpClient();
						return await httpClient.GetByteArrayAsync(path);
					}
					else
					{
						return await File.ReadAllBytesAsync(path);
					}
				}
				catch (Exception e)
				{
					Log.LogError($"load {path} failed", e);
					return default;
				}
			}

			var r = await GetRaw();

			if (r.Length >= abMagic.Length)
			{
				var isABFile = true;
				for (var i = 0; i < abMagic.Length; i++)
				{
					if (abMagic[i] != r[i])
					{
						isABFile = false;
						break;
					}
				}

				if (isABFile)
				{
					var imgData = await JacketGenerateWrapper.GetMainImageDataAsync(r, path);
					using var image = Image.LoadPixelData<Rgba32>(imgData.Data, imgData.Width, imgData.Height);
					var memoryStream = new MemoryStream();
					image.Mutate(i => i.Flip(FlipMode.Vertical));
					image.SaveAsPng(memoryStream);
					memoryStream.Seek(0, SeekOrigin.Begin);

					return memoryStream.ToArray();
				}
			}

			return r;
		}

		private record LoadTask(TaskCompletionSource<byte[]> TaskSource, string url);

	}
}
