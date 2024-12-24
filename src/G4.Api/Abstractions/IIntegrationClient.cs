using G4.Attributes.Abstraction;
using G4.Cache;
using G4.Models;

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines a contract for the IntegrationClient, responsible for retrieving and managing metadata from the G4 engine.
    /// This facilitates seamless integration with external clients by providing access to plugin caches, documents, and manifests.
    /// The interface includes methods for cache synchronization, document retrieval, and manifest management across different plugins and repositories.
    /// </summary>
    public interface IIntegrationClient
    {
        /// <summary>
        /// Retrieves the current plugin cache. If external repositories are provided, the cache is synchronized with them before returning the cache.
        /// </summary>
        /// <param name="repositories">An optional array of external repository models to synchronize the cache with.</param>
        /// <returns>A dictionary where the keys are plugin types, and the values are dictionaries containing plugin cache models.</returns>
        IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> GetCache(params G4ExternalRepositoryModel[] repositories);

        /// <summary>
        /// Retrieves the current plugin cache.
        /// </summary>
        /// <returns>A dictionary where the keys are plugin types and the values are dictionaries containing plugin cache models.</returns>
        IDictionary<string, ConcurrentDictionary<string, G4PluginCacheModel>> GetCache();

        /// <summary>
        /// Retrieves the document associated with the specified key. If the document is not found in the cache,
        /// it synchronizes the cache with the provided external repository and attempts to retrieve the document again.
        /// </summary>
        /// <param name="key">The unique key identifying the document to retrieve.</param>
        /// <param name="repository">The external repository model used to synchronize the cache if the document is not found.</param>
        /// <returns>A string representing the document if found; otherwise, an empty string.</returns>
        string GetDocument(string key, G4ExternalRepositoryModel repository);

        /// <summary>
        /// Retrieves the document associated with the specified plugin type and key. If the document is not found in the cache,
        /// it synchronizes the cache with the provided external repository and attempts to retrieve the document again.
        /// </summary>
        /// <param name="pluginType">The type of plugin to search within.</param>
        /// <param name="key">The unique key identifying the document to retrieve.</param>
        /// <param name="repository">The external repository model used to synchronize the cache if the document is not found.</param>
        /// <returns>A string representing the document if found; otherwise, an empty string.</returns>
        string GetDocument(string pluginType, string key, G4ExternalRepositoryModel repository);

        /// <summary>
        /// Retrieves the document associated with the specified key from any plugin in the cache.
        /// </summary>
        /// <param name="key">The unique key identifying the document to retrieve.</param>
        /// <returns>A string representing the document if found; otherwise, an empty string.</returns>
        string GetDocument(string key);

        /// <summary>
        /// Retrieves the document associated with the specified plugin type and key from the cache.
        /// </summary>
        /// <param name="pluginType">The type of plugin to search within.</param>
        /// <param name="key">The unique key identifying the document to retrieve.</param>
        /// <returns>A string representing the document if found; otherwise, an empty string.</returns>
        string GetDocument(string pluginType, string key);

        /// <summary>
        /// Retrieves a driver plugin from the cache by its unique key.
        /// </summary>
        /// <param name="key">The unique key identifying the driver plugin.</param>
        /// <returns>An object implementing <see cref="IG4PluginReference"/> if a matching driver is found; otherwise, <c>null</c> if no driver exists for the specified key.</returns>
        IG4PluginReference GetDriver(string key);

        /// <summary>
        /// Retrieves all available driver plugins from the cache.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{T}"/> of <see cref="IG4PluginReference"/> representing all cached drivers. Returns an empty collection if no drivers are found.</returns>
        IEnumerable<IG4PluginReference> GetDrivers();

        /// <summary>
        /// Retrieves the manifest associated with the specified key from the cache. If the manifest is not found,
        /// it synchronizes the cache with the provided external repository and attempts to retrieve the manifest again.
        /// </summary>
        /// <param name="key">The unique key identifying the manifest to retrieve.</param>
        /// <param name="repository">The external repository model used to synchronize the cache if the manifest is not found.</param>
        /// <returns>The <see cref="IG4PluginManifest"/> if found; otherwise, returns the default value (null).</returns>
        T GetManifest<T>(string key, G4ExternalRepositoryModel repository) where T : IG4PluginReference;

        /// <summary>
        /// Retrieves the manifest associated with the specified plugin type and key from the cache.
        /// If the manifest is not found, it synchronizes the cache with the provided external repository
        /// and attempts to retrieve the manifest again.
        /// </summary>
        /// <param name="pluginType">The type of plugin to search within.</param>
        /// <param name="key">The unique key identifying the manifest to retrieve.</param>
        /// <param name="repository">The external repository model used to synchronize the cache if the manifest is not found.</param>
        /// <returns>The <see cref="IG4PluginManifest"/> if found; otherwise, returns the default value (null).</returns>
        T GetManifest<T>(string pluginType, string key, G4ExternalRepositoryModel repository) where T : IG4PluginReference;

        /// <summary>
        /// Retrieves the manifest associated with the specified key from the plugin cache.
        /// </summary>
        /// <param name="key">The unique key identifying the manifest to retrieve.</param>
        /// <returns>The <see cref="IG4PluginManifest"/> if found; otherwise, returns the default value (null).</returns>
        T GetManifest<T>(string key) where T : IG4PluginReference;

        /// <summary>
        /// Retrieves the manifest associated with the specified plugin type and key from the cache.
        /// </summary>
        /// <param name="pluginType">The type of plugin to search within.</param>
        /// <param name="key">The unique key identifying the manifest to retrieve.</param>
        /// <returns>The <see cref="IG4PluginManifest"/> if found; otherwise, returns the default value (null).</returns>
        T GetManifest<T>(string pluginType, string key) where T : IG4PluginReference;

        /// <summary>
        /// Retrieves a collection of plugin manifests by synchronizing the cache with the specified external repositories.
        /// </summary>
        /// <typeparam name="T">The type of plugin manifest, which must implement <see cref="IG4PluginReference"/>.</typeparam>
        /// <param name="repositories">An array of external repository models to synchronize the cache with.</param>
        /// <returns>An enumerable collection of plugin manifests of type <typeparamref name="T"/>.</returns>
        IEnumerable<T> GetManifests<T>(params G4ExternalRepositoryModel[] repositories) where T : IG4PluginReference;

        /// <summary>
        /// Retrieves a collection of plugin manifests from the cache.
        /// </summary>
        /// <typeparam name="T">The type of plugin manifest, which must implement <see cref="IG4PluginReference"/>.</typeparam>
        /// <returns>An enumerable collection of plugin manifests of type <typeparamref name="T"/>.</returns>
        IEnumerable<T> GetManifests<T>() where T : IG4PluginReference;

        /// <summary>
        /// Synchronizes the provided <paramref name="cache"/> instance by either updating it with new data or using it as the source of truth to sync other caches.
        /// Depending on the context, the <paramref name="cache"/> can either hold the new data to sync from or be the instance that needs to be updated.
        /// </summary>
        /// <param name="cache">The <see cref="CacheManager"/> instance that will either be updated or serve as the source of new cache data.</param>
        /// <param name="repositories">An array of external repository models that may be used to generate new cache data if necessary.</param>
        void SyncCache(CacheManager cache, params G4ExternalRepositoryModel[] repositories);

        /// <summary>
        /// Synchronizes the provided <paramref name="cache"/> instance by either updating it with default data or using it as the source of truth to sync other caches.
        /// Depending on the context, the <paramref name="cache"/> can either hold the default cache data to sync from or be the instance that needs to be updated.
        /// </summary>
        /// <param name="cache">The <see cref="CacheManager"/> instance that will either be updated or serve as the source of default cache data.</param>
        void SyncCache(CacheManager cache);
    }
}
