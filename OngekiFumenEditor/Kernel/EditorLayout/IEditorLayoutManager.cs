using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.EditorLayout
{
    public interface IEditorLayoutManager
    {
        Task<bool> SaveLayout(Stream outputLayoutDataStream);
        Task<bool> LoadLayout(Stream intputLayoutDataStream);

        //Task CheckAndNotifyUserUseSuggestLayout();
    }
}
