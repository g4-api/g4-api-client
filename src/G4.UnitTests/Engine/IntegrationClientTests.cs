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
