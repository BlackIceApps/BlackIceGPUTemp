using System;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    public sealed class AmdAdlxDevice : IGpuDevice
    {
        private readonly AmdAdlxProvider _provider;
        private readonly int _uniqueId;

        public string Id { get; }
        public string Name { get; }
        public GpuVendor Vendor => GpuVendor.Amd;
        public GpuKind Kind => GpuKind.Unknown;

        internal AmdAdlxDevice(AmdAdlxProvider provider, string id, string name, int uniqueId)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _uniqueId = uniqueId;
        }

        public bool TryReadTemperature(out TemperatureReading? reading, out string? error)
        {
            return _provider.TryReadTemperatureInternal(_uniqueId, out reading, out error);
        }
    }
}
