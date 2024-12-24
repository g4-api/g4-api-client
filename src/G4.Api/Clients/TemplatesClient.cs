using G4.Api.Abstractions;
using G4.Attributes;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Plugins.Framework;

using LiteDB;

using System;
using System.Collections.Generic;
using System.Linq;

namespace G4.Api.Clients
{
    /// <summary>
    /// Provides methods to manage templates within the G4 system.
    /// </summary>
    internal class TemplatesClient(CacheManager cache) : ClientBase, ITemplateClient
    {
        #region *** Constants    ***
        // The name of the collection used to store templates in the LiteDB database
        private const string CollectionName = "Templates";
        #endregion

        #region *** Fields       ***
        // Assuming _cache is a field within the PluginManager class
        private readonly CacheManager _cache = cache;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public int AddTemplate(IG4PluginManifest manifest)
        {
            // Remove any existing template with the same key to avoid duplication
            static void RemoveDuplicates(TemplatesClient client, string key)
            {
                // Retrieve the collection and associated documents based on the provided key
                var (collection, documents) = GetDocuments(client.LiteDatabase, key);

                // If there are no documents, exit the method
                if (documents.Length == 0)
                {
                    return;
                }

                // Select the Ids of the documents to be removed and convert them to BsonValue
                var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

                // Delete all documents with the selected Ids from the collection
                collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));
            }

            // Confirm that the provided manifest is a valid template by checking circular references
            manifest.ConfirmTemplate();

            // Retrieve the "Templates" collection from LiteDB, using GUIDs for automatic ID generation
            var collection = LiteDatabase.GetCollection<G4PluginAttribute>(name: CollectionName, autoId: BsonAutoId.Guid);

            // Create a new cache model based on the provided manifest
            var cacheModel = new G4PluginCacheModel
            {
                Assembly = typeof(TemplatePlugin).Assembly,
                Document = manifest.ConvertToMarkdown(includeNavigationLinks: false),
                Manifest = manifest,
                Type = typeof(TemplatePlugin)
            };

            // Set the plugin type to "Action" within the manifest
            cacheModel.Manifest.PluginType = "Action";

            // Check if the manifest is of type G4PluginAttribute
            if (cacheModel.Manifest is not G4PluginAttribute document)
            {
                // Return 400 Bad Request if the manifest type is invalid
                return 400;
            }

            // Remove any existing template with the same key to avoid duplication
            // Retrieve the Templates collection from LiteDB
            RemoveDuplicates(client: this, manifest.Key);

            // Insert the new or updated template document into the collection
            collection.Insert(document);

            // Update the in-memory plugins cache with the new cache model
            _cache.SyncCache(cacheModel);

            // Return 204 No Content to indicate that the template was added successfully
            return 204;
        }

        /// <inheritdoc />
        public void ClearTemplates()
        {
            // Retrieve the collection for G4PluginAttribute from the LiteDatabase.
            var collection = LiteDatabase.GetCollection<G4PluginAttribute>(name: CollectionName);

            // Return immediately if the collection does not exist.
            if (collection == default)
            {
                return;
            }

            // Retrieve the collection and associated documents for the specified environment.
            var documents = collection
                .Find(i => i.Type.Equals(nameof(IG4PluginManifest), StringComparison.OrdinalIgnoreCase));

            // Select the Ids of the documents to be removed and convert them to BsonValue.
            var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

            // Delete all documents with the selected Ids from the collection.
            collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));
        }

        /// <inheritdoc />
        public (int StatusCode, IG4PluginManifest Manifest) GetTemplate(string key)
        {
            // Retrieve documents associated with the provided key from the LiteDatabase
            var documents = GetDocuments(LiteDatabase, key).Documents;

            // If no documents are found, return 404 Not Found with a default manifest
            // Otherwise, return 200 OK with the first found manifest
            return documents.Length == 0
                ? (404, default)
                : (200, documents[0]);
        }

        /// <inheritdoc />
        public IEnumerable<IG4PluginManifest> GetTemplates()
        {
            // Retrieve the Templates document from LiteDB and access its Templates dictionary
            var documents = LiteDatabase
                .GetCollection<G4PluginAttribute>(name: CollectionName)
                .Find(i => i.Type.Equals(nameof(IG4PluginManifest), StringComparison.OrdinalIgnoreCase));

            // Return the deserialized plugin manifests
            return documents?.Any() != true
                ? []
                : documents;
        }

        /// <inheritdoc />
        public int RemoveTemplate(string key)
        {
            // Retrieve the Templates collection from LiteDB
            var (collection, documents) = GetDocuments(LiteDatabase, key: key);

            // If the Templates collection is null, return a 404 Not Found status code
            if (documents.Length == 0)
            {
                return 404;
            }

            // Select the Ids of the documents to be removed and convert them to BsonValue.
            var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

            // Delete all documents with the selected Ids from the collection.
            collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));

            // Return a 204 No Content status code to indicate successful removal
            return 204;
        }

        // Retrieves a single ApplicationParametersModel document from the specified LiteDatabase collection based on the provided environment name.
        private static (ILiteCollection<G4PluginAttribute> Collection, G4PluginAttribute[] Documents) GetDocuments(ILiteDatabase liteDatabase, string key)
        {
            const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

            // Retrieve the collection for ApplicationParametersModel using the predefined collection name and GUID auto ID.
            var collection = liteDatabase.GetCollection<G4PluginAttribute>(
                name: CollectionName,
                autoId: BsonAutoId.Guid);

            // Search for a document where the Name property matches the provided name, ignoring case.
            // Return the first matching document or null if no match is found.
            var documents = collection
                .Find(i => i.Type.Equals(nameof(IG4PluginManifest), Comparison) && i.Key.Equals(key, Comparison))
                .ToArray();

            // Return the collection and document as a tuple
            return (collection, documents);
        }
        #endregion
    }
}
