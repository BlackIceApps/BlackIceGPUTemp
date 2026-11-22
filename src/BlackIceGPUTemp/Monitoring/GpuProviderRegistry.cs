using System;
using System.Collections.Generic;
using System.Linq;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring
{
    /// <summary>
    /// Represents a registry that stores and manages GPU providers.
    /// </summary>
    public class GpuProviderRegistry
    {
        private readonly IReadOnlyList<IGpuProvider> _providers;
        private readonly Dictionary<string, IGpuProvider> _providersById;

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuProviderRegistry"/> class.
        /// </summary>
        /// <param name="providers">The collection of GPU providers to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="providers"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when any provider in the collection is null, has a null, empty, or whitespace ID,
        /// or when duplicate IDs (case-insensitive) are encountered.
        /// </exception>
        public GpuProviderRegistry(IEnumerable<IGpuProvider> providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var list = new List<IGpuProvider>();
            var dictionary = new Dictionary<string, IGpuProvider>(StringComparer.OrdinalIgnoreCase);

            foreach (var provider in providers)
            {
                if (provider == null)
                {
                    throw new ArgumentException("Provider collection cannot contain null elements.", nameof(providers));
                }

                if (string.IsNullOrWhiteSpace(provider.Id))
                {
                    throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(providers));
                }

                if (dictionary.ContainsKey(provider.Id))
                {
                    throw new ArgumentException($"Duplicate provider ID encountered: '{provider.Id}'.", nameof(providers));
                }

                list.Add(provider);
                dictionary.Add(provider.Id, provider);
            }

            _providers = list.AsReadOnly();
            _providersById = dictionary;
        }

        /// <summary>
        /// Gets the read-only list of registered GPU providers in the order they were provided.
        /// </summary>
        public IReadOnlyList<IGpuProvider> Providers => _providers;

        /// <summary>
        /// Finds a registered GPU provider by its unique identifier, case-insensitively.
        /// </summary>
        /// <param name="id">The unique identifier of the provider to find.</param>
        /// <returns>The matching <see cref="IGpuProvider"/> if found; otherwise, <c>null</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null, empty, or white-space.</exception>
        public IGpuProvider? FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(id));
            }

            return _providersById.TryGetValue(id, out var provider) ? provider : null;
        }
    }
}
