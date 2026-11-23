using System;
using System.Collections.Generic;
using System.Linq;
using BlackIceGPUTemp.Monitoring.Abstractions;

namespace BlackIceGPUTemp.Monitoring
{
    public class GpuProviderRegistry
    {
        private readonly IReadOnlyList<IGpuDeviceEnumerator> _providers;
        private readonly Dictionary<string, IGpuDeviceEnumerator> _providersById;

        public GpuProviderRegistry(IEnumerable<IGpuDeviceEnumerator> providers)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var list = new List<IGpuDeviceEnumerator>();
            var dictionary = new Dictionary<string, IGpuDeviceEnumerator>(StringComparer.OrdinalIgnoreCase);

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

        public IReadOnlyList<IGpuDeviceEnumerator> Providers => _providers;

        public IGpuDeviceEnumerator? FindById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Provider ID cannot be null, empty, or consist entirely of white-space characters.", nameof(id));
            }

            return _providersById.TryGetValue(id, out var provider) ? provider : null;
        }
    }
}