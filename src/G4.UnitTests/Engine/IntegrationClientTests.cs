using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Engine")]
    [TestCategory("Integration")]
    [TestCategory("UnitTest")]
    public class IntegrationClientTests
    {
        // Plugin type of the fixture plugins; a private name keeps them apart from the plugins the cache discovers.
        private const string PluginType = "UnitTestMcpType";

        // Names of the two unreachable servers, in the order they are configured and reported.
        private static readonly string[] DeadServerNames = ["DeadFirst", "DeadSecond"];

        // Name of the tool the engine rejected in the synced-entries test.
        private static readonly string[] RejectedToolNames = ["RejectedTool"];

        [TestMethod(DisplayName = "Verify that a tool the engine rejects counts as still listed by its server, so the " +
            "stale-entry pass keeps its previously cached version.")]
        public void AddSyncedEntriesKeepsRejectedToolTest()
        {
            // Arrange: one plugin the server provided and one tool the engine rejected during conversion.
            var syncedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var provided = NewPlugin(key: "ProvidedPlugin", server: null);
            var addSyncedEntries = typeof(G4Client).Assembly
                .GetType("G4.Api.Clients.IntegrationClient")
                .GetMethod("AddSyncedEntries", BindingFlags.NonPublic | BindingFlags.Static);

            // Act: record what the synchronized server still lists, the way the MCP sync does.
            addSyncedEntries.Invoke(
                obj: null,
                parameters: [syncedEntries, new[] { provided }, RejectedToolNames]);

            // Assert: both are recorded, the rejected tool under the Action type that every MCP tool converts to.
            Assert.HasCount(2, syncedEntries);
            Assert.Contains($"{PluginType}/ProvidedPlugin", syncedEntries);
            Assert.Contains("Action/RejectedTool", syncedEntries);
        }

        [TestMethod(DisplayName = "Verify that a server's sync result reports both the tools the engine could not " +
            "convert and the plugins the cache refused.")]
        public void NewServerRejectionsCombinesToolAndCacheRejectionsTest()
        {
            // Arrange: one tool the engine could not convert and one converted plugin the cache refused.
            var rejectedTools = new Dictionary<string, string> { ["g4.Unreadable"] = "The tool could not be converted." };
            var cacheRejections = new Dictionary<string, string> { ["g4.Undocumented"] = "The plugin has no examples." };
            var newServerRejections = typeof(G4Client).Assembly
                .GetType("G4.Api.Clients.IntegrationClient")
                .GetMethod("NewServerRejections", BindingFlags.NonPublic | BindingFlags.Static);

            // Act: combine them the way the client builds a server's sync result.
            var rejections = (Dictionary<string, string>)newServerRejections.Invoke(
                obj: null,
                parameters: [rejectedTools, cacheRejections]);

            // Assert: both rejections are reported with their reasons, and lookups ignore case like plugin keys.
            Assert.HasCount(2, rejections);
            Assert.AreEqual("The tool could not be converted.", rejections["G4.UNREADABLE"]);
            Assert.AreEqual("The plugin has no examples.", rejections["g4.Undocumented"]);
        }

        [TestMethod(DisplayName = "Verify that a server's sync result reports no rejections when neither the engine " +
            "nor the cache rejected anything.")]
        public void NewServerRejectionsEmptyTest()
        {
            // Arrange: no rejections from either step, as for a fully synchronized server.
            var newServerRejections = typeof(G4Client).Assembly
                .GetType("G4.Api.Clients.IntegrationClient")
                .GetMethod("NewServerRejections", BindingFlags.NonPublic | BindingFlags.Static);

            // Act: combine two empty sets.
            var rejections = (Dictionary<string, string>)newServerRejections.Invoke(
                obj: null,
                parameters: [new Dictionary<string, string>(), new Dictionary<string, string>()]);

            // Assert: the result is an empty dictionary rather than null, so clients can always read it.
            Assert.IsNotNull(rejections);
            Assert.IsEmpty(rejections);
        }

        [TestMethod(DisplayName = "Verify that an unreachable MCP server is reported per server and does not fail the sync.")]
        public void SyncCacheReportsUnreachableServerTest()
        {
            // Arrange: two servers on closed local ports, so both connections are refused.
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            var servers = new Dictionary<string, McpServerModel>(StringComparer.OrdinalIgnoreCase)
            {
                ["DeadFirst"] = new() { Type = "http", Url = $"http://127.0.0.1:{GetClosedPort()}/mcp" },
                ["DeadSecond"] = new() { Type = "http", Url = $"http://127.0.0.1:{GetClosedPort()}/mcp" }
            };
            var stopwatch = Stopwatch.StartNew();

            // Act: sync both servers.
            var results = client.Integration.SyncCache(cacheManager, servers);
            stopwatch.Stop();

            // Assert: the sync returns normally, with one failed result per server that names the failure.
            Assert.HasCount(2, results);
            Assert.IsTrue(condition: results.All(i => !i.IsSynced && !string.IsNullOrWhiteSpace(i.Error)));
            Assert.AreSequenceEqual(expected: DeadServerNames, actual: results.Select(i => i.Name).ToArray());

            // Assert: the servers were queried at the same time, so the sync stayed within one time limit.
            Assert.IsLessThan(upperBound: TimeSpan.FromSeconds(20), value: stopwatch.Elapsed);
        }

        [TestMethod(DisplayName = "Verify that MCP plugins whose server is no longer configured are removed and others are kept.")]
        public void SyncCacheRemovesPluginsOfRemovedServerTest()
        {
            // Arrange: one cached plugin from a server that is no longer configured, one from a configured server that
            // will fail, and one that did not come from any server.
            var cacheManager = new CacheManager();
            var failingServer = new McpServerModel { Type = "http", Url = $"http://127.0.0.1:{GetClosedPort()}/mcp" };
            var bucket = new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase)
            {
                ["RemovedServerPlugin"] = NewPlugin(key: "RemovedServerPlugin", server: new McpServerModel { Url = "http://127.0.0.1:1/removed" }),
                ["FailingServerPlugin"] = NewPlugin(key: "FailingServerPlugin", server: failingServer),
                ["LocalPlugin"] = NewPlugin(key: "LocalPlugin", server: null)
            };
            cacheManager.PluginsCache[PluginType] = bucket;
            var client = new G4Client(cacheManager);
            var servers = new Dictionary<string, McpServerModel>(StringComparer.OrdinalIgnoreCase)
            {
                ["Failing"] = failingServer
            };

            // Act: sync with only the failing server configured.
            client.Integration.SyncCache(cacheManager, servers);

            // Assert: the removed server's plugin is gone; the failing server keeps its plugin; the local plugin stays.
            Assert.IsFalse(condition: bucket.ContainsKey(key: "RemovedServerPlugin"));
            Assert.IsTrue(condition: bucket.ContainsKey(key: "FailingServerPlugin"));
            Assert.IsTrue(condition: bucket.ContainsKey(key: "LocalPlugin"));
        }

        [TestMethod(DisplayName = "Verify that a plugin the engine rejects is reported with the reason.")]
        public void SyncCacheModelsReportsEngineRejectionTest()
        {
            // Arrange: a plugin without examples, which the engine skips because it cannot render its documentation.
            var cacheManager = new CacheManager();
            var rejected = NewPlugin(key: "EngineRejectedPlugin", server: null);
            var syncCacheModels = typeof(G4Client).Assembly
                .GetType("G4.Api.Clients.IntegrationClient")
                .GetMethod("SyncCacheModels", BindingFlags.NonPublic | BindingFlags.Static);

            // Act: send the plugin through the client's sync helper.
            var rejections = (Dictionary<string, string>)syncCacheModels.Invoke(
                obj: null,
                parameters: [cacheManager, "unit test", new[] { rejected }]);

            // Assert: the client reports the engine's rejection with a reason, and the cache does not hold the plugin.
            var isCached = cacheManager.PluginsCache.TryGetValue(PluginType, out var bucket)
                && bucket.ContainsKey("EngineRejectedPlugin");

            Assert.IsTrue(condition: rejections.TryGetValue("EngineRejectedPlugin", out var reason));
            Assert.IsFalse(condition: string.IsNullOrWhiteSpace(value: reason));
            Assert.IsFalse(condition: isCached);
        }

        // Finds a local port with nothing listening on it, so a connection to it is refused at once.
        private static int GetClosedPort()
        {
            // Let the operating system pick a free port, then release it.
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }

        // Builds a minimal cached plugin of the fixture type, optionally marked as coming from an MCP server.
        private static G4PluginCacheModel NewPlugin(string key, McpServerModel server)
        {
            return new G4PluginCacheModel
            {
                Manifest = new G4PluginAttribute
                {
                    Aliases = [],
                    Key = key,
                    PluginType = PluginType
                },
                ModelContextServer = server
            };
        }
    }
}
