using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OngekiFumenEditor.Kernel.RecentFiles.DefaultImp
{
	[Export(typeof(IEditorRecentFilesManager))]
	[PartCreationPolicy(CreationPolicy.Shared)]
	internal class DefaultEditorRecentFilesManager : IEditorRecentFilesManager
	{
		public ObservableCollection<RecentRecordInfo> recentRecordInfos = new();
		public IEnumerable<RecentRecordInfo> RecentRecordInfos => recentRecordInfos;

		private const int MaxRecordCount = 10;
		private object locker = new();

		public DefaultEditorRecentFilesManager()
		{
			LoadRecordOpenedList();
		}

		public void SaveRecordOpenedList()
		{
			lock (locker)
			{
				var list = recentRecordInfos.Take(MaxRecordCount).ToList();
				var jsonStr = JsonSerializer.Serialize(list);
				var base64Str = Base64.Encode(jsonStr);

				Properties.EditorGlobalSetting.Default.RecentOpenedListStr = base64Str;
				Properties.EditorGlobalSetting.Default.Save();
			}
		}

		public void LoadRecordOpenedList()
		{
			lock (locker)
			{
				recentRecordInfos.Clear();

				var base64Str = Properties.EditorGlobalSetting.Default.RecentOpenedListStr;
				if (!string.IsNullOrWhiteSpace(base64Str))
				{
					var jsonStr = Base64.Decode(Properties.EditorGlobalSetting.Default.RecentOpenedListStr);
					var list = JsonSerializer.Deserialize<List<RecentRecordInfo>>(jsonStr);
					recentRecordInfos.AddRange(list.Take(MaxRecordCount));
				}
			}
		}

		public void PostRecord(RecentRecordInfo info)
		{
			var fileName = info.Type == RecentOpenType.OpenEmbeddedRecommendedScript
				? info.FileName
				: Path.GetFullPath(info.FileName);
			info = info with { FileName = fileName, LastAccessTime = DateTime.Now };

			if (info.FileName == recentRecordInfos.FirstOrDefault()?.FileName)
				return;
			recentRecordInfos.RemoveRange(recentRecordInfos.Where(x => x.FileName == info.FileName).ToArray());

			recentRecordInfos.Insert(0, info);
			SaveRecordOpenedList();
		}

		public bool CheckValid(RecentRecordInfo info)
		{
			if (info is null || string.IsNullOrWhiteSpace(info.FileName))
				return false;

			return info.Type switch
			{
				RecentOpenType.OpenEmbeddedRecommendedScript => typeof(DefaultEditorRecentFilesManager).Assembly.GetManifestResourceInfo(info.FileName) is not null,
				_ => File.Exists(info.FileName),
			};
		}

		public void ClearAllRecords()
		{
			recentRecordInfos.Clear();
			SaveRecordOpenedList();
		}
	}
}
