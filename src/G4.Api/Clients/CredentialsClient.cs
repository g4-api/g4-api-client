using G4.Api.Abstractions;
using G4.Cache;
using G4.Credentials;
using G4.Credentials.Models;

using Microsoft.Data.Sqlite;

using System;
using System.Collections.Generic;
using System.Linq;

namespace G4.Api.Clients
{
    /// <summary>
    /// Provides credential persistence and caching capabilities for OAuth and related credential types.
    /// </summary>
    internal class CredentialsClient : ClientBase, ICredentialsClient
    {
        #region *** Fields       ***
        // Manages caching mechanisms for plugins.
        private readonly CacheManager _cache;

        // Manages credentials for external integrations.
        private readonly CredentialsManager _credentials;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialsClient"/> class
        /// using the shared SQLite connection from <see cref="CacheManager"/>.
        /// </summary>
        public CredentialsClient()
            : this(CacheManager.SqliteConnection)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CredentialsClient"/> class
        /// with an explicit <see cref="SqliteConnection"/>.
        /// </summary>
        /// <param name="sqliteConnection">The SQLite connection used by the underlying <see cref="CredentialsManager"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sqliteConnection"/> is null.</exception>
        public CredentialsClient(SqliteConnection sqliteConnection)
        {
            // Validate that the provided SQLite connection is not null.
            ArgumentNullException.ThrowIfNull(sqliteConnection);

            // Obtain shared in-memory cache instance
            _cache = CacheManager.Instance;

            // Initialize credentials manager with provided SQLite connection
            _credentials = new CredentialsManager(sqliteConnection);
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public OAuthCredentialModel GetCredentials(string idOrName)
        {
            // Get credentials from the underlying credentials database manager.
            // This will not use the cache, as credentials may be updated or created outside of this client.
            return _credentials.GetCredentials(idOrName);
        }

        /// <inheritdoc />
        public IDictionary<string, OAuthCredentialModel> GetCredentials()
        {
            // Get all credentials from the underlying credentials database manager.
            // This will not use the cache, as credentials may be updated or created outside of this client.
            return _credentials
                .GetCredentials()
                .ToDictionary(i => ($"{i.Name};{i.Id}".ToLower()), i => i);
        }

        /// <inheritdoc />
        public OAuthCredentialsResponseModel NewCredentials(OAuthCredentialModel oauth)
        {
            // Create new credentials using the underlying credentials manager.
            return _credentials.NewCredentials(oauth);
        }

        /// <inheritdoc />
        public int RemoveCredentials(string idOrName)
        {
            // Resolve the credential so we can compute the cache key
            var credentials = _credentials.GetCredentials(idOrName);

            // No credential found matching the provided identifier or name, so return 0 records removed.
            if (credentials == null) {
                return 0;
            }

            // Remove from persistent store
            var removedCount = _credentials.RemoveCredentials(idOrName: credentials.Id);

            // Build normalized cache key (Name + Id, case-insensitive)
            // Using invariant lowercase to ensure consistent dictionary access
            var key = $"{credentials.Name};{credentials.Id}".ToLowerInvariant();

            // Remove from in-memory cache if it exists
            _cache.CredentialsCache.Remove(key);

            // Return the number of records removed from the persistent store
            return removedCount;
        }

        /// <inheritdoc />
        public OAuthCredentialModel SaveCredentials(OAuthCredentialModel oauth)
        {
            // Persist credentials using the underlying storage provider
            var credentials = _credentials.SaveCredentials(oauth)
                ?? throw new InvalidOperationException("Failed to persist OAuth credentials.");

            // Build normalized cache key (Name + Id, case-insensitive)
            // Using invariant lowercase to ensure consistent dictionary access
            var key = $"{credentials.Name};{credentials.Id}".ToLowerInvariant();

            // Store in in-memory cache for fast lookup
            _cache.CredentialsCache[key] = credentials;

            // Return the persisted credentials to the caller
            return credentials;
        }
        #endregion
    }
}
