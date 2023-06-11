using OngekiFumenEditor.Base.OngekiObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Base.Collections
{
    public class BulletPalleteList : IEnumerable<BulletPallete>, INotifyCollectionChanged
    {
        private static readonly Dictionary<char, int> ALPHABET = Enumerable.Empty<char>()
            .Concat(Enumerable.Range(0, 10).Select(x => x + '0').Select(x => (char)x))
            .Concat(Enumerable.Range(0, 26).Select(x => x + 'A').Select(x => (char)x))
            .Select((x, i) => (x, i)).ToDictionary(x => x.x, x => x.i);

        private static readonly Dictionary<int, char> ALPHABET_REV = ALPHABET.ToDictionary(x => x.Value, x => x.Key);

        public static int ConvertIdToInt(string id)
        {
            return id.Reverse().Select((x, i) => (int)Math.Pow(ALPHABET.Count, i) * (ALPHABET.TryGetValue(x, out var d) ? d : 0)).Sum();
        }

        public static string ConvertIntToId(int val)
        {
            var str = "";

            while (val != 0)
            {
                str = ALPHABET_REV[val % ALPHABET_REV.Count] + str;
                val = val / ALPHABET_REV.Count;
            }

            return str;
        }

        private HashSet<BulletPallete> palletes = new();
        private string cacheCurrentMaxId = "9Z";

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public IEnumerator<BulletPallete> GetEnumerator() => palletes.Select(x => x).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void AddPallete(BulletPallete pallete)
        {
            if (string.IsNullOrWhiteSpace(pallete.StrID))
            {
                //generate new id
                pallete.StrID = ConvertIntToId(ConvertIdToInt(cacheCurrentMaxId) + 1);
            }

            if (palletes.Add(pallete))
            {
                pallete.PropertyChanged += OnPalletePropChanged;
                cacheCurrentMaxId = Comparer<string>.Default.Compare(pallete.StrID, cacheCurrentMaxId) > 0 ? pallete.StrID : cacheCurrentMaxId;

                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, pallete));
            }
        }

        public void RemovePallete(BulletPallete pallete)
        {
            if (palletes.Remove(pallete))
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
