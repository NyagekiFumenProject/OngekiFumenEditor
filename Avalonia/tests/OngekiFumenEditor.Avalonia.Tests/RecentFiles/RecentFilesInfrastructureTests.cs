using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using Gekimini.Avalonia.Framework;
using Gekimini.Avalonia.Framework.Languages;
using Gekimini.Avalonia.Framework.RecentFiles;
using Gekimini.Avalonia.Framework.RecentFiles.DefaultImpl;
using Gekimini.Avalonia.Models.Settings;
using Gekimini.Avalonia.Platforms.Services.Settings;
using Xunit;

namespace OngekiFumenEditor.Avalonia.Tests.RecentFiles;

public sealed class RecentFilesInfrastructureTests
{
    private static readonly EditorFileType FileType = new("test-project", LocalizedString.CreateFromRawText("Test Project"));

    [Fact]
    public void PostRecent_WithData_CreatesStableIdAndCommitsOneAggregateSnapshot()
    {
        var settings = new RecordingSettingManager();
        var manager = new DefaultEditorRecentFilesManager(settings);
        settings.ResetSaveCounts();
        var sourceData = new byte[] { 1, 2, 3 };

        var record = manager.PostRecent(FileType, "Project", "folder/project.nyagekiProj", sourceData);
        sourceData[0] = 99;

        Assert.NotEqual(Guid.Empty, record.RecordId);
        Assert.Equal("folder/project.nyagekiProj", record.LocationDescription);
        Assert.Equal(new byte[] { 1, 2, 3 }, manager.ReadData(record));
        Assert.Equal(1, settings.GetSaveCount<RecentRecordInfoStoreSetting>());
        Assert.Equal(0, settings.GetSaveCount<RecentRecordDataStoreSetting>());
    }

    [Fact]
    public void UpdateRecent_ExistingInvalidRecord_PreservesIdMovesToFrontAndReplacesData()
    {
        var manager = new DefaultEditorRecentFilesManager(new RecordingSettingManager());
        var first = manager.PostRecent(FileType, "First", "first", new byte[] { 1 });
        var second = manager.PostRecent(FileType, "Second", "second", new byte[] { 2 });
        manager.SetMarkedInvalid(first, true);

        var updated = manager.UpdateRecent(first.RecordId, "First Updated", "first/new", new byte[] { 7, 8 });

        Assert.Equal(first.RecordId, updated.RecordId);
        Assert.Equal([first.RecordId, second.RecordId], manager.RecentRecordInfos.Select(x => x.RecordId));
        Assert.Equal(new byte[] { 7, 8 }, manager.ReadData(updated));
        Assert.False(manager.IsMarkedInvalid(updated));
        Assert.Equal(new byte[] { 2 }, manager.ReadData(second));
    }

    [Fact]
    public void PostRecent_OverCapacity_EvictsOldestRecordAndAllAssociatedState()
    {
        var settings = new RecordingSettingManager();
        settings.Set(new RecentRecordInfoStoreSetting { RecordMaxCount = 2 });
        var manager = new DefaultEditorRecentFilesManager(settings);
        var oldest = manager.PostRecent(FileType, "Oldest", "oldest", new byte[] { 1 });
        manager.SetMarkedInvalid(oldest, true);
        var middle = manager.PostRecent(FileType, "Middle", "middle", new byte[] { 2 });
        var newest = manager.PostRecent(FileType, "Newest", "newest", new byte[] { 3 });

        Assert.Equal([newest.RecordId, middle.RecordId], manager.RecentRecordInfos.Select(x => x.RecordId));
        Assert.Null(manager.ReadData(oldest));
        Assert.False(manager.IsMarkedInvalid(oldest));
        Assert.False(manager.RemoveRecent(oldest.RecordId));
    }

    [Fact]
    public void RemoveRecent_ExistingRecord_RemovesMetadataDataAndInvalidState()
    {
        var manager = new DefaultEditorRecentFilesManager(new RecordingSettingManager());
        var record = manager.PostRecent(FileType, "Project", "project", new byte[] { 4 });
        manager.SetMarkedInvalid(record, true);

        var removed = manager.RemoveRecent(record.RecordId);

        Assert.True(removed);
        Assert.Empty(manager.RecentRecordInfos);
        Assert.Null(manager.ReadData(record));
        Assert.False(manager.IsMarkedInvalid(record));
    }

    [Fact]
    public void ClearAllRecordsAndDatas_RemovesAggregateStateAndRestartsValidation()
    {
        var coordinator = new RecordingValidityCoordinator();
        var manager = new DefaultEditorRecentFilesManager(new RecordingSettingManager(), coordinator);
        var first = manager.PostRecent(FileType, "First", "first", new byte[] { 1 });
        var second = manager.PostRecent(FileType, "Second", "second", new byte[] { 2 });
        manager.SetMarkedInvalid(first, true);

        manager.ClearAllRecordsAndDatas();

        Assert.Empty(manager.RecentRecordInfos);
        Assert.Null(manager.ReadData(first));
        Assert.Null(manager.ReadData(second));
        Assert.False(manager.IsMarkedInvalid(first));
        Assert.Equal(1, coordinator.BeginGenerationCallCount);

        var replacement = manager.PostRecent(FileType, "Replacement", "replacement", new byte[] { 3 });
        Assert.Equal(new byte[] { 3 }, manager.ReadData(replacement));
    }

    [Fact]
    public void Constructor_LegacyStore_DiscardsLocationKeyedRecordsAndData()
    {
        var settings = new RecordingSettingManager();
        settings.Set(new RecentRecordInfoStoreSetting
        {
            Version = 1,
            RecordMaxCount = 7,
            RecentRecordInfoList =
            [
                new RecentRecordInfo("legacy", "Legacy", "C:/legacy.nyagekiProj")
            ]
        });
        settings.Set(new RecentRecordDataStoreSetting
        {
            RecordInfoDataMap = { ["legacy-key"] = new byte[] { 9 } }
        });

        var manager = new DefaultEditorRecentFilesManager(settings);

        Assert.Empty(manager.RecentRecordInfos);
        var aggregate = settings.Get<RecentRecordInfoStoreSetting>();
        Assert.Equal(RecentRecordInfoStoreSetting.CurrentVersion, aggregate.Version);
        Assert.Equal(7, aggregate.RecordMaxCount);
        Assert.Empty(settings.Get<RecentRecordDataStoreSetting>().RecordInfoDataMap);
    }

    [Fact]
    public void PostRecent_WhenPersistenceFails_LeavesCurrentSnapshotUnchanged()
    {
        var settings = new RecordingSettingManager();
        var manager = new DefaultEditorRecentFilesManager(settings);
        settings.ThrowOnNextSave = true;

        Assert.Throws<IOException>(() =>
            manager.PostRecent(FileType, "Project", "project", new byte[] { 1 }));
        Assert.Empty(manager.RecentRecordInfos);
    }

    [Fact]
    public async Task ValidityCoordinator_SameGeneration_CoalescesChecksAndRefreshesOnDemand()
    {
        var coordinator = new RecentRecordValidityCoordinator();
        var record = new RecentRecordInfo("test", "Project", "project", RecordId: Guid.NewGuid());
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;

        Task<bool> Check()
        {
            callCount++;
            return completion.Task;
        }

        var first = coordinator.GetOrCheckAsync(record, Check);
        var second = coordinator.GetOrCheckAsync(record, Check);
        Assert.Equal(1, callCount);
        completion.SetResult(true);
        Assert.True(await first);
        Assert.True(await second);

        coordinator.BeginValidationGeneration();
        Assert.True(await coordinator.GetOrCheckAsync(record, () =>
        {
            callCount++;
            return Task.FromResult(true);
        }));
        Assert.Equal(2, callCount);

        Assert.False(await coordinator.CheckFreshAsync(record, () =>
        {
            callCount++;
            return Task.FromResult(false);
        }));
        Assert.False(await coordinator.GetOrCheckAsync(record, () =>
        {
            callCount++;
            return Task.FromResult(true);
        }));
        Assert.Equal(3, callCount);
    }

    private sealed class RecordingSettingManager : ISettingManager
    {
        private readonly Dictionary<Type, object> settings = new();
        private readonly Dictionary<Type, int> saveCounts = new();

        public bool ThrowOnNextSave { get; set; }

        public void Set<T>(T value) where T : class
        {
            settings[typeof(T)] = value;
        }

        public T Get<T>() where T : class
        {
            return (T)settings[typeof(T)];
        }

        public int GetSaveCount<T>()
        {
            return saveCounts.GetValueOrDefault(typeof(T));
        }

        public void ResetSaveCounts()
        {
            saveCounts.Clear();
        }

        public void SaveSetting<T>(T obj, JsonTypeInfo<T> jsonTypeInfo)
        {
            if (ThrowOnNextSave)
            {
                ThrowOnNextSave = false;
                throw new IOException("persistence failed");
            }

            settings[typeof(T)] = obj!;
            saveCounts[typeof(T)] = saveCounts.GetValueOrDefault(typeof(T)) + 1;
        }

        public T GetSetting<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            JsonTypeInfo<T> jsonTypeInfo) where T : new()
        {
            if (settings.TryGetValue(typeof(T), out var value))
                return (T)value;

            var created = new T();
            settings[typeof(T)] = created;
            return created;
        }
    }

    private sealed class RecordingValidityCoordinator : IRecentRecordValidityCoordinator
    {
        public int BeginGenerationCallCount { get; private set; }

        public long BeginValidationGeneration() => ++BeginGenerationCallCount;

        public Task<bool> GetOrCheckAsync(
            RecentRecordInfo recordInfo,
            Func<Task<bool>> checkFactory) => checkFactory();

        public Task<bool> CheckFreshAsync(
            RecentRecordInfo recordInfo,
            Func<Task<bool>> checkFactory) => checkFactory();

        public void Invalidate(Guid recordId)
        {
        }
    }
}
