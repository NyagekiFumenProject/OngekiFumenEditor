using BenchmarkDotNet.Attributes;
using OngekiFumenEditor.Base;
using OngekiFumenEditor.Base.OngekiObjects.ConnectableObject;
using System;
using System.Collections.Generic;
using System.Text;

namespace OngekiFumenEditor.Benchmark.Infrastructure
{
    internal abstract class BenchmarkBase
    {
        [GlobalSetup]
        public virtual void GlobalSetup()
        {
            BenchmarkRuntime.EnsureInitialized();
        }
    }
}
