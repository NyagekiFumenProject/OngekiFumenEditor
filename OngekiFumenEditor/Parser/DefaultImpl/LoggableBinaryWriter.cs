using OngekiFumenEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    public class LoggableBinaryWriter : IDisposable
    {
        private readonly BinaryWriter core;

        public LoggableBinaryWriter(BinaryWriter core) => this.core = core;

        public void Dispose() => core?.Dispose();

        public void Flush() => core.Flush();

        private void Log(object value) => Utils.Log.LogDebug($"LoggableBinaryWriter.Write()\t{value.GetType().Name}\t{value}");

        public void Write(int value)
        {
            core.Write(value);
            Log(value);
        }

        public void Write(float value)
        {
            core.Write(value);
            Log(value);
        }

        public void Write(string value)
        {
            core.Write(value);
            Log(value);
        }

        public void Write(double value)
        {
            core.Write(value);
            Log(value);
        }

        public void Write(bool value)
        {
            core.Write(value);
            Log(value);
        }
    }
}
