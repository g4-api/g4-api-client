using G4.Api;
using G4.Api.Abstractions;
using G4.Cache;
using G4.Credentials;
using G4.Credentials.Models;

using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Engine")]
    [TestCategory("Credentials")]
    [TestCategory("UnitTest")]
    public class CredentialsClientTests
    {
        // Connection string of a private in-memory SQLite database; each connection owns its own database, which is
        // discarded when the connection is disposed.
        private const string InMemoryConnectionString = "Data Source=:memory:";

        [TestMethod(DisplayName = "Verify that the stored credentials are grouped by credential type in the shape of " +
            "the credentials cache, with one partition per known credential type.")]
        public void GetCredentialsGroupsByTypeTest()
        {
            // Arrange: persist one SQL Server record in a private store.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);
            var sqlServer = NewSqlServerCredential();
            new CredentialsManager(sqliteConnection: connection).SaveSqlServerCredentials(sqlServer);

            // Act: read every stored credential.
            var credentials = client.GetCredentials();

            // Assert: every known type has a partition, the OAuth one is empty, and the record sits under its key.
            var key = $"{sqlServer.Name};{sqlServer.Id}".ToLowerInvariant();
            Assert.IsEmpty(collection: credentials[CredentialTypes.OAuth]);
            Assert.HasCount(expected: 1, collection: credentials[CredentialTypes.SqlServer]);
            Assert.AreEqual(expected: sqlServer.Id, actual: credentials[CredentialTypes.SqlServer][key].Id);
        }

        [TestMethod(DisplayName = "Verify that the stored credential lookup returns null when no record matches, or " +
            "when the identifier or name is empty.")]
        #region *** Data Set ***
        [DataRow("unit-test-missing-credential")]
        [DataRow("")]
        [DataRow(null)]
        #endregion
        public void GetCredentialsMissingRecordTest(string idOrName)
        {
            // Arrange: open a private store that holds no records.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);

            // Act: look up a value that matches no stored record.
            var credential = client.GetCredentials(idOrName);

            // Assert: the lookup reports the missing record as null instead of throwing.
            Assert.IsNull(value: credential);
        }

        [TestMethod(DisplayName = "Verify that the stored credential lookup reads a SQL Server record from the store " +
            "by its identifier or name and returns it as its concrete model.")]
        #region *** Data Set ***
        [DataRow(true)]
        [DataRow(false)]
        #endregion
        public void GetCredentialsReadsSqlServerRecordTest(bool byId)
        {
            // Arrange: persist one SQL Server record in a private store, without caching it.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);
            var sqlServer = NewSqlServerCredential();
            new CredentialsManager(sqliteConnection: connection).SaveSqlServerCredentials(sqlServer);

            // Act: look the record up by its identifier or by its name.
            var credential = client.GetCredentials(idOrName: byId ? sqlServer.Id : sqlServer.Name);

            // Assert: the stored record is returned as a SQL Server model with its plain password restored.
            var actual = credential as SqlServerCredentialModel;
            Assert.IsNotNull(value: actual);
            Assert.AreEqual(expected: sqlServer.Id, actual: actual.Id);
            Assert.AreEqual(expected: "localhost", actual: actual.Server);
            Assert.AreEqual(expected: "unit-test-password", actual: actual.Password);
        }

        [TestMethod(DisplayName = "Verify that creating OAuth credentials persists a pending record and returns the " +
            "provider consent URL with the normalized scope and domains.")]
        public void NewCredentialsReturnsConsentUrlTest()
        {
            // Arrange: describe a Google client without a refresh token, so the provider consent is still required.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);
            var oauth = NewOAuthCredential(provider: "google");

            // Act: start the OAuth flow; the consent URL is composed locally, so no provider call is made.
            var response = client.NewCredentials(oauth);

            // Assert: the response asks for consent through the Google endpoint and reports the normalized values.
            const string googleConsentEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            Assert.IsNotNull(value: response);
            Assert.IsTrue(condition: response.RequiresConsent);
            Assert.StartsWith(expectedPrefix: googleConsentEndpoint, value: response.ConsentUrl);
            Assert.Contains(substring: "client_id=unit-test-client", value: response.ConsentUrl);
            Assert.AreEqual(expected: "unit-test-scope-first unit-test-scope-second", actual: response.Scope);
            Assert.AreEqual(expected: "none", actual: response.Domains);

            // Assert: the pending record received a time-based identifier and was persisted in the store.
            var credential = client.GetCredentials(idOrName: oauth.Id) as OAuthCredentialModel;
            Assert.StartsWith(expectedPrefix: "cred_", value: oauth.Id);
            Assert.IsNotNull(value: credential);
            Assert.AreEqual(expected: oauth.Name, actual: credential.Name);
        }

        [TestMethod(DisplayName = "Verify that creating OAuth credentials for a provider without a consent URL " +
            "formatter fails after the pending record is persisted.")]
        public void NewCredentialsUnknownProviderTest()
        {
            // Arrange: describe a client of a provider the engine registers no consent URL formatter for.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);
            var oauth = NewOAuthCredential(provider: "unit-test-unknown-provider");

            // Act: start the OAuth flow and capture the rejection of the provider.
            var exception = Assert.ThrowsExactly<InvalidOperationException>(action: () => client.NewCredentials(oauth));

            // Assert: the failure names the provider, and the record was persisted before the consent URL failed.
            Assert.Contains(substring: "'unit-test-unknown-provider'", value: exception.Message);
            Assert.IsNotNull(value: client.GetCredentials(idOrName: oauth.Id));
        }

        [TestMethod(DisplayName = "Verify that removing a credential that matches no record removes nothing and " +
            "returns zero.")]
        public void RemoveCredentialsMissingRecordTest()
        {
            // Arrange: open a private store that holds no records.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);

            // Act: remove a value that matches no stored record.
            var removedCount = client.RemoveCredentials(idOrName: "unit-test-missing-credential");

            // Assert: nothing was removed.
            Assert.AreEqual(expected: 0, actual: removedCount);
        }

        [TestMethod(DisplayName = "Verify that removing a SQL Server record deletes it from the store and from the " +
            "SQL Server partition of the credentials cache.")]
        public void RemoveCredentialsRemovesSqlServerRecordTest()
        {
            // Arrange: persist one SQL Server record in a private store and cache it in the shared cache.
            using var connection = new SqliteConnection(InMemoryConnectionString);
            var client = NewCredentialsClient(connection);
            var sqlServer = NewSqlServerCredential();
            var key = $"{sqlServer.Name};{sqlServer.Id}".ToLowerInvariant();
            var partition = CacheManager.Instance.CredentialsCache[CredentialTypes.SqlServer];
            new CredentialsManager(sqliteConnection: connection).SaveSqlServerCredentials(sqlServer);
            partition[key] = sqlServer;

            try
            {
                // Act: remove the record by its name.
                var removedCount = client.RemoveCredentials(idOrName: sqlServer.Name);

                // Assert: one row left the store, the record no longer resolves, and its cache entry is gone.
                Assert.AreEqual(expected: 1, actual: removedCount);
                Assert.IsNull(value: client.GetCredentials(idOrName: sqlServer.Id));
                Assert.IsFalse(condition: partition.ContainsKey(key));
            }
            finally
            {
                // Cleanup: drop the cache entry when an assertion failed before the remove, so the shared cache stays
                // clean.
                partition.Remove(key);
            }
        }

        // Creates the API client's internal credentials client over the specified store connection; G4Client always
        // uses the shared connection of the cache manager.
        private static ICredentialsClient NewCredentialsClient(SqliteConnection connection)
        {
            // Locate the internal client type and construct it over the caller's connection.
            var clientType = typeof(G4Client).Assembly.GetType("G4.Api.Clients.CredentialsClient", throwOnError: true);

            return (ICredentialsClient)Activator.CreateInstance(clientType, connection);
        }

        // Creates an OAuth client description of the specified provider with a unique name and a comma-separated scope,
        // which the engine normalizes to a space-separated scope; the identifier is left empty for the engine to assign.
        private static OAuthCredentialModel NewOAuthCredential(string provider)
        {
            // Compose a unique name so pending records of parallel tests never share an identity.
            return new OAuthCredentialModel
            {
                ClientId = "unit-test-client",
                ClientSecret = "unit-test-secret",
                Name = $"unit-test-oauth-{Guid.NewGuid():N}",
                Provider = provider,
                RedirectUrl = "http://localhost/unit-test/callback",
                Scope = "unit-test-scope-first, unit-test-scope-second"
            };
        }

        // Creates a SQL Server record with a unique identity, so records of parallel tests never share a cache key.
        private static SqlServerCredentialModel NewSqlServerCredential()
        {
            // Compose the identity from one unique value and fill the members the store round trip is checked against.
            var unique = Guid.NewGuid().ToString(format: "N");

            return new SqlServerCredentialModel
            {
                Authentication = SqlServerAuthenticationTypes.SqlPassword,
                Database = "UnitTests",
                Id = $"cred_sql_{unique}",
                Name = $"unit-test-sql-{unique}",
                Password = "unit-test-password",
                Server = "localhost",
                Username = "unit-test-user"
            };
        }
    }
}
