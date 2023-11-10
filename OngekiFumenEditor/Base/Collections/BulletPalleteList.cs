using OngekiFumenEditor.Base.OngekiObjects;
using OngekiFumenEditor.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace OngekiFumenEditor.Base.Collections
{
	public class BulletPalleteList : IReadOnlyList<BulletPallete>, INotifyCollectionChanged
	{
		private static readonly Dictionary<char, int> ALPHABET = Enumerable.Empty<char>()
			.Concat(Enumerable.Range(0, 10).Select(x => x + '0').Select(x => (char)x))
			.Concat(Enumerable.Range(0, 26).Select(x => x + 'A').Select(x => (char)x))
			.Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);

		private static readonly Dictionary<int, char> ALPHABET_REV = ALPHABET.ToDictionary(x => x.Value, x => x.Key);

		public static int ConvertIdToInt(string id)
		{
			return id
				.ToUpperInvariant()
				.Reverse()
				.Select((x, i) => (int)Math.Pow(ALPHABET.Count, i) * (ALPHABET.TryGetValue(x, out var d) ? d : 0))
				.Sum();
		}

		public static string ConvertIntToId(int val)
		{
			var str = "";

			while (val != 0)
			{
				str = ALPHABET_REV[val % ALPHABET_REV.Count] + str;
				val = val / ALPHABET_REV.Count;
			}

			return str.ToUpperInvariant();
		}

		private Dictionary<int, BulletPallete> palleteMap = new();
		private string cacheCurrentMaxId = null;

		public int Count => palleteMap.Count;
		public BulletPallete this[int index] => this[index];
		public BulletPallete this[string strId] => palleteMap.TryGetValue(ConvertIdToInt(strId), out var r) ? r : default;

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public IEnumerator<BulletPallete> GetEnumerator() => palleteMap.Values.OrderBy(x => ConvertIdToInt(x.StrID)).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public void AddPallete(BulletPallete pallete)
		{
			if (cacheCurrentMaxId is null)
			{
				if (palleteMap.Count == 0)
					cacheCurrentMaxId = "9Z";
				else
					cacheCurrentMaxId = ConvertIntToId(palleteMap.Keys.OrderBy(x => x).LastOrDefault());
			}

			if (string.IsNullOrWhiteSpace(pallete.StrID))
			{
				//分配一个新的StrId 
				pallete.StrID = ConvertIntToId(ConvertIdToInt(cacheCurrentMaxId) + 1);
			}

			var addable = true;
			if (palleteMap.TryGetValue(ConvertIdToInt(pallete.StrID), out var old))
			{
				if (old == pallete)
					addable = false; //重复添加，那就忽略了
				else
				{
					Log.LogWarn($"remove old ({old}) and add new ({pallete}).");
					RemovePallete(old); //存在旧的，那就先删了旧的再添加新的
				}
			}

			if (addable)
			{
				palleteMap[ConvertIdToInt(pallete.StrID)] = pallete;

				pallete.PropertyChanged += OnPalletePropChanged;
				cacheCurrentMaxId = Comparer<string>.Default.Compare(pallete.StrID, cacheCurrentMaxId) > 0 ? pallete.StrID : cacheCurrentMaxId;

				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pallete));
			}
		}

		public void RemovePallete(BulletPallete pallete)
		{
			if (palleteMap.Remove(ConvertIdToInt(pallete.StrID)))
			{
				pallete.PropertyChanged -= OnPalletePropChanged;
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		private void OnPalletePropChanged(object sender, PropertyChangedEventArgs e)
		{

		}
	}
}
