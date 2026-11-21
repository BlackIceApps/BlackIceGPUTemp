namespace TinyTemp.Monitoring.Abstractions
{
    /// <summary>
    /// Represents an immutable result of checking a GPU provider's availability.
    /// </summary>
    public sealed class ProviderProbeResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderProbeResult"/> class.
        /// </summary>
        /// <param name="isAvailable">Indicates whether the provider is available for use.</param>
        /// <param name="message">An optional message providing details about the probe result.</param>
        public ProviderProbeResult(bool isAvailable, string? message)
        {
            IsAvailable = isAvailable;
            Message = message;
        }

        /// <summary>
        /// Gets a value indicating whether the provider is available.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Gets an optional detail or error message regarding availability.
        /// </summary>
        public string? Message { get; }
    }
}
