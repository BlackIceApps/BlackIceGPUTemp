using System;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring.Providers.Amd
{
    public sealed class AmdAdlxDevice : IGpuDevice
    {
        private readonly IGpuTemperatureReader _temperatureReader;

        public string Id { get; }
        public string Name { get; }
        public GpuVendor Vendor => GpuVendor.Amd;
        public GpuKind Kind { get; }

        internal AmdAdlxDevice(IGpuTemperatureReader temperatureReader, string id, string name, GpuKind kind)
        {
            _temperatureReader = temperatureReader ?? throw new ArgumentNullException(nameof(temperatureReader));
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
        }

        public bool TryReadTemperature(out TemperatureReading? reading, out string? error, CancellationToken cancellationToken = default)
        {
            return _temperatureReader.TryReadTemperature(Id, out reading, out error, cancellationToken);
        }
    }
}