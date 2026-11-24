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

            bool hasAvailableProvider = false;

            foreach (var provider in providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Check if provider supports explicit lifecycle management
                var lifecycleProvider = provider as IGpuProviderLifecycle;

                if (lifecycleProvider != null)
                {
                    // Call Connect first - handle exceptions from Connect itself
                    try
                    {
                        if (!lifecycleProvider.Connect(out _))
                        {
                            // Connect failed - skip this provider, no Disconnect needed
                            continue;
                        }
                    }
                    catch
                    {
                        // Connect threw - no session established, no Disconnect needed
                        throw;
                    }

                    bool disconnectCalled = false;

                    try
                    {
                        // Check cancellation after successful Connect
                        cancellationToken.ThrowIfCancellationRequested();

                        var probeResult = ((IGpuProviderProbe)provider).Probe(cancellationToken);

                        if (probeResult.IsAvailable)
                        {
                            hasAvailableProvider = true;

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
                                    firstDevice.Name,
                                    lifecycleProvider);
                            }
else
                        {
                            // No devices - disconnect (normal cleanup, propagate Disconnect exception)
                            disconnectCalled = true;
                            lifecycleProvider.Disconnect();
                        }
                    }
                    else
                    {
                        // Probe failed - disconnect (normal cleanup, propagate Disconnect exception)
                        disconnectCalled = true;
                        lifecycleProvider.Disconnect();
                    }
                    }
                    catch
                    {
                        // Any exception (including OperationCanceledException) - disconnect if not already done
                        if (!disconnectCalled)
                        {
                            try
                            {
                                lifecycleProvider.Disconnect();
                            }
                            catch
                            {
                                // Suppress cleanup exceptions, rethrow original
                            }
                        }
                        throw;
                    }
                }
                else
                {
                    // Provider without lifecycle - old behavior
                    var probeResult = ((IGpuProviderProbe)provider).Probe(cancellationToken);

                    if (probeResult.IsAvailable)
                    {
                        hasAvailableProvider = true;

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
            }

            if (hasAvailableProvider)
            {
                return new GpuMonitoringInitializationResult("No supported GPU devices found.");
            }

            return new GpuMonitoringInitializationResult("No GPU providers are available.");
        }
    }
}