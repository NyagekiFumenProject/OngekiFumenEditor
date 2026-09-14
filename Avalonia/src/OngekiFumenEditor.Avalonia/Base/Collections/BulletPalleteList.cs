using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using OngekiFumenEditor.Avalonia.Base.OngekiObjects;

namespace OngekiFumenEditor.Avalonia.Base.Collections
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

		private readonly Dictionary<int, BulletPallete> palleteMap = new();

		/// <summary>
		/// 与 <see cref="GetEnumerator"/> 暴露顺序一致的 backing 列表：按 <see cref="ConvertIdToInt"/> 升序。
		/// 索引器与枚举都走它，使 <see cref="Count"/><c>this[int]</c>/枚举三者互相自洽（真正的
		/// <see cref="IReadOnlyList{T}"/> 语义），并免掉原先每次枚举都 <c>OrderBy</c> 一遍的排序与分配。
		/// </summary>
		private readonly List<BulletPallete> orderedPalletes = new();

		private string cacheCurrentMaxId = null;

		public int Count => orderedPalletes.Count;
		public BulletPallete this[int index] => orderedPalletes[index];
		public BulletPallete this[string strId] => palleteMap.TryGetValue(ConvertIdToInt(strId), out var r) ? r : default;

		public event NotifyCollectionChangedEventHandler CollectionChanged;

		public IEnumerator<BulletPallete> GetEnumerator() => orderedPalletes.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>
		/// 按数值 id 在 backing 列表里定位插入点。顺序与旧实现
		/// <c>palleteMap.Values.OrderBy(x =&gt; ConvertIdToInt(x.StrID))</c> 完全一致
		/// （注意是按 <see cref="ConvertIdToInt"/> 的数值序，不是字符串序）。
		/// </summary>
		private int FindOrderedIndex(int id)
		{
			var lo = 0;
			var hi = orderedPalletes.Count;
			while (lo < hi)
			{
				var mid = lo + ((hi - lo) >> 1);
				if (ConvertIdToInt(orderedPalletes[mid].StrID) < id)
					lo = mid + 1;
				else
					hi = mid;
			}
			return lo;
		}

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
				var id = ConvertIdToInt(pallete.StrID);
				palleteMap[id] = pallete;
				orderedPalletes.Insert(FindOrderedIndex(id), pallete);

				pallete.PropertyChanged += OnPalletePropChanged;
				cacheCurrentMaxId = Comparer<string>.Default.Compare(pallete.StrID, cacheCurrentMaxId) > 0 ? pallete.StrID : cacheCurrentMaxId;

				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pallete));
			}
		}

		public void RemovePallete(BulletPallete pallete)
		{
			if (palleteMap.Remove(ConvertIdToInt(pallete.StrID), out var removed))
			{
				// 退订与移除都用真正入表的那一个实例（AddPallete 订阅的就是它），
				// 保证订阅与退订严格互逆；调用方传入同 id 的另一个实例时也不会漏退订。
				orderedPalletes.Remove(removed);
				removed.PropertyChanged -= OnPalletePropChanged;
				CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
		}

		private void OnPalletePropChanged(object sender, PropertyChangedEventArgs e)
		{

		}
	}
}

