using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("TemplatesClient")]
    [TestCategory("UnitTest")]
    public class TemplatesClientTests
    {
        // Identifies the physical cache bucket used by template capabilities.
        private const string PluginType = "Action";

        // Qualifies all manifests owned by this test class.
        private const string PluginNamespace = "G4.UnitTests.Templates";

        [TestMethod(DisplayName = "Verify that adding a template synchronizes persistence, cache, events, and lexical retrieval.")]
        public void AddTemplateSynchronizesSupplyChainTest()
        {
            // Arrange: create the lexical projection before adding a uniquely identified template through the API client.
            const string Key = "UnitTestTemplateAddition";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            var retrievalManager = new LexicalRetrievalManager(cacheManager);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);
            var manifest = NewManifest(Key, "template addition phrase", "UnitTestTemplateAdditionAlias");

            // Act: register the template and query every supported representation of the completed mutation.
            var statusCode = client.Templates.AddTemplate(manifest);
            var persisted = client.Templates.GetTemplate(Key);
            var lexical = retrievalManager.FindExamples(Key, PluginNamespace, "template addition phrase", take: 3);

            // Assert: the one application cache is authoritative and its completed addition updates every consumer.
            Assert.AreEqual(204, statusCode);
            Assert.AreEqual(200, persisted.StatusCode);
            Assert.AreSame(cacheManager.PluginsCache[PluginType][Key], cacheManager.PluginsCache[PluginType]["UnitTestTemplateAdditionAlias"]);
            Assert.HasCount(1, lexical.Examples);
            Assert.HasCount(1, notifications);
            Assert.AreEqual(CacheManager.CacheChangeTypes.Added, notifications[0].ChangeType);

            CleanupTemplate(client, Key);
        }

        [TestMethod(DisplayName = "Verify that replacing a template synchronizes content and removes aliases retired by the replacement.")]
        public void UpdateTemplateRemovesStaleAliasesTest()
        {
            // Arrange: persist an initial stable identity with one backward-compatible alias.
            const string Key = "UnitTestTemplateUpdate";
            const string RetiredAlias = "UnitTestTemplateRetiredAlias";
            const string CurrentAlias = "UnitTestTemplateCurrentAlias";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            client.Templates.AddTemplate(NewManifest(Key, "amber glacier", RetiredAlias));
            var retrievalManager = new LexicalRetrievalManager(cacheManager);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            // Act: replace the same namespace-plus-key identity with new content and a different alias.
            var statusCode = client.Templates.AddTemplate(NewManifest(Key, "violet harbor", CurrentAlias));
            var retiredLexical = retrievalManager.FindTools(prompt: "amber glacier", take: 3);
            var currentLexical = retrievalManager.FindTools(prompt: "violet harbor", take: 3);

            // Assert: replacement retires old cache lookups and lexical content without duplicating the persisted identity.
            Assert.AreEqual(204, statusCode);
            Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(RetiredAlias));
            Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(CurrentAlias));
            Assert.IsEmpty(retiredLexical.Tools);
            Assert.HasCount(1, currentLexical.Tools);
            Assert.HasCount(1, client.Templates.GetTemplates().Where(i => i.Key.Equals(Key, StringComparison.OrdinalIgnoreCase)));
            Assert.HasCount(1, notifications);
            Assert.AreEqual(CacheManager.CacheChangeTypes.Updated, notifications[0].ChangeType);

            CleanupTemplate(client, Key);
        }

        [TestMethod(DisplayName = "Verify that removing a template synchronizes persistence, cache, events, aliases, and lexical retrieval.")]
        public void RemoveTemplateSynchronizesSupplyChainTest()
        {
            // Arrange: register and index one template before observing its removal through the same cache instance.
            const string Key = "UnitTestTemplateRemoval";
            const string Alias = "UnitTestTemplateRemovalAlias";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            client.Templates.AddTemplate(NewManifest(Key, "template removal phrase", Alias));
            var retrievalManager = new LexicalRetrievalManager(cacheManager);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            // Act: remove the persisted identity through the API client and query all derived state afterward.
            var statusCode = client.Templates.RemoveTemplate(Key);
            var persisted = client.Templates.GetTemplate(Key);
            var lexical = retrievalManager.FindExamples(Key, PluginNamespace, "template removal phrase", take: 3);

            // Assert: canonical, alias, persistent, and lexical entries disappear after one removal notification.
            Assert.AreEqual(204, statusCode);
            Assert.AreEqual(404, persisted.StatusCode);
            Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(Key));
            Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(Alias));
            Assert.IsEmpty(lexical.Examples);
            Assert.HasCount(1, notifications);
            Assert.AreEqual(CacheManager.CacheChangeTypes.Removed, notifications[0].ChangeType);
        }

        [TestMethod(DisplayName = "Verify that clearing templates removes every persisted template from cache and lexical retrieval.")]
        public void ClearTemplatesSynchronizesSupplyChainTest()
        {
            // Arrange: add two persisted templates beside one static action that clear must preserve.
            const string FirstKey = "UnitTestTemplateClearFirst";
            const string SecondKey = "UnitTestTemplateClearSecond";
            const string StaticKey = "UnitTestStaticAction";
            var cacheManager = NewCacheManager();
            var staticModel = NewCacheModel(StaticKey, "static action phrase");
            cacheManager.SyncCache(staticModel);
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, FirstKey);
            CleanupTemplate(client, SecondKey);
            client.Templates.AddTemplate(NewManifest(FirstKey, "first clear phrase"));
            client.Templates.AddTemplate(NewManifest(SecondKey, "second clear phrase"));
            var retrievalManager = new LexicalRetrievalManager(cacheManager);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            // Act: clear persisted templates, retaining unrelated cache capabilities in the same physical bucket.
            client.Templates.ClearTemplates();
            var firstLexical = retrievalManager.FindExamples(FirstKey, PluginNamespace, "first clear phrase", take: 3);
            var secondLexical = retrievalManager.FindExamples(SecondKey, PluginNamespace, "second clear phrase", take: 3);

            // Assert: only template-owned identities are removed from persistence, cache, and derived indexes.
            Assert.AreEqual(404, client.Templates.GetTemplate(FirstKey).StatusCode);
            Assert.AreEqual(404, client.Templates.GetTemplate(SecondKey).StatusCode);
            Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(FirstKey));
            Assert.IsFalse(cacheManager.PluginsCache[PluginType].ContainsKey(SecondKey));
            Assert.IsTrue(cacheManager.PluginsCache[PluginType].ContainsKey(StaticKey));
            Assert.IsEmpty(firstLexical.Examples);
            Assert.IsEmpty(secondLexical.Examples);
            Assert.HasCount(2, notifications);
            Assert.IsTrue(notifications.All(i => i.ChangeType == CacheManager.CacheChangeTypes.Removed));
        }

        [TestMethod(DisplayName = "Verify that a namespace collision is rejected before template persistence changes.")]
        public void NamespaceCollisionLeavesPersistenceAndCacheUnchangedTest()
        {
            // Arrange: reserve the physical key under another namespace without creating a persisted template.
            const string Key = "UnitTestTemplateNamespaceConflict";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            var existingModel = NewCacheModel(Key, "existing namespace phrase");
            existingModel.Manifest.Namespace = "G4.UnitTests.Existing";
            cacheManager.SyncCache(existingModel);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);
            var conflictingManifest = NewManifest(Key, "conflicting namespace phrase");

            // Act: attempt to persist another logical identity that cannot coexist in the flat Action bucket.
            _ = Assert.ThrowsExactly<InvalidOperationException>(() => client.Templates.AddTemplate(conflictingManifest));

            // Assert: preflight rejection leaves both persistence and the existing authoritative cache entry unchanged.
            Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
            Assert.AreSame(existingModel, cacheManager.PluginsCache[PluginType][Key]);
            Assert.IsEmpty(notifications);
        }

        [TestMethod(DisplayName = "Verify that a template key cannot overwrite another capability alias.")]
        public void CanonicalKeyMatchingAliasIsRejectedTest()
        {
            // Arrange: reserve the incoming template key as an alias owned by another canonical capability.
            const string Key = "UnitTestReservedAlias";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            var ownerModel = NewCacheModel("UnitTestAliasOwner", "alias owner phrase", Key);
            cacheManager.SyncCache(ownerModel);

            // Act: attempt to register a canonical template identity over the backward-compatible alias lookup.
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                client.Templates.AddTemplate(NewManifest(Key, "alias collision phrase")));

            // Assert: rejection preserves the alias owner and prevents an orphan persisted template.
            Assert.AreSame(ownerModel, cacheManager.PluginsCache[PluginType][Key]);
            Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
        }

        [TestMethod(DisplayName = "Verify that a template alias cannot overwrite another capability canonical key.")]
        public void AliasMatchingCanonicalKeyIsRejectedTest()
        {
            // Arrange: reserve one canonical lookup path before registering another template with that path as an alias.
            const string Key = "UnitTestTemplateAliasConflict";
            const string ReservedKey = "UnitTestReservedCanonical";
            var cacheManager = NewCacheManager();
            var client = new G4Client(cacheManager);
            CleanupTemplate(client, Key);
            var ownerModel = NewCacheModel(ReservedKey, "canonical owner phrase");
            cacheManager.SyncCache(ownerModel);

            // Act: attempt to register an alias that would replace the existing canonical cache entry.
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                client.Templates.AddTemplate(NewManifest(Key, "canonical collision phrase", ReservedKey)));

            // Assert: rejection leaves both the canonical owner and persistent template collection unchanged.
            Assert.AreSame(ownerModel, cacheManager.PluginsCache[PluginType][ReservedKey]);
            Assert.AreEqual(404, client.Templates.GetTemplate(Key).StatusCode);
        }

        // Removes a test-owned persisted template when a previous interrupted test left it behind.
        private static void CleanupTemplate(G4Client client, string key)
        {
            // RemoveTemplate is idempotent for the cleanup contract because a missing key returns 404 without mutation.
            _ = client.Templates.RemoveTemplate(key);
        }

        // Creates an isolated cache with an empty Action bucket so each test owns every relevant cache mutation.
        private static CacheManager NewCacheManager()
        {
            // Replace environment-derived actions while preserving the cache instance's event infrastructure.
            var cacheManager = new CacheManager();
            cacheManager.PluginsCache[PluginType] =
                new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase);
            return cacheManager;
        }

        // Creates a non-template cache model for collision and preservation scenarios.
        private static G4PluginCacheModel NewCacheModel(string key, string phrase, params string[] aliases)
        {
            // Reuse the manifest factory while omitting the TemplatePlugin type marker used by API-created entries.
            return new G4PluginCacheModel
            {
                Manifest = NewManifest(key, phrase, aliases)
            };
        }

        // Creates a valid template manifest with deterministic identity and lexical content.
        private static G4PluginAttribute NewManifest(string key, string phrase, params string[] aliases)
        {
            // Deserialize the proven template fixture so ConfirmTemplate receives a complete executable rule graph.
            var json = File.ReadAllText("Resources/LoginTemplate.txt");
            var manifest = JsonSerializer.Deserialize<G4PluginAttribute>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Replace only test-owned identity and retrieval fields, leaving the valid rule graph intact.
            manifest.Aliases = aliases;
            manifest.Description = [phrase];
            manifest.Examples.First().Description = [phrase];
            manifest.Key = key;
            manifest.Namespace = PluginNamespace;
            manifest.Summary = [phrase];
            return manifest;
        }
    }
}
