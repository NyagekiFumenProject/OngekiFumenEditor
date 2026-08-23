using System;
using System.Collections.Concurrent;
using Injectio.Attributes;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OngekiFumenEditor.Avalonia.Platforms.Services.FileSystem.Providers;

namespace OngekiFumenEditor.Avalonia.Utils
{
    [RegisterSingleton<ImageLoader>]
    public class ImageLoader
    {
        private readonly byte[] assetBundleMagic = "UnityFS"u8.ToArray();

        private const int ParallelCount = 2;
        private readonly ConcurrentDictionary<string, WeakReference<byte[]>> cacheMap = new();
        private readonly ConcurrentStack<LoadTask> tasks = new();
        private readonly Func<string, CancellationToken, Task<byte[]>> download;
        private readonly ITemporaryFolderProvider temporaryFolderProvider;

        private volatile bool isProcessing = false;

        public ImageLoader(ITemporaryFolderProvider temporaryFolderProvider)
            : this(temporaryFolderProvider, DownloadAsync)
        {
        }

        internal ImageLoader(
            ITemporaryFolderProvider temporaryFolderProvider,
            Func<string, CancellationToken, Task<byte[]>> download)
        {
            ArgumentNullException.ThrowIfNull(temporaryFolderProvider);
            ArgumentNullException.ThrowIfNull(download);
            this.temporaryFolderProvider = temporaryFolderProvider;
            this.download = download;
        }

        public Task<byte[]> LoadImage(string url, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<byte[]>(cancellationToken);

            var taskCompleteSource = new TaskCompletionSource<byte[]>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            tasks.Push(new LoadTask(taskCompleteSource, url, cancellationToken));
            PrcessQueue();
            return taskCompleteSource.Task;
        }

        private async void PrcessQueue()
        {
            if (isProcessing)
                return;
            isProcessing = true;

            var currentTaskRunningCount = 0;
            while (!tasks.IsEmpty)
            {
                if (currentTaskRunningCount >= ParallelCount)
                {
                    await Task.Yield();
                    continue;
                }
                Interlocked.Increment(ref currentTaskRunningCount);

                if (tasks.TryPop(out var task))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessTask(task.Url, task.TaskSource, task.CancellationToken);
                        }
                        catch (OperationCanceledException) when (task.CancellationToken.IsCancellationRequested)
                        {
                            task.TaskSource.TrySetCanceled(task.CancellationToken);
                        }
                        catch (Exception exception)
                        {
                            task.TaskSource.TrySetException(exception);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref currentTaskRunningCount);
                        }
                    });
                }
                else
                {
                    Interlocked.Decrement(ref currentTaskRunningCount);
                }
            }

            isProcessing = false;
        }

        private async ValueTask ProcessTask(
            string path,
            TaskCompletionSource<byte[]> taskSource,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var md5 = MD5.Create();
            var hash = Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(path)));

            var isNetworkLoad = path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase);

            var data = await LoadFromInMemory(hash);
            if (data != null)
            {
                taskSource.TrySetResult(data);
                return;
            }

            if (isNetworkLoad)
            {
                data = await LoadCache(hash, cancellationToken);
                if (data != null)
                {
                    taskSource.TrySetResult(data);
                    return;
                }
            }

            data = await Load(path, isNetworkLoad, cancellationToken);
            if (data == null)
            {
                taskSource.TrySetResult(null);
                return;
            }

            if (isNetworkLoad)
                await SaveCache(hash, data, cancellationToken);
            await SaveFromInMemory(hash, data);
            taskSource.TrySetResult(data);
        }

        private async Task SaveCache(string hash, byte[] data, CancellationToken cancellationToken)
        {
            var cacheFolder = await temporaryFolderProvider.Root
                .GetOrCreateDirectoryAsync("images", cancellationToken);
            var cacheFile = await cacheFolder.GetOrCreateFileAsync(
                $"{hash}.img.cache",
                cancellationToken);
            await cacheFile.WriteAllBytesAsync(data, cancellationToken);
        }

        private async Task<byte[]> LoadCache(string hash, CancellationToken cancellationToken)
        {
            var cacheFolder = await temporaryFolderProvider.Root
                .GetOrCreateDirectoryAsync("images", cancellationToken);
            var cacheFile = await cacheFolder.TryGetFileAsync(
                $"{hash}.img.cache",
                cancellationToken);
            return cacheFile is null
                ? null
                : await cacheFile.ReadAllBytesAsync(cancellationToken);
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

        private async ValueTask<byte[]> Load(
            string path,
            bool isNetworkLoad,
            CancellationToken cancellationToken)
        {
            async ValueTask<byte[]> GetRaw()
            {
                try
                {
                    if (isNetworkLoad)
                    {
                        return await download(path, cancellationToken);
                    }
                    else
                    {
                        return await File.ReadAllBytesAsync(path, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Log.LogError($"load {path} failed", e);
                    return default;
                }
            }

            var r = await GetRaw();

            if (r is not null && r.Length >= assetBundleMagic.Length)
            {
                var isABFile = true;
                for (var i = 0; i < assetBundleMagic.Length; i++)
                {
                    if (assetBundleMagic[i] != r[i])
                    {
                        isABFile = false;
                        break;
                    }
                }

                if (isABFile)
                {
                    Log.LogWarn($"Unity asset bundle image loading is not supported: {path}");
                    return default;
                }
            }

            return r;
        }

        private static async Task<byte[]> DownloadAsync(
            string url,
            CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            return await httpClient.GetByteArrayAsync(url, cancellationToken);
        }

        private sealed record LoadTask(
            TaskCompletionSource<byte[]> TaskSource,
            string Url,
            CancellationToken CancellationToken);
    }
}
