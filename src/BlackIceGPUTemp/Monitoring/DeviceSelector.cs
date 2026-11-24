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

        /// <summary>
        /// Finds a GPU device by its provider ID and device ID.
        /// </summary>
        /// <param name="providerId">The provider in which to search for the device.</param>
        /// <param name="deviceId">The identifier of the device to find.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the operation to complete.</param>
        /// <returns>
        /// The found <see cref="IGpuDevice"/> if the provider exists, enumeration succeeds, and the device is found;
        /// otherwise, <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="providerId"/> or <paramref name="deviceId"/> is null, empty, or consists only of white-space characters.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when the operation is cancelled via <paramref name="cancellationToken"/> before or during device enumeration.
        /// Provider lookup returns <see langword="null"/> without throwing if the provider is not found.
        /// </exception>
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