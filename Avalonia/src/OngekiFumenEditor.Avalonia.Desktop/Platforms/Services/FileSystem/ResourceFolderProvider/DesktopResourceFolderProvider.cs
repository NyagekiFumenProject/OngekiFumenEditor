using Injectio.Attributes;
using OngekiFumenEditor.Avalonia.Platforms.Services.ResourceFolderProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Avalonia.Desktop.Platforms.Services.FileSystem.ResourceFolderProvider
{
    [RegisterSingleton<IResourceFolderProvider>]
    public class DesktopResourceFolderProvider : IResourceFolderProvider
    {
        public Task<Stream> OpenRead(string filePath)
        {
            return Task.FromResult<Stream>(File.OpenRead(Path.Combine("Resources", filePath)));
        }
    }
}
