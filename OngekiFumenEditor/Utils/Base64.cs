using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    public static class Base64
    {
        public static string Decode(string base64Content, Encoding encoding) => encoding.GetString(Convert.FromBase64String(base64Content));
        public static string Decode(string base64Content) => Decode(base64Content, Encoding.UTF8);

        public static string Encode(string rawContent, Encoding encoding) => Convert.ToBase64String(encoding.GetBytes(rawContent));
        public static string Encode(string rawContent) => Encode(rawContent, Encoding.UTF8);
    }
}
