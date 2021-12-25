using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils.ObjectPool
{
    public interface ICacheCleanable
    {
        //在对象池提取之前调用清理
        void OnBeforeGetClean();
        //在放入对象池之后调用清理
        void OnAfterPutClean();
    }
}
