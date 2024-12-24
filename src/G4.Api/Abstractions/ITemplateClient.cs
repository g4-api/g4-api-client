using G4.Models;

using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines the contract for managing plugin templates within the G4 system, including adding, removing, and retrieving templates.
    /// </summary>
    public interface ITemplateClient
    {
        /// <summary>
        /// Adds a new plugin template to the system by updating the LiteDB collection and the in-memory cache.
        /// </summary>
        /// <param name="manifest">The manifest containing information about the plugin to be added.</param>
        /// <returns>
        /// An integer representing the status code of the operation.
        /// Returns <see cref="StatusCodes.Status204NoContent"/> if the template is added successfully,
        /// or <see cref="StatusCodes.Status500InternalServerError"/> if an error occurs during the process.
        /// </returns>
        public int AddTemplate(IG4PluginManifest manifest);

        /// <summary>
        /// Clears all plugin templates by dropping each collection in the LiteDB database.
        /// </summary>
        public void ClearTemplates();

        /// <summary>
        /// Retrieves a plugin template based on the provided key.
        /// </summary>
        /// <param name="key">The unique identifier for the plugin template.</param>
        /// <returns>
        /// A tuple containing the status code of the operation and the retrieved plugin manifest.
        /// If the template is found, <paramref name="StatusCode"/> is <see cref="StatusCodes.Status200OK"/>
        /// and <paramref name="Manifest"/> contains the plugin manifest.
        /// If the template is not found, <paramref name="StatusCode"/> is <see cref="StatusCodes.Status404NotFound"/>
        /// and <paramref name="Manifest"/> is <c>null</c>.
        /// </returns>
        public (int StatusCode, IG4PluginManifest Manifest) GetTemplate(string key);

        /// <summary>
        /// Retrieves all templates manifests stored in the system.
        /// </summary>
        /// <returns>
        /// An enumerable of <see cref="IG4PluginManifest"/> representing all available plugin manifests.
        /// If no templates are found, an empty enumerable is returned.
        /// </returns>
        public IEnumerable<IG4PluginManifest> GetTemplates();

        /// <summary>
        /// Removes a plugin template identified by the specified key from the LiteDB database.
        /// </summary>
        /// <param name="key">The unique identifier for the plugin template to be removed.</param>
        /// <returns>
        /// An integer representing the status code of the operation.
        /// Returns <see cref="StatusCodes.Status204NoContent"/> if the template is successfully removed,
        /// or <see cref="StatusCodes.Status404NotFound"/> if the template or the templates collection is not found.
        /// </returns>
        public int RemoveTemplate(string key);
    }
}
