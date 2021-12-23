using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class LinqExtensionMethod
    {
        /// <summary>
        /// 在有序的集合上，通过条件去确定前后两个元素
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sortedList"></param>
        /// <param name="predicator">条件判断，bool predicator(T previous,T next)</param>
        /// <returns></returns>
        public static T[] FindBetweenByPredicator<T>(this IEnumerable<T> sortedList,Func<T,T,bool> predicator,bool enableFirstLastEmpty = false)
        {
            //todo
            return default;
        }
    }
}
