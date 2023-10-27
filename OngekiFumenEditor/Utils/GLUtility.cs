using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Utils
{
    internal static class GLUtility
    {
        [Conditional("DEBUG")]
        public static void CheckError(string desc = default, [CallerFilePath] string callerFilePath = default, [CallerLineNumber] int callerLineNumber = default, [CallerMemberName] string callerMemberName = default)
        {
            /*
            var error = GL.GetError();
            if (error != ErrorCode.NoError)
                throw new Exception($"GL.GetError() returned {error} (desc:{desc}) in method {callerMemberName}(...) at {callerFilePath}:{callerLineNumber}");
        */
        }
    }
}
