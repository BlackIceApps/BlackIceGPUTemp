namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Specifies the vendor of a GPU device.
    /// </summary>
    public enum GpuVendor
    {
        /// <summary>
        /// Unknown or unspecified vendor.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Advanced Micro Devices (AMD).
        /// </summary>
        Amd = 1,

        /// <summary>
        /// NVIDIA Corporation.
        /// </summary>
        Nvidia = 2,

        /// <summary>
        /// Intel Corporation.
        /// </summary>
        Intel = 3
    }
}
