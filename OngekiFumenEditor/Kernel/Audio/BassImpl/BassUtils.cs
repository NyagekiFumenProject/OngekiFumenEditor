using ManagedBass;
using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Kernel.Audio.BassImpl
{
    internal static class BassUtils
    {
        public static void ReportError(string callMethod)
        {
            var state = Bass.LastError;
            if (state != Errors.OK)
            {
                var msg = $"Call {callMethod}(...) failed, error = {Bass.LastError}";
                Log.LogError(msg);

                throw new Exception(msg);
            }
        }
    }
}
