using G4.Credentials.Models;

using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines a client interface for managing credential records of every known credential type, including
    /// retrieval, OAuth creation, persistence, and removal.
    /// </summary>
    public interface ICredentialsClient
    {
        /// <summary>
        /// Retrieves a single credential record of any known credential type by its identifier or name.
        /// </summary>
        /// <remarks>
        /// The lookup reads the persistent credential store, not the cache, so it sees records created or changed
        /// outside this client. An identifier match wins over a name match. When records of two types share the name,
        /// the record of the type that comes first in ordinal type-name order wins.
        /// </remarks>
        /// <param name="idOrName">The unique identifier or logical name of the credential.</param>
        /// <returns>
        /// The matching record as its concrete model, such as <see cref="OAuthCredentialModel"/> or
        /// <see cref="SqlServerCredentialModel"/>; otherwise <c>null</c>.
        /// </returns>
        CredentialModelBase GetCredentials(string idOrName);

        /// <summary>
        /// Retrieves every stored credential record, grouped by credential type.
        /// </summary>
        /// <remarks>
        /// The records are read from the persistent credential store and returned in the shape of
        /// <see cref="G4.Cache.CacheManager.CredentialsCache"/>: one partition per known credential type, including
        /// empty partitions.
        /// </remarks>
        /// <returns>
        /// A dictionary keyed by credential type (see <see cref="CredentialTypes"/>), where each value maps the
        /// lower-case <c>name;id</c> key of a record to the record.
        /// </returns>
        IDictionary<string, IDictionary<string, CredentialModelBase>> GetCredentials();

        /// <summary>
        /// Creates a new <see cref="OAuthCredentialsResponseModel"/> from the provided
        /// <see cref="OAuthCredentialModel"/>.
        /// </summary>
        /// <param name="oauth">The source OAuth credential model containing the client configuration.</param>
        /// <returns>An <see cref="OAuthCredentialsResponseModel"/> representing the initializedOAuth credential response.</returns>
        OAuthCredentialsResponseModel NewCredentials(OAuthCredentialModel oauth);

        /// <summary>
        /// Removes the credential record of any known credential type that matches the specified Id or Name.
        /// </summary>
        /// <remarks>
        /// The record is resolved the same way as <see cref="GetCredentials(string)"/>, removed from the table of its
        /// credential type, and removed from the matching partition of the credentials cache.
        /// </remarks>
        /// <param name="idOrName">The credential identifier or friendly name to delete.</param>
        /// <returns>The number of rows removed from the persistent store; <c>0</c> when no record matches.</returns>
        int RemoveCredentials(string idOrName);

        /// <summary>
        /// Persists the provided <see cref="OAuthCredentialModel"/> to the underlying
        /// credential store.
        /// </summary>
        /// <remarks>
        /// This method completes the OAuth authorization flow, so it accepts and returns the OAuth model. The saved
        /// record is also written to the <see cref="CredentialTypes.OAuth"/> partition of the credentials cache.
        /// </remarks>
        /// <param name="obj">The OAuth credential model to save.</param>
        /// <returns>The saved <see cref="OAuthCredentialModel"/>.</returns>
        OAuthCredentialModel SaveCredentials(OAuthCredentialModel obj);
    }
}
