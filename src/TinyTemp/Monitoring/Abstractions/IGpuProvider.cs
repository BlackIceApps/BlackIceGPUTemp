using System.Collections.Generic;

namespace TinyTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Defines a provider that can discover and initialize GPU devices.
    /// </summary>
    public interface IGpuProvider
    {
        /// <summary>
        /// Gets the unique identifier of the provider.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the user-friendly display name of the provider.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Checks whether the provider is supported and available on the current system.
        /// </summary>
        /// <returns>A <see cref="ProviderProbeResult"/> containing availability and diagnostic details.</returns>
        ProviderProbeResult Probe();

        /// <summary>
        /// Enumerates all GPU devices detected by this provider.
        /// </summary>
        /// <returns>A read-only list of detected <see cref="IGpuDevice"/> instances.</returns>
        IReadOnlyList<IGpuDevice> EnumerateDevices();
    }
}
