using G4.Abstraction.Logging;
using G4.Api.Abstractions;
using G4.Api.Models;
using G4.Attributes;
using G4.Attributes.Abstraction;
using G4.Cache;
using G4.Credentials;
using G4.Credentials.Models;
using G4.Extensions;
using G4.Models;
using G4.Plugins.Clients;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
        // Longest time the MCP sync waits for the servers, which it queries at the same time. One server took about
        // 5 seconds, while the MCP transport keeps retrying a refused connection for about 30 seconds; a server still
        // busy after this limit is reported as not answering and skipped, well inside Studio's 60-second sync timeout.
        private static readonly TimeSpan McpServerSyncTimeout = TimeSpan.FromSeconds(15);

        // Manages caching mechanisms for plugins.
        private readonly CacheManager _cache;

        // Manages credentials for external integrations.
        private readonly CredentialsManager _credentials;

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

            // Initialize the credentials manager using a connection from the CacheManager.
            _credentials = new CredentialsManager(CacheManager.SqliteConnection);

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
        public OAuthCredentialModel GetCredentials(string idOrName)
        {
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;

            return _cache
                .CredentialsCache
                .Values
                .FirstOrDefault(i => i.Id.Equals(idOrName, comparison) || i.Name.Equals(idOrName, comparison));
        }

        /// <inheritdoc />
        public IDictionary<string, OAuthCredentialModel> GetCredentials()
        {
            return _cache.CredentialsCache;
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

            // Retrieve external plugins from the provided repositories, one model per plugin (buckets also hold aliases).
            var cacheModels = repositories
                .GetExternalPlugins()
                .SelectMany(i => i.Value.Values)
                .Distinct()
                .ToArray();

            // Add them through the cache's validated sync, which creates missing plugin-type buckets.
            SyncCacheModels(cache, source: "external repositories", cacheModels);
        }

        /// <inheritdoc />
        public IReadOnlyList<McpServerSyncResultModel> SyncCache(
            CacheManager cache,
            IDictionary<string, McpServerModel> servers)
        {
            // Ensure the server collection is always initialized before attempting synchronization.
            servers ??= new Dictionary<string, McpServerModel>();

            // Query every server at the same time, so the sync takes as long as the slowest server, not their sum.
            var queries = servers
                .Select(server => (
                    Name: server.Key,
                    Server: server.Value,
                    Query: Task.Run(() => GetServerPlugins(server.Key, server.Value))))
                .ToArray();

            // Wait for all of them, but no longer than the time limit; a server still busy after it is reported and skipped.
            Task.WhenAny(Task.WhenAll(queries.Select(i => i.Query)), Task.Delay(McpServerSyncTimeout)).Wait();

            // Add the plugins of every server that answered, and describe the outcome of each server.
            var results = new List<McpServerSyncResultModel>();
            var syncedServerUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var syncedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, server, query) in queries)
            {
                var result = NewServerSyncResult(name, server, query);
                results.Add(result);

                // A failed or slow server keeps its previous plugins, so a short outage does not remove its tools.
                if (!result.IsSynced)
                {
                    G4Logger.Instance.LogWarning(
                        "MCP server '{Name}' ({Url}) was not synchronized: {Error}", name, server?.Url, result.Error);
                    continue;
                }

                // Add the server's plugins and remember them, so the stale-entry pass keeps them.
                var cacheModels = query.Result;
                SyncCacheModels(cache, source: $"MCP server '{name}'", cacheModels);

                syncedServerUrls.Add(server.Url);

                foreach (var cacheModel in cacheModels)
                {
                    syncedEntries.Add(NewEntryKey(cacheModel.Manifest.PluginType, cacheModel.Manifest.Key));
                }
            }

            // Remove plugins of servers that are no longer configured, and plugins a synchronized server dropped.
            var configuredServerUrls = new HashSet<string>(
                servers.Values.Select(i => i?.Url).Where(i => !string.IsNullOrEmpty(i)),
                StringComparer.OrdinalIgnoreCase);

            RemoveStaleServerPlugins(cache, new StaleServerPluginsOptions
            {
                ConfiguredServerUrls = configuredServerUrls,
                SyncedEntries = syncedEntries,
                SyncedServerUrls = syncedServerUrls
            });

            return results;

            // Lists the plugins of one server as cache models. Runs on a worker thread, so a slow server cannot hold
            // up the others; any failure (refused connection, protocol error) surfaces through the returned task.
            static G4PluginCacheModel[] GetServerPlugins(string name, McpServerModel server)
            {
                // Connect and list the server's tools; the client connects in its constructor.
                var client = new ModelContextProtocolClient(name, server);

                // One model per plugin; a bucket holds each model under its key and its aliases.
                return [.. client
                    .GetModelContextPlugins(name, server)
                    .SelectMany(i => i.Value.Values)
                    .Distinct()];
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

        // Builds the key that identifies one plugin across servers: its plugin type and canonical key.
        private static string NewEntryKey(string pluginType, string key) => $"{pluginType}/{key}";

        // Describes the outcome of one server query: synchronized with its plugin count, failed with the innermost
        // error, or still running after the time limit. The query is read only when it completed successfully.
        private static McpServerSyncResultModel NewServerSyncResult(
            string name,
            McpServerModel server,
            Task<G4PluginCacheModel[]> query)
        {
            // Start from the server identity shared by every outcome.
            var result = new McpServerSyncResultModel
            {
                Name = name,
                Url = server?.Url
            };

            // A server that is still busy after the time limit is skipped; its task finishes on its own.
            if (!query.IsCompleted)
            {
                result.Error = $"The server did not answer within {McpServerSyncTimeout.TotalSeconds} seconds.";
                return result;
            }

            // A failed query names its real cause, which is wrapped in an AggregateException.
            if (query.IsFaulted || query.IsCanceled)
            {
                result.Error = query.Exception?.GetBaseException().Message ?? "The server query was canceled.";
                return result;
            }

            // The server answered: report how many plugins it provided.
            result.IsSynced = true;
            result.PluginCount = query.Result.Length;

            return result;
        }

        // Removes MCP plugins that no longer have a source: plugins of servers that are not configured any more, and
        // plugins a synchronized server no longer provides. Plugins of a configured server that failed are kept.
        private static void RemoveStaleServerPlugins(CacheManager cache, StaleServerPluginsOptions options)
        {
            // Collect the stale MCP plugins from a snapshot, once per model (buckets also hold aliases).
            var staleModels = cache.PluginsCache
                .SelectMany(bucket => bucket.Value.Values.Select(model => (PluginType: bucket.Key, Model: model)))
                .Where(i => !string.IsNullOrEmpty(i.Model?.ModelContextServer?.Url))
                .DistinctBy(i => i.Model)
                .Where(i => TestStaleServerPlugin(i.PluginType, i.Model, options))
                .ToArray();

            // Remove each one through the cache, which also drops its aliases and raises the change event.
            foreach (var (pluginType, model) in staleModels)
            {
                try
                {
                    cache.RemoveCacheEntity(pluginType, model.Manifest.Key);
                }
                catch (Exception exception)
                {
                    // A concurrent sync may have removed it already; the rest are still removed.
                    G4Logger.Instance.LogWarning(
                        "Stale MCP plugin '{Key}' could not be removed: {Error}",
                        model.Manifest.Key,
                        exception.GetBaseException().Message);
                }
            }

            // Tests whether one cached MCP plugin lost its source.
            static bool TestStaleServerPlugin(string pluginType, G4PluginCacheModel model, StaleServerPluginsOptions options)
            {
                // A plugin of a server that is not configured any more has no source.
                var serverUrl = model.ModelContextServer.Url;

                if (!options.ConfiguredServerUrls.Contains(serverUrl))
                {
                    return true;
                }

                // A plugin of a server that answered is stale when the server no longer provides it.
                return options.SyncedServerUrls.Contains(serverUrl)
                    && !options.SyncedEntries.Contains(NewEntryKey(pluginType, model.Manifest.Key));
            }
        }

        // Adds plugins through the cache's validated sync, one at a time, so a plugin whose key or alias conflicts with
        // a cached one is logged and skipped while the rest are added.
        private static void SyncCacheModels(CacheManager cache, string source, IEnumerable<G4PluginCacheModel> cacheModels)
        {
            foreach (var cacheModel in cacheModels)
            {
                try
                {
                    // The cache creates a missing plugin-type bucket, validates the plugin, and registers its aliases.
                    cache.SyncCache(cacheModel);
                }
                catch (Exception exception)
                {
                    G4Logger.Instance.LogWarning(
                        "Plugin '{Key}' from {Source} was not added: {Error}",
                        cacheModel?.Manifest?.Key,
                        source,
                        exception.GetBaseException().Message);
                }
            }
        }
        #endregion

        #region *** Nested Types ***
        // Carries the sources found by one MCP sync to the stale-plugin pass, so the pass needs no further parameters.
        private sealed class StaleServerPluginsOptions
        {
            // Gets or sets the URLs of every configured server, whether or not it answered.
            public HashSet<string> ConfiguredServerUrls { get; init; }

            // Gets or sets the plugin-type and key pairs the answering servers provided.
            public HashSet<string> SyncedEntries { get; init; }

            // Gets or sets the URLs of the servers that answered.
            public HashSet<string> SyncedServerUrls { get; init; }
        }
        #endregion
    }
}
