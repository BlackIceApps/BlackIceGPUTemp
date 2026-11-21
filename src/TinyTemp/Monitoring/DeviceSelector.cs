using System;
using System.Linq;
using TinyTemp.Monitoring.Abstractions;

namespace TinyTemp.Monitoring
{
    /// <summary>
    /// Represents a selector that finds a specific GPU device across registered providers.
    /// </summary>
    public class DeviceSelector
    {
        private readonly GpuProviderRegistry _registry;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceSelector"/> class.
        /// </summary>
        /// <param name="registry">The GPU provider registry to use for finding providers.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public DeviceSelector(GpuProviderRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// Finds a specific GPU device by provider identifier and device identifier.
        /// </summary>
        /// <param name="providerId">The unique identifier of the provider.</param>
        /// <param name="deviceId">The unique identifier of the device.</param>
        /// <returns>The matching <see cref="IGpuDevice"/> if found; otherwise, <c>null</c>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="providerId"/> or <paramref name="deviceId"/> is null, empty, or white-space.
        /// </exception>
        public IGpuDevice? FindDevice(string providerId, string deviceId)
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

            var devices = provider.EnumerateDevices();
            if (devices == null)
            {
                return null;
            }

            return devices.FirstOrDefault(d => d != null && string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
