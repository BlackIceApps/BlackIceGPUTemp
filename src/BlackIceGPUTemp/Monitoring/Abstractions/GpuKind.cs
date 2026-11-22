namespace BlackIceGPUTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Specifies the form factor or kind of a GPU device.
    /// </summary>
    public enum GpuKind
    {
        /// <summary>
        /// Unknown or unspecified GPU kind.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Integrated GPU (typically on-die with the CPU).
        /// </summary>
        Integrated = 1,

        /// <summary>
        /// Discrete GPU (dedicated graphics card).
        /// </summary>
        Discrete = 2
    }
}
