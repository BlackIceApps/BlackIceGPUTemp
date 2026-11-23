namespace BlackIceGPUTemp.Monitoring.Abstractions
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
        /// <param name="message">
        /// An optional message providing details about the probe result.
        /// Must not be <see langword="null"/> when <paramref name="isAvailable"/> is <see langword="false"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="isAvailable"/> is <see langword="false"/> and <paramref name="message"/> is <see langword="null"/>.
        /// </exception>
        public ProviderProbeResult(bool isAvailable, string? message)
        {
            if (!isAvailable && message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            IsAvailable = isAvailable;
            Message = message;
        }

        /// <summary>
        /// Gets a value indicating whether the provider is available.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// Gets an optional detail or error message regarding availability.
        /// This value is never <see langword="null"/> when <see cref="IsAvailable"/> is <see langword="false"/>.
        /// </summary>
        public string? Message { get; }
    }
}
