using System;

namespace TinyTemp.Monitoring.Providers.Amd.Native;

/// <summary>
/// Defines the native ADLX (AMD Device Library) interface contracts for GPU monitoring.
/// </summary>
public interface IAdlxNative
{
    /// <summary>
    /// Initializes the ADLX system.
    /// </summary>
    /// <param name="system">Receives the unmanaged ADLX system interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int Initialize(out IntPtr system);

    /// <summary>
    /// Terminates the ADLX system.
    /// </summary>
    /// <returns>The native ADLX result code.</returns>
    int Terminate();

    /// <summary>
    /// Retrieves the list of GPUs from the ADLX system.
    /// </summary>
    /// <param name="system">The unmanaged ADLX system interface pointer.</param>
    /// <param name="gpuList">Receives the unmanaged GPU list interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuList(IntPtr system, out IntPtr gpuList);

    /// <summary>
    /// Retrieves the number of GPUs in the GPU list.
    /// </summary>
    /// <param name="gpuList">The unmanaged GPU list interface pointer.</param>
    /// <param name="count">Receives the count of GPUs.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuListSize(IntPtr gpuList, out int count);

    /// <summary>
    /// Retrieves a GPU interface at the specified index from the GPU list.
    /// </summary>
    /// <param name="gpuList">The unmanaged GPU list interface pointer.</param>
    /// <param name="index">The zero-based index of the GPU.</param>
    /// <param name="gpu">Receives the unmanaged GPU interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuAt(IntPtr gpuList, int index, out IntPtr gpu);

    /// <summary>
    /// Retrieves the name of the specified GPU.
    /// </summary>
    /// <param name="gpu">The unmanaged GPU interface pointer.</param>
    /// <param name="name">Receives the GPU name, or null if unavailable.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuName(IntPtr gpu, out string? name);

    /// <summary>
    /// Retrieves the unique identifier of the specified GPU.
    /// </summary>
    /// <param name="gpu">The unmanaged GPU interface pointer.</param>
    /// <param name="uniqueId">Receives the unique identifier of the GPU.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuUniqueId(IntPtr gpu, out int uniqueId);

    /// <summary>
    /// Retrieves the performance monitoring services interface.
    /// </summary>
    /// <param name="system">The unmanaged ADLX system interface pointer.</param>
    /// <param name="services">Receives the unmanaged performance monitoring services interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetPerformanceMonitoringServices(IntPtr system, out IntPtr services);

    /// <summary>
    /// Retrieves the current GPU metrics for the specified GPU.
    /// </summary>
    /// <param name="services">The unmanaged performance monitoring services interface pointer.</param>
    /// <param name="gpu">The unmanaged GPU interface pointer.</param>
    /// <param name="metrics">Receives the unmanaged GPU metrics interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetCurrentGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metrics);

    /// <summary>
    /// Retrieves the supported GPU metrics interface for the specified GPU.
    /// </summary>
    /// <param name="services">The unmanaged performance monitoring services interface pointer.</param>
    /// <param name="gpu">The unmanaged GPU interface pointer.</param>
    /// <param name="metricsSupport">Receives the unmanaged GPU metrics support interface pointer.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetSupportedGpuMetrics(IntPtr services, IntPtr gpu, out IntPtr metricsSupport);

    /// <summary>
    /// Checks whether GPU temperature monitoring is supported.
    /// </summary>
    /// <param name="metricsSupport">The unmanaged GPU metrics support interface pointer.</param>
    /// <param name="supported">Receives a value indicating whether temperature monitoring is supported.</param>
    /// <returns>The native ADLX result code.</returns>
    int IsSupportedGpuTemperature(IntPtr metricsSupport, out bool supported);

    /// <summary>
    /// Retrieves the current GPU temperature in degrees Celsius.
    /// </summary>
    /// <param name="metrics">The unmanaged GPU metrics interface pointer.</param>
    /// <param name="celsius">Receives the temperature value in degrees Celsius.</param>
    /// <returns>The native ADLX result code.</returns>
    int GetGpuTemperature(IntPtr metrics, out double celsius);

    /// <summary>
    /// Releases an unmanaged ADLX interface.
    /// </summary>
    /// <param name="adlxInterface">The unmanaged ADLX interface pointer to release.</param>
    /// <returns>The native ADLX result code.</returns>
    int Release(IntPtr adlxInterface);
}
