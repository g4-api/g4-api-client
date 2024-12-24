using G4.Api.Abstractions;
using G4.Attributes;
using G4.Attributes.Abstraction;
using G4.Cache;
using G4.Extensions;
using G4.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace G4.Api.Clients
{
    /// <summary>
    /// Serves as a bridge between the G4 engine and external clients by managing the retrieval and synchronization of metadata.
    /// It facilitates efficient access to plugin caches, documents, and manifests, ensuring that external integrations have up-to-date and consistent configuration data.
    /// By leveraging caching mechanisms, it optimizes performance and reduces redundant database operations, while providing robust methods to interact with various plugins and repositories.
    /// </summary>
    internal class IntegrationClient: ClientBase, IIntegrationClient
    {
        #region *** Fields       ***
        // Manages caching mechanisms for plugins.
        private readonly CacheManager _cache;

        // Represents the internal cache for non-cached plugins.
        // This cache is used to store plugins that are not present in the main cache.
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> _internalCache = new(StringComparer.OrdinalIgnoreCase);
        #endregion

        #region *** Properties   ***
        // Provides a thread-safe dictionary containing plugin cache data. 
        // If the dictionary is empty or does not contain the "Driver" key, 
        // it automatically populates the dictionary with driver-specific data before returning it.
        private static ConcurrentDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> InternalCache
        {
            get
            {
                // Check if the internal cache is empty or if it does not yet contain the "Driver" key.
                // If so, we populate the "Driver" entry by calling FindDrivers().
                if (_internalCache.IsEmpty || !_internalCache.ContainsKey("Driver"))
                {
                    _internalCache.TryAdd("Driver", FindDrivers());
                }

                // Return the populated or existing cache.
                return _internalCache;
            }
        }
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationClient"/> class.
        /// This parameterless constructor creates the client using the singleton instance of <see cref="CacheManager"/>.
        /// </summary>
        public IntegrationClient()
            : this(cache: CacheManager.Instance)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationClient"/> class using the specified <see cref="CacheManager"/>.
        /// Merges any pre-existing plugin data from the internal cache into the provided cache.
        /// </summary>
        /// <param name="cache">A <see cref="CacheManager"/> instance that manages plugin caching.</param>
        public IntegrationClient(CacheManager cache)
        {
            // Assign the provided cache to the internal cache field.
            _cache = cache;

            // Merge driver data or other plugin data from the internal cache into the user-provided cache.
            foreach (var item in InternalCache)
            {
                _cache.PluginsCache[item.Key] = item.Value;
            }
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> GetCache(params G4ExternalRepositoryModel[] repositories)
        {
            // Synchronize the cache with the provided external repositories.
            SyncCache(_cache, repositories);

            // Return the current state of the plugin cache.
            return _cache.PluginsCache;
        }

        /// <inheritdoc />
        public IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> GetCache()
        {
            return _cache.PluginsCache;
        }

        /// <inheritdoc />
        public string GetDocument(string key, G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the document using the provided key from the cache.
            var document = GetDocument(key);

            // Check if the retrieved document is not null or empty.
            if (!string.IsNullOrEmpty(document))
            {
                // If the document exists, return it immediately.
                return document;
            }

            // If the document was not found, synchronize the cache with the external repository.
            SyncCache(_cache, repository);

            // Attempt to retrieve the document again after cache synchronization.
            return GetDocument(key);
        }

        /// <inheritdoc />
        public string GetDocument(string pluginType, string key, G4ExternalRepositoryModel repository)
        {
            // Attempt to retrieve the document using the provided key from the cache.
            var document = GetDocument(pluginType, key);

            // Check if the retrieved document is not null or empty.
            if (!string.IsNullOrEmpty(document))
            {
                // If the document exists, return it immediately.
                return document;
            }

            // If the document was not found, synchronize the cache with the external repository.
            SyncCache(_cache, repository);

            // Attempt to retrieve the document again after cache synchronization.
            return GetDocument(key);
        }

        /// <inheritdoc />
        public string GetDocument(string key)
        {
            // Iterate through all plugin types available in the plugins cache.
            foreach (var pluginType in _cache.PluginsCache.Keys)
            {
                // Attempt to retrieve the cache model for the given key within the current plugin type.
                if (_cache.PluginsCache[pluginType].TryGetValue(key, out G4PluginCacheModel cacheModel))
                {
                    return cacheModel.Document;
                }
            }

            // Return an empty string if no document was found.
            return string.Empty;
        }

        /// <inheritdoc />
        public string GetDocument(string pluginType, string key)
        {
            // Attempt to retrieve the plugins dictionary for the specified plugin type.
            var isType = _cache.PluginsCache.TryGetValue(pluginType, out ConcurrentDictionary<string, G4PluginCacheModel> plugins);

            // If the plugin type exists, attempt to retrieve the cache model for the given key.
            var isPlugin = plugins.TryGetValue(key, out G4PluginCacheModel cacheModel) && isType;

            // Return the document if the plugin is found, otherwise return an empty string.
            return isPlugin ? cacheModel.Document : string.Empty;
        }

        /// <inheritdoc />
        public IG4PluginReference GetDriver(string key)
        {
            // Attempt to retrieve a dictionary of drivers from the internal cache.
            // Then check if there is a driver matching the provided key.
            if (InternalCache.TryGetValue("Driver", out var drivers) && drivers.TryGetValue(key, out var driver))
            {
                // Return the manifest of the matched driver plugin.
                return driver.Manifest;
            }

            // If no matching driver was found, return the default (null).
            return default;
        }

        /// <inheritdoc />
        public IEnumerable<IG4PluginReference> GetDrivers()
        {
            // Attempt to retrieve a dictionary of drivers from the internal cache.
            if (!InternalCache.TryGetValue("Driver", out ConcurrentDictionary<string, G4PluginCacheModel> value))
            {
                // If no driver dictionary exists, return an empty collection.
                return [];
            }

            // Project each cached driver into its manifest representation.
            return value.Values.Select(i => i.Manifest);
        }

        /// <inheritdoc />
        public T GetManifest<T>(string key, G4ExternalRepositoryModel repository) where T : IG4PluginReference
        {
            // Attempt to retrieve the manifest using the provided key from the cache.
            var manifest = GetManifest<T>(key);

            // If the manifest is found, return it immediately.
            if (manifest is not null)
            {
                return manifest;
            }

            // If the manifest was not found, synchronize the cache with the external repository.
            SyncCache(_cache, repository);

            // Attempt to retrieve the manifest again after cache synchronization.
            return GetManifest<T>(key);
        }

        /// <inheritdoc />
        public T GetManifest<T>(string pluginType, string key, G4ExternalRepositoryModel repository) where T : IG4PluginReference
        {
            // Attempt to retrieve the manifest using the provided plugin type and key from the cache.
            var manifest = GetManifest<T>(pluginType, key);

            // If the manifest is found, return it immediately.
            if (manifest is not null)
            {
                return manifest;
            }

            // If the manifest was not found, synchronize the cache with the external repository.
            SyncCache(_cache, repository);

            // Attempt to retrieve the manifest again after cache synchronization.
            return GetManifest<T>(pluginType, key);
        }

        /// <inheritdoc />
        public T GetManifest<T>(string key) where T : IG4PluginReference
        {
            // Iterate through all plugin types available in the plugins cache.
            foreach (var pluginType in _cache.PluginsCache.Keys)
            {
                // Attempt to retrieve the cache model for the given key within the current plugin type.
                if (_cache.PluginsCache[pluginType].TryGetValue(key, out G4PluginCacheModel cacheModel))
                {
                    // Return the manifest if it was found.
                    return (T)cacheModel.Manifest;
                }
            }

            // Return the default value (null) if no manifest was found for the provided key.
            return default;
        }

        /// <inheritdoc />
        public T GetManifest<T>(string pluginType, string key) where T : IG4PluginReference
        {
            // Attempt to retrieve the plugins dictionary for the specified plugin type.
            var isType = _cache.PluginsCache.TryGetValue(pluginType, out ConcurrentDictionary<string, G4PluginCacheModel> plugins);

            // If the plugin type exists, attempt to retrieve the cache model for the given key.
            var isPlugin = plugins.TryGetValue(key, out G4PluginCacheModel cacheModel) && isType;

            // Return the manifest if the plugin is found, otherwise return the default value (null).
            return isPlugin ? (T)cacheModel.Manifest : default;
        }

        /// <inheritdoc />
        public IEnumerable<T> GetManifests<T>(params G4ExternalRepositoryModel[] repositories) where T : IG4PluginReference
        {
            // Synchronize the cache with the provided external repositories.
            SyncCache(_cache, repositories);

            // Retrieve the manifests from the cache after synchronization.
            return GetManifests<T>();
        }

        /// <inheritdoc />
        public IEnumerable<T> GetManifests<T>() where T : IG4PluginReference
        {
            // Flatten the cache and retrieve all manifests, casting each to the specified type.
            return _cache
                .PluginsCache
                .Values
                .SelectMany(i => i.Values)
                .Select(i => (T)i.Manifest);
        }

        /// <inheritdoc />
        public void SyncCache(CacheManager cache)
        {
            // Create a new instance of CacheManager that holds the source data.
            var cacheManager = CacheManager.Instance;

            // Iterate through each item in the newly created cache.
            foreach (var item in cacheManager.PluginsCache)
            {
                // Sync the provided cache with the new cache data, creating a new entry with case-insensitive keys.
                cache.PluginsCache[item.Key] = new(item.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <inheritdoc />
        public void SyncCache(CacheManager cache, params G4ExternalRepositoryModel[] repositories)
        {
            // Ensure repositories are not null before proceeding.
            repositories ??= [];

            // Retrieve external plugins from the provided repositories.
            foreach (var item in repositories.GetExternalPlugins())
            {
                // Iterate through each plugin within the current plugin type group.
                foreach (var plugin in item.Value)
                {
                    // Update or add the plugin to the cache under the corresponding plugin type and key.
                    cache.PluginsCache[item.Key][plugin.Key] = plugin.Value;
                }
            }
        }

        // Scans all cached types for those decorated with G4DriverPluginAttribute and aggregates them
        // into a key-value pair representing driver data.
        private static ConcurrentDictionary<string, G4PluginCacheModel> FindDrivers()
        {
            // Retrieve all types that are currently stored in the CacheManager.
            var types = CacheManager.Types.Values;

            // Prepare a list to hold all driver-related data objects.
            var cacheData = new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase);

            // Iterate over each type in the cache.
            foreach (var type in types)
            {
                // Attempt to retrieve the G4DriverPluginAttribute from the current type, if present.
                var attribute = type.GetCustomAttribute<G4DriverPluginAttribute>();

                // If the attribute is null, this type is not a driver plugin, so skip it.
                if (attribute is null || string.IsNullOrEmpty(attribute.Driver))
                {
                    continue;
                }

                // Construct a minimal data object for this driver plugin.
                // Add this driver data object to the overall collection.
                cacheData[attribute.Driver] = new G4PluginCacheModel
                {
                    // The driver plugin's assembly is the same as the type's assembly.
                    Assembly = type.Assembly,

                    // The driver plugin does not reference any additional assemblies.
                    ReferencedAssemblies = [],

                    // The type of this plugin is "Driver."
                    Type = type,

                    // At the moment, no custom documentation is available, so we provide a placeholder.
                    Document = "No document available.",

                    // Build a nested object to represent the manifest details of this driver plugin.
                    Manifest = new G4PluginAttribute
                    {
                        Categories = ["WebDriverClients"],
                        Description = [attribute.Description],
                        Key = attribute.Driver,
                        PluginType = "Driver",
                        Summary = [attribute.Description]
                    }
                };
            }

            // Return the collection of driver data objects.
            return cacheData;
        }
        #endregion
    }
}
