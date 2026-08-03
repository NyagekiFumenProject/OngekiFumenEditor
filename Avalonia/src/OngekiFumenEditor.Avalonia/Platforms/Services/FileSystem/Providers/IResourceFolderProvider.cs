using System;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Avalonia.Platforms.Services.ResourceFolderProvider
{
    public interface IResourceFolderProvider
    {
        Task<Stream> OpenRead(string filePath);
    }
}
