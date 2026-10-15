using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Extensions;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("FlowsClient")]
    [TestCategory("UnitTest")]
    public class FlowsClientTests
    {
        // Qualifies all flows owned by this test class so they never collide with other stored flows.
        private const string FlowNamespace = "G4.UnitTests.Flows";

        // Holds a rule argument combining the characters that break naive storage: unicode, quotes, backslashes,
        // line breaks, a CLI parameter macro, and a JSON-shaped string.
        private const string NastyArgument =
            "{{$ --Name:Foo --Value:Bar}} ☃ \"quoted\" 'single' \\back\\slash\\ \r\n" +
            "tab\tline שלום 日本語 🚀 {\"nested\":{\"array\":[1,\"two\",null]}} <tag attr=\"x\">&amp;</tag>";

        // Reads decoded flow automations the same way the flows client does.
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Gets or sets the test context that supplies the licensed username used by flow automations.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify that adding a flow synchronizes persistence, cache, and events.")]
        public void AddFlowSynchronizesPersistenceAndCacheTest()
        {
            // Arrange: create an isolated cache and client, removing any flow left behind by an interrupted run.
            const string Key = "UnitTestFlowAddition";
            const string Alias = "UnitTestFlowAdditionAlias";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            CleanupFlow(client, Key);
            var notifications = new List<CacheManager.CacheChangedEventArgs>();
            cacheManager.CacheChanged += (_, eventArgs) => notifications.Add(eventArgs);

            // Act: register the flow and read it back from persistence and the cache.
            var statusCode = client.Flows.AddFlow(NewManifest(Key, TestContext, Alias));
            var (persistedStatusCode, _) = client.Flows.GetFlow(FlowNamespace, Key);
            var isCached = cacheManager.TryGetFlow(FlowNamespace, Key, out var cacheModel);
            var isAliasCached = cacheManager.TryGetFlow(FlowNamespace, Alias, out var aliasModel);

            // Assert: the flow is stored, cached under its key and alias, and announced once as a flow addition.
            Assert.AreEqual(204, statusCode);
            Assert.AreEqual(200, persistedStatusCode);
            Assert.IsTrue(isCached);
            Assert.IsTrue(isAliasCached);
            Assert.AreSame(cacheModel, aliasModel);
            Assert.HasCount(1, notifications);
            Assert.AreEqual(CacheManager.CacheChangeTypes.Added, notifications[0].ChangeType);
            Assert.AreEqual(G4FlowManifestModel.FlowType, notifications[0].EntityType);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that an alias conflict is rejected before flow persistence changes.")]
        public void AddFlowRejectsAliasConflictBeforePersistenceTest()
        {
            // Arrange: register a flow whose canonical key the incoming flow claims as an alias.
            const string OwnerKey = "UnitTestFlowAliasOwner";
            const string ConflictingKey = "UnitTestFlowAliasConflict";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            CleanupFlow(client, OwnerKey);
            CleanupFlow(client, ConflictingKey);
            client.Flows.AddFlow(NewManifest(OwnerKey, TestContext));

            // Act: attempt to register an alias that would replace the owner's canonical cache entry.
            _ = Assert.ThrowsExactly<InvalidOperationException>(() =>
                client.Flows.AddFlow(NewManifest(ConflictingKey, TestContext, OwnerKey)));

            // Assert: the rejected flow is neither stored nor cached, and the owner still resolves its key.
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, ConflictingKey).StatusCode);
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, ConflictingKey, out _));
            Assert.IsTrue(cacheManager.TryGetFlow(FlowNamespace, OwnerKey, out var ownerModel));
            Assert.AreEqual(OwnerKey, ownerModel.Manifest.Key);

            CleanupFlow(client, OwnerKey);
        }

        [TestMethod(DisplayName = "Verify that adding a flow without a manifest, key, or automation returns 400.")]
        public void AddFlowRejectsInvalidManifestTest()
        {
            // Arrange: prepare manifests that cannot form a stored, invocable flow.
            var client = new G4Client(new CacheManager());
            var keylessManifest = NewManifest(" ", TestContext);
            var automationlessManifest = NewManifest("UnitTestFlowWithoutAutomation", TestContext);
            automationlessManifest.Automation = default;

            // Act: submit each invalid manifest through the client.
            var nullStatus = client.Flows.AddFlow(default);
            var keylessStatus = client.Flows.AddFlow(keylessManifest);
            var automationlessStatus = client.Flows.AddFlow(automationlessManifest);

            // Assert: every invalid manifest is rejected with 400 and nothing is stored.
            Assert.AreEqual(400, nullStatus);
            Assert.AreEqual(400, keylessStatus);
            Assert.AreEqual(400, automationlessStatus);
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, "UnitTestFlowWithoutAutomation").StatusCode);
        }

        #region *** Data Set ***
        [DataRow("not base64 at all!")]
        [DataRow("{\"stages\":[]}")]
        [DataRow("WzEsMiwzXQ==")]
        [DataRow("bnVsbA==")]
        #endregion
        [TestMethod(DisplayName = "Verify that adding a flow whose automation is not a Base64-encoded automation returns 400.")]
        public void AddFlowRejectsInvalidEncodedAutomationTest(string encodedAutomation)
        {
            // Arrange: attach a body that is invalid Base64, raw unencoded JSON, encoded "[1,2,3]", or encoded "null".
            const string Key = "UnitTestFlowInvalidEncoding";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            CleanupFlow(client, Key);
            var manifest = NewManifest(Key, TestContext);
            manifest.Automation = encodedAutomation;

            // Act: submit the manifest through the client.
            var statusCode = client.Flows.AddFlow(manifest);

            // Assert: the flow is rejected at save time and reaches neither persistence nor cache.
            Assert.AreEqual(400, statusCode);
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, Key).StatusCode);
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, Key, out _));
        }

        [TestMethod(DisplayName = "Verify that clearing flows removes every stored flow from persistence and cache.")]
        public void ClearFlowsSynchronizesPersistenceAndCacheTest()
        {
            // Arrange: store two flows through one client and cache.
            const string FirstKey = "UnitTestFlowClearFirst";
            const string SecondKey = "UnitTestFlowClearSecond";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            client.Flows.AddFlow(NewManifest(FirstKey, TestContext));
            client.Flows.AddFlow(NewManifest(SecondKey, TestContext));

            // Act: clear every stored flow.
            client.Flows.ClearFlows();

            // Assert: neither flow remains stored or cached.
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, FirstKey).StatusCode);
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, SecondKey).StatusCode);
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, FirstKey, out _));
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, SecondKey, out _));
            Assert.IsEmpty(client.Flows.GetFlows(FlowNamespace));
        }

        [TestMethod(DisplayName = "Verify that invoking a cached flow runs its automation without mutating the cached flow.")]
        public void InvokeFlowRunsCachedAutomationTest()
        {
            // Arrange: store a one-rule simulator flow and capture its cached encoded automation.
            const string Key = "UnitTestFlowInvocation";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            CleanupFlow(client, Key);
            var manifest = NewManifest(Key, TestContext);
            var encodedAutomation = manifest.Automation;
            client.Flows.AddFlow(manifest);
            _ = cacheManager.TryGetFlow(FlowNamespace, Key, out var cacheModel);

            // Act: invoke the flow by namespace and key.
            var (statusCode, responses) = client.Flows.Invoke(FlowNamespace, Key);

            // Assert: the flow ran and produced session responses.
            Assert.AreEqual(200, statusCode);
            Assert.IsNotNull(responses);
            Assert.IsNotEmpty(responses);
            Assert.IsNotEmpty(responses.SelectMany(i => i.Value.Sessions));

            // Assert: the invocation decoded its own instance, so the cached encoded automation is unchanged.
            Assert.AreEqual(encodedAutomation, ((G4FlowManifestModel)cacheModel.Manifest).Automation);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that invoking a flow that is not cached returns 404 and no responses.")]
        public void InvokeUnknownFlowReturnsNotFoundTest()
        {
            // Arrange: create a client over a cache that holds no flow with the requested key.
            var client = new G4Client(new CacheManager());

            // Act: invoke an unknown flow.
            var (statusCode, responses) = client.Flows.Invoke(FlowNamespace, "UnitTestFlowMissing");

            // Assert: the template-style not-found contract is returned.
            Assert.AreEqual(404, statusCode);
            Assert.IsNull(responses);
        }

        [TestMethod(DisplayName = "Verify that flow parameters are replaced from the JSON object and the command-line " +
            "argument, the argument winning per name.")]
        public void InvokeFlowReplacesParametersFromJsonAndArgumentTest()
        {
            // Arrange: a flow whose rules read one parameter each.
            const string Key = "UnitTestFlowParameterSources";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            client.Flows.AddFlow(NewManifest(Key, NewAutomation(
                TestContext,
                "{{$ parameters.JsonOnly }}",
                "{{$ parameters.Both }}",
                "{{$ parameters.CliOnly }}",
                "{{$ parameters.Number }}",
                "{{$ parameters.Flag }}",
                "{{$ parameters.mixedCASE }}",
                "{{$ parameters.Dollar }}")));

            // Arrange: read the request the way the controller does, so JSON values arrive as JSON elements.
            var invokeModel = JsonSerializer.Deserialize<G4FlowInvokeModel>(
                "{\"parameters\":{\"JsonOnly\":\"json\",\"Both\":\"json-both\",\"Number\":3,\"Flag\":true," +
                "\"MixedCase\":\"any-case\",\"Dollar\":\"$0\"},\"argument\":\"{{$ --Both:cli-both --CliOnly:cli}}\"}",
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

            // Act: invoke the flow and capture the automation it ran.
            var (statusCode, automation) = InvokeAndCapture(client, Key, invokeModel);

            // Assert: each placeholder received its value; the argument won on 'Both'; '$0' stayed literal.
            Assert.AreEqual(200, statusCode);
            CollectionAssert.AreEqual(
                expected: new[] { "json", "cli-both", "cli", "3", "true", "any-case", "$0" },
                actual: GetArguments(automation));

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that declared defaults fill missing flow parameters, and that missing and " +
            "undeclared names are tolerated like template parameters.")]
        public void InvokeFlowAppliesDefaultsLenientlyTest()
        {
            // Arrange: declared parameters with and without defaults.
            const string Key = "UnitTestFlowParameterDefaults";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            var manifest = NewManifest(Key, NewAutomation(
                TestContext,
                "{{$ parameters.WithDefault }}",
                "{{$ parameters.Given }}",
                "{{$ parameters.NoDefault }}"));
            manifest.Parameters =
            [
                new PluginParameterModel { Name = "WithDefault", Default = "fallback" },
                new PluginParameterModel { Name = "Given", Default = "unused" },
                new PluginParameterModel { Name = "NoDefault", Mandatory = true }
            ];
            client.Flows.AddFlow(manifest);
            var invokeModel = new G4FlowInvokeModel
            {
                Parameters = new Dictionary<string, object> { ["Given"] = "given", ["Undeclared"] = "ignored" }
            };

            // Act: invoke without the mandatory parameter and with an undeclared one.
            var (statusCode, automation) = InvokeAndCapture(client, Key, invokeModel);

            // Assert: the flow ran; the default filled its gap, a given value beat its default, and a missing
            // parameter without a default stayed as its placeholder.
            Assert.AreEqual(200, statusCode);
            CollectionAssert.AreEqual(
                expected: new[] { "fallback", "given", "{{$ parameters.NoDefault }}" },
                actual: GetArguments(automation));

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that flow parameters are replaced in driver parameters and settings, not only in rules.")]
        public void InvokeFlowReplacesParametersOutsideRulesTest()
        {
            // Arrange: placeholders in the driver parameters and the settings of the flow.
            const string Key = "UnitTestFlowParameterScope";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            var flowAutomation = NewAutomation(TestContext, "static");
            flowAutomation.DriverParameters["custom"] = "{{$ parameters.Custom }}";
            flowAutomation.Settings.EnvironmentsSettings.DefaultEnvironment = "{{$ parameters.Environment }}";
            client.Flows.AddFlow(NewManifest(Key, flowAutomation));
            var invokeModel = new G4FlowInvokeModel
            {
                Argument = "{{$ --Custom:custom-value --Environment:SystemParameters}}"
            };

            // Act: invoke the flow and capture the automation it ran.
            var (statusCode, automation) = InvokeAndCapture(client, Key, invokeModel);

            // Assert: both placeholders outside the rules received their values.
            Assert.AreEqual(200, statusCode);
            Assert.AreEqual("custom-value", $"{automation.DriverParameters["custom"]}");
            Assert.AreEqual("SystemParameters", automation.Settings.EnvironmentsSettings.DefaultEnvironment);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that a template inside a flow resolves its own parameters, including one " +
            "passed from a flow parameter.")]
        public void InvokeFlowWithTemplateResolvesTemplateParametersTest()
        {
            // Arrange: a template whose internal rule reads its own 'Inner' parameter.
            const string Key = "UnitTestFlowWithTemplate";
            const string TemplateKey = "UnitTestFlowTemplate";
            var cacheManager = new CacheManager();
            cacheManager.PluginsCache["Template"] = new ConcurrentDictionary<string, G4PluginCacheModel>(StringComparer.OrdinalIgnoreCase)
            {
                [TemplateKey] = new G4PluginCacheModel
                {
                    Manifest = new G4PluginAttribute
                    {
                        Aliases = [],
                        Key = TemplateKey,
                        PluginType = "Action",
                        Source = "Template",
                        Rules = [new ActionRuleModel { PluginName = "NoAction", Argument = "{{$ parameters.Inner }}" }]
                    }
                }
            };
            var client = new G4Client(cacheManager);
            CleanupFlow(client, Key);

            // Arrange: the flow calls the template and passes a flow parameter as the template's 'Inner'.
            var flowAutomation = NewAutomation(TestContext, "unused");
            var flowRule = flowAutomation.Stages.First().Jobs.First().Rules.First();
            flowRule.PluginName = TemplateKey;
            flowRule.Argument = "{{$ --Inner:{{$ parameters.Outer }}}}";
            client.Flows.AddFlow(NewManifest(Key, flowAutomation));
            var invokeModel = new G4FlowInvokeModel
            {
                Parameters = new Dictionary<string, object> { ["Outer"] = "from-flow", ["Inner"] = "not-for-the-template" }
            };

            // Act: invoke the flow and capture the automation it ran.
            var (statusCode, automation) = InvokeAndCapture(client, Key, invokeModel);

            // Assert: the template's internal rule received the flow value through the template's own argument.
            Assert.AreEqual(200, statusCode);
            var templateRule = automation.Stages.First().Jobs.First().Rules.First();
            Assert.AreEqual("{{$ --Inner:from-flow}}", templateRule.Argument);
            Assert.AreEqual("from-flow", templateRule.Rules.First().Argument);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that the caller's token always wins and the caller's other values only fill the flow's gaps.")]
        public void InvokeFlowAppliesCallerOverridesTest()
        {
            // Arrange: a flow with its own token, username, driver, and default environment.
            const string Key = "UnitTestFlowOverrides";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            var flowAutomation = NewAutomation(TestContext, "static");
            flowAutomation.Authentication.Token = "flow-token";
            client.Flows.AddFlow(NewManifest(Key, flowAutomation));
            var callerSettings = new G4SettingsModel();
            callerSettings.EnvironmentsSettings.DefaultEnvironment = "CallerEnvironment";
            var invokeModel = new G4FlowInvokeModel
            {
                Authentication = new AuthenticationModel { Token = "caller-token", Username = "caller-user" },
                DriverParameters = new Dictionary<string, object> { ["driver"] = "ChromeDriver", ["extra"] = "added" },
                Settings = callerSettings
            };

            // Act: invoke the flow and capture the automation it ran.
            var (statusCode, automation) = InvokeAndCapture(client, Key, invokeModel);

            // Assert: the caller's token replaced the flow's; every other flow value was kept; gaps were filled.
            Assert.AreEqual(200, statusCode);
            Assert.AreEqual("caller-token", automation.Authentication.Token);
            Assert.AreEqual($"{TestContext.Properties["G4.Username"]}", automation.Authentication.Username);
            Assert.AreEqual("SimulatorDriver", $"{automation.DriverParameters["driver"]}");
            Assert.AreEqual("added", $"{automation.DriverParameters["extra"]}");
            Assert.AreEqual("SystemParameters", automation.Settings.EnvironmentsSettings.DefaultEnvironment);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that the caller's reference ID replaces the flow's automation ID, and that the flow's ID is kept without one.")]
        public void InvokeFlowAppliesCallerReferenceIdTest()
        {
            // Arrange: a flow that stores its own automation ID.
            const string Key = "UnitTestFlowReferenceId";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            var flowAutomation = NewAutomation(TestContext, "static");
            flowAutomation.Reference = new G4AutomationReferenceModel { Id = "flow-reference-id" };
            client.Flows.AddFlow(NewManifest(Key, flowAutomation));

            // Act: invoke once with a caller ID and once without one.
            var (callerStatus, callerAutomation) = InvokeAndCapture(client, Key, new G4FlowInvokeModel { ReferenceId = "caller-run-1" });
            var (flowStatus, flowIdAutomation) = InvokeAndCapture(new G4Client(new CacheManager()), Key, new G4FlowInvokeModel());

            // Assert: the caller's ID is the run's ID; without it, the flow's stored ID is used.
            Assert.AreEqual(200, callerStatus);
            Assert.AreEqual("caller-run-1", callerAutomation.Reference.Id);
            Assert.AreEqual(200, flowStatus);
            Assert.AreEqual("flow-reference-id", flowIdAutomation.Reference.Id);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that a value that breaks the flow's JSON returns 400, because values are inserted as they are.")]
        public void InvokeFlowWithJsonBreakingValueReturnsBadRequestTest()
        {
            // Arrange: a flow whose rule reads one parameter, and a value with a quote.
            const string Key = "UnitTestFlowBreakingValue";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            client.Flows.AddFlow(NewManifest(Key, NewAutomation(TestContext, "{{$ parameters.Quote }}")));
            var invokeModel = new G4FlowInvokeModel
            {
                Parameters = new Dictionary<string, object> { ["Quote"] = "a\"b" }
            };

            // Act: invoke the flow.
            var (statusCode, responses) = client.Flows.Invoke(FlowNamespace, Key, invokeModel);

            // Assert: the flow is rejected and does not run.
            Assert.AreEqual(400, statusCode);
            Assert.IsNull(responses);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that a stored flow is loaded into a new cache from persistence.")]
        public void PersistedFlowReloadsIntoNewCacheTest()
        {
            // Arrange: store a flow through one cache instance.
            const string Key = "UnitTestFlowReload";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            client.Flows.AddFlow(NewManifest(Key, TestContext));

            // Act: construct a new cache, whose full synchronization reads the LiteDB Flows collection.
            var reloadedCache = new CacheManager();
            var isCached = reloadedCache.TryGetFlow(FlowNamespace, Key, out var cacheModel);

            // Assert: the stored flow, including its automation body, survives the persistence round trip.
            Assert.IsTrue(isCached);
            var automation = ConvertToAutomation(((G4FlowManifestModel)cacheModel.Manifest).Automation);
            Assert.IsNotNull(automation);
            Assert.HasCount(1, automation.Stages);
            Assert.AreEqual("NoAction", automation.Stages.First().Jobs.First().Rules.First().PluginName);

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that a flow with special characters survives persistence, reload, and invocation unchanged.")]
        public void SpecialCharacterFlowSurvivesRoundTripTest()
        {
            // Arrange: store a flow whose rule argument combines the characters that break naive storage.
            const string Key = "UnitTestFlowSpecialCharacters";
            var client = new G4Client(new CacheManager());
            CleanupFlow(client, Key);
            var manifest = NewManifest(Key, TestContext);
            manifest.Automation = NewEncodedAutomation(TestContext, argument: NastyArgument);
            var encodedAutomation = manifest.Automation;
            var addStatus = client.Flows.AddFlow(manifest);

            // Act: reload the cache from persistence, then invoke the flow through a client over the reloaded cache.
            var reloadedCache = new CacheManager();
            _ = reloadedCache.TryGetFlow(FlowNamespace, Key, out var cacheModel);
            var reloadedEncoding = ((G4FlowManifestModel)cacheModel.Manifest).Automation;
            var (invokeStatus, responses) = new G4Client(reloadedCache).Flows.Invoke(FlowNamespace, Key);

            // Assert: storage returned the exact encoded body, and it decodes to the exact original argument.
            Assert.AreEqual(204, addStatus);
            Assert.AreEqual(encodedAutomation, reloadedEncoding);
            var rule = ConvertToAutomation(reloadedEncoding).Stages.First().Jobs.First().Rules.First();
            Assert.AreEqual(NastyArgument, rule.Argument);

            // Assert: the reloaded flow runs and produces session responses.
            Assert.AreEqual(200, invokeStatus);
            Assert.IsNotEmpty(responses.SelectMany(i => i.Value.Sessions));

            CleanupFlow(client, Key);
        }

        [TestMethod(DisplayName = "Verify that removing a flow synchronizes persistence and cache and reports a missing flow.")]
        public void RemoveFlowSynchronizesPersistenceAndCacheTest()
        {
            // Arrange: store a flow with an alias before observing its removal.
            const string Key = "UnitTestFlowRemoval";
            const string Alias = "UnitTestFlowRemovalAlias";
            var cacheManager = new CacheManager();
            var client = new G4Client(cacheManager);
            CleanupFlow(client, Key);
            client.Flows.AddFlow(NewManifest(Key, TestContext, Alias));

            // Act: remove the flow, then attempt to remove it again.
            var statusCode = client.Flows.RemoveFlow(FlowNamespace, Key);
            var repeatedStatusCode = client.Flows.RemoveFlow(FlowNamespace, Key);

            // Assert: the flow and its alias leave persistence and cache, and a repeated removal reports 404.
            Assert.AreEqual(204, statusCode);
            Assert.AreEqual(404, repeatedStatusCode);
            Assert.AreEqual(404, client.Flows.GetFlow(FlowNamespace, Key).StatusCode);
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, Key, out _));
            Assert.IsFalse(cacheManager.TryGetFlow(FlowNamespace, Alias, out _));
        }

        // Removes a test-owned stored flow when a previous interrupted test left it behind.
        private static void CleanupFlow(G4Client client, string key)
        {
            // RemoveFlow is idempotent for the cleanup contract because a missing flow returns 404 without mutation.
            _ = client.Flows.RemoveFlow(FlowNamespace, key);
        }

        // Decodes an encoded flow automation the way the flows client does, so assertions can inspect its content.
        private static G4AutomationModel ConvertToAutomation(string encodedAutomation)
        {
            // Reverse the Base64 encoding, then the System.Text.Json serialization.
            return JsonSerializer.Deserialize<G4AutomationModel>(encodedAutomation.ConvertFromBase64(), s_jsonOptions);
        }

        // Creates the Base64-encoded automation of a flow that runs one NoAction rule on the simulator driver.
        // The username comes from the test context because automation invocation requires a licensed user.
        private static string NewEncodedAutomation(TestContext testContext, string argument)
        {
            // Build a minimal complete automation: one stage, one job, and one rule that performs no action.
            var automation = new G4AutomationModel
            {
                Authentication = new AuthenticationModel
                {
                    Username = $"{testContext.Properties["G4.Username"]}"
                },
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                },
                Stages =
                [
                    new G4StageModel
                    {
                        Jobs =
                        [
                            new G4JobModel
                            {
                                Rules =
                                [
                                    new ActionRuleModel
                                    {
                                        Argument = argument,
                                        PluginName = "NoAction"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };

            // Encode the automation the way clients encode flows: System.Text.Json serialization, then Base64.
            return JsonSerializer.Serialize(automation).ConvertToBase64();
        }

        // Returns the arguments of the top-level rules of an automation, in order.
        private static string[] GetArguments(G4AutomationModel automation)
        {
            return [.. automation.Stages.First().Jobs.First().Rules.Select(rule => rule.Argument)];
        }

        // Invokes a flow and captures the automation it ran, as initialized just before invocation.
        private static (int StatusCode, G4AutomationModel Automation) InvokeAndCapture(
            G4Client client,
            string key,
            G4FlowInvokeModel invokeModel)
        {
            G4AutomationModel automation = default;
            client.Flows.AutomationRequestInitialized += (_, queueModel) => automation ??= queueModel.Status.Automation;

            var (statusCode, _) = client.Flows.Invoke(FlowNamespace, key, invokeModel);

            return (statusCode, automation);
        }

        // Creates a simulator automation with one NoAction rule per argument, for flows that read parameters.
        private static G4AutomationModel NewAutomation(TestContext testContext, params string[] arguments)
        {
            return new G4AutomationModel
            {
                Authentication = new AuthenticationModel
                {
                    Username = $"{testContext.Properties["G4.Username"]}"
                },
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                },
                Stages =
                [
                    new G4StageModel
                    {
                        Jobs =
                        [
                            new G4JobModel
                            {
                                Rules = [.. arguments.Select(argument => new ActionRuleModel
                                {
                                    Argument = argument,
                                    PluginName = "NoAction"
                                })]
                            }
                        ]
                    }
                ]
            };
        }

        // Creates a flow manifest owned by the test namespace for an automation, encoded the way clients encode flows.
        private static G4FlowManifestModel NewManifest(string key, G4AutomationModel automation)
        {
            return new G4FlowManifestModel
            {
                Automation = JsonSerializer.Serialize(automation).ConvertToBase64(),
                Key = key,
                Namespace = FlowNamespace,
                Summary = [$"Unit test flow {key}."]
            };
        }

        // Creates a flow manifest owned by the test namespace whose encoded automation runs one NoAction rule.
        private static G4FlowManifestModel NewManifest(string key, TestContext testContext, params string[] aliases)
        {
            return new G4FlowManifestModel
            {
                Aliases = aliases,
                Automation = NewEncodedAutomation(testContext, argument: string.Empty),
                Key = key,
                Namespace = FlowNamespace,
                Summary = [$"Unit test flow {key}."]
            };
        }
    }
}
