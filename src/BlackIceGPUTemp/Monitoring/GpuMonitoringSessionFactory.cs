using System;
using System.Collections.Generic;
using System.Threading;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring
{
    /// <summary>
    /// Default implementation of <see cref="IGpuMonitoringSessionFactory"/>.
    /// </summary>
    public sealed class GpuMonitoringSessionFactory : IGpuMonitoringSessionFactory
    {
        private readonly IGpuProviderFactory _providerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuMonitoringSessionFactory"/> class.
        /// </summary>
        /// <param name="providerFactory">The factory for creating GPU providers.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="providerFactory"/> is null.</exception>
        public GpuMonitoringSessionFactory(IGpuProviderFactory providerFactory)
        {
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        }

        /// <inheritdoc />
        public GpuMonitoringInitializationResult Create(CancellationToken cancellationToken)
        {
            var providers = _providerFactory.CreateProviders();
            if (providers == null || providers.Count == 0)
            {
                return new GpuMonitoringInitializationResult("No GPU providers available.");
            }

            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var probeResult = ((IGpuProviderProbe)provider).Probe(cancellationToken);

                if (probeResult.IsAvailable)
                {
                    var devices = ((IGpuDeviceEnumerator)provider).EnumerateDevices(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (devices != null && devices.Count > 0)
                    {
                        var firstDevice = devices[0];
                        var registry = new GpuProviderRegistry(providers);
                        var selector = new DeviceSelector(registry);
                        var monitorService = new HardwareMonitorService(selector);

                        return new GpuMonitoringInitializationResult(
                            monitorService,
                            provider.Id,
                            firstDevice.Id,
                            firstDevice.Name);
                    }
                }
            }

            return new GpuMonitoringInitializationResult("No supported GPU devices found.");
        }
    }
}