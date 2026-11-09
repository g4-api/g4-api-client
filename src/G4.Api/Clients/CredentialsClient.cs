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
    /// Provides credential persistence and caching capabilities for every known credential type.
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
        public CredentialModelBase GetCredentials(string idOrName)
        {
            // Reject an empty lookup value before querying the store, which binds it as a command parameter.
            if (string.IsNullOrEmpty(idOrName))
            {
                return null;
            }

            // Query the store of every credential type, not the cache, as records may be created or changed outside
            // of this client. The types are listed in ordinal type-name order, which decides a shared name.
            CredentialModelBase[] credentials =
            [
                _credentials.GetCredentials(idOrName),
                _credentials.GetSqlServerCredentials(idOrName)
            ];

            // Resolve the record by identifier first and by name second among the records that were found.
            return FindCredential(credentials: credentials.Where(i => i != null), idOrName);
        }

        /// <inheritdoc />
        public IDictionary<string, IDictionary<string, CredentialModelBase>> GetCredentials()
        {
            // Read every record of every credential type from the store, not the cache, as records may be created or
            // changed outside of this client.
            IEnumerable<CredentialModelBase> credentials =
            [
                .. _credentials.GetCredentials(),
                .. _credentials.GetSqlServerCredentials()
            ];

            // Group the records with the engine cache builder, so the result has the exact partitions and keys of the
            // credentials cache, including empty partitions.
            return CacheManager.NewCredentialsCache(credentials);
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
            // Resolve the record of any credential type, so the delete targets its table and its cache partition.
            var credential = GetCredentials(idOrName);

            // No credential found matching the provided identifier or name, so return 0 records removed.
            if (credential == null)
            {
                return 0;
            }

            // Remove the record from the table of its credential type, addressed by its unique identifier; a type
            // without a table in the store has no rows to remove.
            var removedCount = credential switch
            {
                OAuthCredentialModel => _credentials.RemoveCredentials(idOrName: credential.Id),
                SqlServerCredentialModel => _credentials.RemoveSqlServerCredentials(idOrName: credential.Id),
                _ => 0
            };

            // Build the normalized cache key (Name + Id, case-insensitive) the engine cache builder uses.
            var key = $"{credential.Name};{credential.Id}".ToLowerInvariant();

            // Remove the record from the cache partition of its credential type, when the partition exists.
            if (_cache.CredentialsCache.TryGetValue(key: credential.CredentialType, value: out var partition))
            {
                partition.Remove(key);
            }

            // Return the number of records removed from the persistent store.
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

            // Store the record in the OAuth partition of the in-memory cache for fast lookup; every known credential
            // type always has a partition.
            _cache.CredentialsCache[CredentialTypes.OAuth][key] = credentials;

            // Return the persisted credentials to the caller
            return credentials;
        }

        // Resolves one credential record from the specified records: an identifier match wins over a name match, both
        // compared case-insensitively, and the first match in the sequence order wins.
        private static CredentialModelBase FindCredential(IEnumerable<CredentialModelBase> credentials, string idOrName)
        {
            // Materialize the records once so the identifier pass and the name pass read the same snapshot.
            const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
            var candidates = credentials.ToArray();

            // Prefer the identifier, which is unique, and fall back to the name, which two types can share.
            return candidates.FirstOrDefault(i => string.Equals(a: i.Id, b: idOrName, comparisonType: comparison))
                ?? candidates.FirstOrDefault(i => string.Equals(a: i.Name, b: idOrName, comparisonType: comparison));
        }
        #endregion
    }
}
