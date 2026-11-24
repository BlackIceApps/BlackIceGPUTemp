using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring
{
    public class DeviceSelector
    {
        private readonly GpuProviderRegistry _registry;

        public DeviceSelector(GpuProviderRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public IGpuDevice? FindDevice(string providerId, string deviceId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerId))
            {
                throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(providerId));
            }

            if (string.IsNullOrWhiteSpace(deviceId))
            {
                throw new ArgumentException("Device ID cannot be null, empty, or consist entirely of white-space characters.", nameof(deviceId));
            }

            var provider = _registry.FindById(providerId);
            if (provider == null)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            bool success = provider.TryEnumerateDevices(out IReadOnlyList<IGpuDevice>? devices, out _, cancellationToken);
            if (!success || devices == null)
            {
                return null;
            }

            return devices.FirstOrDefault(d => d != null && string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        }
    }
}