using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OngekiFumenEditor.Parser.DefaultImpl
{
    public class LoggableBinaryReader : IDisposable
    {
        private readonly BinaryReader core;

        public LoggableBinaryReader(BinaryReader core) => this.core = core;

        public void Dispose() => core.Dispose();

        private void Log(object value) => Utils.Log.LogDebug($"LoggableBinaryReader.Read()\t{value.GetType().Name}\t{value}");

        public int ReadInt32()
        {
            var v = core.ReadInt32();
            Log(v);
            return v;
        }

        public string ReadString()
        {
            var v = core.ReadString();
            Log(v);
            return v;
        }

        public double ReadDouble()
        {
            var v = core.ReadDouble();
            Log(v);
            return v;
        }

        public float ReadSingle()
        {
            var v = core.ReadSingle();
            Log(v);
            return v;
        }

        public bool ReadBoolean()
        {
            var v = core.ReadBoolean();
            Log(v);
            return v;
        }
    }
}
