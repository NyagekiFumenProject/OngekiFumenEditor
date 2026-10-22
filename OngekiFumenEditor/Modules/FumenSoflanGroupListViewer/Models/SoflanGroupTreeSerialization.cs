using System;
using System.Linq;
using System.Text;

namespace OngekiFumenEditor.Modules.FumenSoflanGroupListViewer.Models
{
    /// <summary>旧版 [SGC]/[SGF] 行的路径/名称解码（读取迁移用）。</summary>
    public static class SoflanGroupTreeSerialization
    {
        public const string RootPath = "-";

        public static string[] DecodePath(string path)
            => string.IsNullOrEmpty(path) || path == RootPath
                ? Array.Empty<string>()
                : path.Split('/').Select(Uri.UnescapeDataString).ToArray();

        public static string DecodeName(string encoded)
            => string.IsNullOrEmpty(encoded) || encoded == RootPath
                ? string.Empty
                : Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
    }
}
