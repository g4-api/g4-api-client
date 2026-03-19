using G4.Credentials.Models;

using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines a client interface for managing OAuth credentials, including retrieval,
    /// creation, and persistence of credential models.
    /// </summary>
    public interface ICredentialsClient
    {
        /// <summary>
        /// Retrieves a single <see cref="OAuthCredentialModel"/> by its identifier or name.
        /// </summary>
        /// <param name="idOrName">The unique identifier or logical name of the credential.</param>
        /// <returns>The matching <see cref="OAuthCredentialModel"/>.</returns>
        OAuthCredentialModel GetCredentials(string idOrName);

        /// <summary>
        /// Retrieves all stored <see cref="OAuthCredentialModel"/> entries.
        /// </summary>
        /// <returns>A dictionary where the keys are credential identifiers and names, and the values are the corresponding <see cref="OAuthCredentialModel"/> instances.</returns>
        IDictionary<string, OAuthCredentialModel> GetCredentials();

        /// <summary>
        /// Creates a new <see cref="OAuthCredentialsResponseModel"/> from the provided
        /// <see cref="OAuthCredentialModel"/>.
        /// </summary>
        /// <param name="oauth">The source OAuth credential model containing the client configuration.</param>
        /// <returns>An <see cref="OAuthCredentialsResponseModel"/> representing the initializedOAuth credential response.</returns>
        OAuthCredentialsResponseModel NewCredentials(OAuthCredentialModel oauth);

        /// <summary>
        /// Removes OAuth credential records that match the specified Id or Name.
        /// </summary>
        /// <param name="idOrName">The credential identifier or friendly name to delete.</param>
        /// <returns>The number of rows removed from the <c>OAuthCredentials</c> table.</returns>
        int RemoveCredentials(string idOrName);

        /// <summary>
        /// Persists the provided <see cref="OAuthCredentialModel"/> to the underlying
        /// credential store.
        /// </summary>
        /// <param name="obj">The OAuth credential model to save.</param>
        /// <returns>The saved <see cref="OAuthCredentialModel"/>.</returns>
        OAuthCredentialModel SaveCredentials(OAuthCredentialModel obj);
    }
}
