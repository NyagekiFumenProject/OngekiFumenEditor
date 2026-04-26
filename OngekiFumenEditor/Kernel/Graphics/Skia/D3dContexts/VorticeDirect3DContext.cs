using OngekiFumenEditor.Utils;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Threading;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace OngekiFumenEditor.Kernel.Graphics.Skia.D3dContexts
{
    public class VorticeDirect3DContext : IDisposable
    {
        private readonly IDXGIFactory4 _factory;
        private readonly ID3D12Device2 _device;
        private readonly IDXGIAdapter1 _adapter;
        private readonly ID3D12CommandQueue _queue;
        private bool _disposed;

        public VorticeDirect3DContext()
        {
            if (!D3D12.IsSupported(FeatureLevel.Level_11_1))
                throw new NotSupportedException("Current platform doesn't support Direct3D 12.");

            var factory = DXGI.CreateDXGIFactory2<IDXGIFactory4>(true);

            ID3D12Device2 device = default;
            IDXGIAdapter1 adapter = default;
            using (var factory6 = factory.QueryInterfaceOrNull<IDXGIFactory6>())
            {
                if (factory6 is not null)
                {
                    for (var i = 0u; factory6.EnumAdapterByGpuPreference(i, GpuPreference.HighPerformance, out adapter).Success; i++)
                    {
                        if (D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out device).Success)
                            break;
                    }
                }
                else
                {
                    for (var i = 0u; factory.EnumAdapters1(i, out adapter).Success; i++)
                    {
                        if (D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out device).Success)
                            break;
                    }
                }
            }

            _factory = factory;
            _device = device ?? throw new NotSupportedException("Current platform unable to create Direct3D12 device.");
            _queue = _device.CreateCommandQueue(new CommandQueueDescription
            {
                Flags = CommandQueueFlags.None,
                Type = CommandListType.Direct
            });

            _adapter = adapter;
        }

        public IDXGIFactory4 Factory => _factory;

        public ID3D12Device2 Device => _device;

        public IDXGIAdapter1 Adapter => _adapter;

        public ID3D12CommandQueue Queue => _queue;

        public GRD3DBackendContext CreateBackendContext() =>
            new GRVorticeD3DBackendContext
            {
                Adapter = Adapter,
                Device = Device,
                Queue = Queue,
            };


        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _queue.Dispose();
            _device.Dispose();
            _adapter.Dispose();
            _factory.Dispose();
        }
    }
}
