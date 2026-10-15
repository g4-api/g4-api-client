using G4.Abstraction.Cli;
using G4.Abstraction.Logging;
using G4.Api.Abstractions;
using G4.Api.Models;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Events;
using G4.Plugins;
using G4.Plugins.Engine;
using G4.WebDriver.Remote;

using LiteDB;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace G4.Api.Clients
{
    /// <summary>
    /// Represents a client responsible for managing and invoking G4™ Flows: complete automations persisted in LiteDB,
    /// cached by namespace and key, and invoked through the same pipeline as <see cref="AutomationClient"/>.
    /// </summary>
    /// <param name="cache">The cache manager that owns the flows cache used for registration and invocation.</param>
    /// <param name="logger">The logger instance for logging.</param>
    /// <remarks>
    /// Management mirrors <see cref="TemplatesClient"/>: persistence changes first and the flows cache follows, with cache
    /// conflicts checked before any write. Invocation mirrors <see cref="AutomationClient"/>, except that the automation
    /// is decoded from the flow's Base64 body in the flows cache instead of being supplied by the caller.
    /// </remarks>
    internal class FlowsClient(CacheManager cache, ILogger logger) : ClientBase, IFlowsClient
    {
        #region *** Constants    ***
        // The name of the collection used to store flows in the LiteDB database; CacheManager loads the same collection.
        private const string CollectionName = "Flows";

        // The namespace that identifies flows registered without an explicit namespace.
        private const string SystemNamespace = "G4.System";

        // Parses parameter values written in command-line form, with the same rules as a template's argument.
        private static readonly CliFactory s_cliFactory = new();

        // Tracks the cancellation source of every running flow invocation by automation ID so StopAutomation can cancel it.
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> s_cancellationTokenSources =
            new(StringComparer.OrdinalIgnoreCase);

        // Maps flow manifests to stored documents without LiteDB's default string clean-up (trimming whitespace and
        // turning empty strings into null), so Markdown lines in summaries and descriptions keep their indentation
        // and blank lines. Scoped to flows; other collections keep the shared database mapper.
        private static readonly BsonMapper s_documentMapper = new()
        {
            EmptyStringToNull = false,
            TrimWhitespace = false
        };

        // Deserializes decoded flow automations; case-insensitive reading accepts camelCase and PascalCase encodings.
        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        #endregion

        #region *** Fields       ***
        // Holds the authoritative cache whose flows are registered, removed, and invoked by this client.
        private readonly CacheManager _cache = cache;
        #endregion

        #region *** Events       ***
        /// <inheritdoc />
        public event EventHandler<AutomationCallbackEventArgs> AutomationCallback;

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationInvoked;

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationInvoking;

        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> AutomationRequestInitialized;

        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationStatusChanged;

        /// <inheritdoc />
        public event EventHandler<int> AutomationStopped;

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobInvoked;

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobInvoking;

        /// <inheritdoc />
        public event EventHandler<JobEventArgs> JobStatusChanged;

        /// <inheritdoc />
        public event EventHandler<LogEventArgs> LogCreated;

        /// <inheritdoc />
        public event EventHandler<LogEventArgs> LogCreating;

        /// <inheritdoc />
        public event EventHandler<(RuleEventArgs EventArguments, Exception Exception)> OnRuleError;

        /// <inheritdoc />
        public event EventHandler<(PluginBase Plugin, G4RuleModelBase Rule)> PluginCreated;

        /// <inheritdoc />
        public event EventHandler<RuleCallbackEventArgs> RuleCallback;

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleInvoked;

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleInvoking;

        /// <inheritdoc />
        public event EventHandler<RuleEventArgs> RuleStatusChanged;

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageInvoked;

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageInvoking;

        /// <inheritdoc />
        public event EventHandler<StageEventArgs> StageStatusChanged;
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public ILogger Logger { get; } = logger;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public int AddFlow(G4FlowManifestModel manifest)
        {
            // Reject manifests that cannot form a cache identity.
            var isInvalid = manifest == null || string.IsNullOrWhiteSpace(manifest.Key);

            if (isInvalid)
            {
                return 400;
            }

            // Reject an automation that does not decode, so a broken flow fails when saved rather than when invoked.
            if (ConvertToAutomation(manifest.Automation) == null)
            {
                return 400;
            }

            // Reject cache identity collisions before changing persistent flow state.
            AssertCacheRegistration(_cache, manifest);

            // Replace any stored flow with the same namespace and key so the collection keeps one document per identity.
            var (collection, documents) = GetDocuments(LiteDatabase, manifest.Namespace, manifest.Key);
            RemoveDocuments(collection, documents);

            // Insert the new or updated flow document through the whitespace-preserving mapper. The untyped insert does
            // not assign an identifier, so an empty one is assigned here, as the typed insert's GUID auto-id would.
            if (manifest.Id == Guid.Empty)
            {
                manifest.Id = Guid.NewGuid();
            }

            LiteDatabase
                .GetCollection(name: CollectionName, autoId: BsonAutoId.Guid)
                .Insert(s_documentMapper.ToDocument(manifest));

            // Update the in-memory flows cache with the persisted manifest.
            _cache.SyncFlows(manifest);

            // Return 204 No Content to indicate that the flow was added successfully.
            return 204;
        }

        /// <inheritdoc />
        public void ClearFlows()
        {
            // Retrieve every stored flow document before deleting them.
            var collection = LiteDatabase.GetCollection<G4FlowManifestModel>(name: CollectionName, autoId: BsonAutoId.Guid);
            var documents = collection
                .Find(i => i.Type.Equals(nameof(G4FlowManifestModel), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Delete all stored flows in one operation.
            RemoveDocuments(collection, documents);

            // Remove every persisted flow from the same authoritative cache instance after persistence succeeds.
            var identities = documents.DistinctBy(
                document => $"{GetNormalizedNamespace(document.Namespace)}.{document.Key}",
                StringComparer.OrdinalIgnoreCase);

            foreach (var document in identities)
            {
                RemoveCachedFlow(_cache, document);
            }
        }

        /// <inheritdoc />
        public (int StatusCode, G4FlowManifestModel Manifest) GetFlow(string @namespace, string key)
        {
            // Retrieve documents associated with the provided identity from the LiteDatabase.
            var documents = GetDocuments(LiteDatabase, @namespace, key).Documents;

            // Return 404 Not Found when no flow is stored; otherwise, return 200 OK with the first match.
            return documents.Length == 0
                ? (404, default)
                : (200, documents[0]);
        }

        /// <inheritdoc />
        public IEnumerable<G4FlowManifestModel> GetFlows()
        {
            // Retrieve every stored flow document through its type discriminator.
            return LiteDatabase
                .GetCollection<G4FlowManifestModel>(name: CollectionName)
                .Find(i => i.Type.Equals(nameof(G4FlowManifestModel), StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <inheritdoc />
        public IEnumerable<G4FlowManifestModel> GetFlows(string @namespace)
        {
            // Filter the stored flows by normalized namespace so omitted namespaces match G4.System.
            return GetFlows()
                .Where(i => TestSameNamespace(i.Namespace, @namespace))
                .ToArray();
        }

        /// <inheritdoc />
        public (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(string @namespace, string key)
        {
            // A flow invoked without a request runs as stored, apart from the defaults of its declared parameters.
            return Invoke(@namespace, key, invokeModel: new G4FlowInvokeModel());
        }

        /// <inheritdoc />
        public (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel)
        {
            // Resolve the flow from the authoritative cache; report 404 Not Found when it is not cached.
            if (!_cache.TryGetFlow(@namespace, key, out var cacheModel))
            {
                return (404, default);
            }

            // Build a fresh automation instance from the cached flow, the parameter values, and the caller's overrides,
            // so initialization and invocation never touch the cached flow.
            var manifest = (G4FlowManifestModel)cacheModel.Manifest;
            var automation = NewAutomation(manifest, invokeModel ?? new G4FlowInvokeModel());

            // Report 400 Bad Request when the flow cannot be decoded, or is no longer valid JSON after its
            // parameters were applied (values are inserted as they are, like template values).
            if (automation == null)
            {
                Logger.LogError(
                    message: "Flow '{Namespace}.{Key}' could not be decoded into an automation after its parameters were applied.",
                    GetNormalizedNamespace(manifest.Namespace),
                    manifest.Key);
                return (400, default);
            }

            // Generate new automation queue models from the decoded automation model.
            var automations = automation.NewAutomations();

            // Retrieve the maximum degree of parallelism from the automation settings.
            var maxParallel = automation.GetMaxParallel();

            // Create automation queue models and register status events.
            var automationQueueModels = NewAutomationRequests(client: this, automations, registerStatusEvents: true);

            // Invoke automation tasks using the created queue models and the specified maximum degree of parallelism.
            var responses = Invoke(client: this, automationQueueModels, maxParallel);

            // Return 200 OK with the collected automation responses.
            return (200, responses);
        }

        /// <inheritdoc />
        public int RemoveFlow(string @namespace, string key)
        {
            // Retrieve the stored documents that match the requested identity.
            var (collection, documents) = GetDocuments(LiteDatabase, @namespace, key);

            // Return 404 Not Found when no stored flow matches.
            if (documents.Length == 0)
            {
                return 404;
            }

            // Delete all matching documents from the collection.
            RemoveDocuments(collection, documents);

            // Remove the persisted flow identity through the event-producing cache mutation path.
            RemoveCachedFlow(_cache, documents[0]);

            // Return a 204 No Content status code to indicate successful removal.
            return 204;
        }

        /// <inheritdoc />
        public int StopAutomation(string automationId)
        {
            // Initialize the exit code to 0, indicating success by default.
            // This will be updated to 1 if any errors occur during the stop process.
            var exitCode = 0;

            try
            {
                // Attempt to retrieve the CancellationTokenSource associated with the automation ID
                var tokenSource = s_cancellationTokenSources.GetValueOrDefault(automationId);

                // If a CTS is found, proceed to cancel the automation tasks
                if (tokenSource != default)
                {
                    // Request cooperative cancellation for the running automation tasks
                    tokenSource.Cancel();

                    // Remove the CTS from the tracking dictionary
                    // to prevent memory leaks and duplicate cancellation attempts
                    s_cancellationTokenSources.TryRemove(automationId, out _);

                    // Dispose the CTS after cancellation to release resources
                    tokenSource.Dispose();
                }

                // Locate all automation sessions whose reference ID matches
                // the provided automationId (case-insensitive comparison)
                var driversSessions = AutomationInvoker
                    .Automations
                    .Where(i =>
                        i.Value?.Reference?.Id?.Equals(
                            value: automationId,
                            comparisonType: StringComparison.OrdinalIgnoreCase
                        ) == true
                    )
                    // Extract only the session keys (used to access Drivers)
                    .Select(i => i.Key);

                // Iterate over each matched driver session
                foreach (var session in driversSessions)
                {
                    // Attempt to retrieve the WebDriver instance for the session
                    if (AutomationInvoker.Drivers.TryRemove(session, out IWebDriver driver))
                    {
                        // Close the browser window if still open
                        driver?.Close();

                        // Release all unmanaged resources held by the driver
                        driver?.Dispose();
                    }
                }
            }
            catch
            {
                // Any failure (lookup, driver close, dispose, etc.)
                // results in a non-zero exit code
                exitCode = 1;
            }
            finally
            {
                // Notify subscribers of the completed stop operation and its exit code.
                AutomationStopped?.Invoke(sender: this, e: exitCode);
            }

            // Return the exit code indicating the result of the stop operation
            return exitCode;
        }

        // Validates the flows-cache registration constraints before LiteDB state is replaced.
        // This preflight prevents a rejected cache mutation from leaving persistence ahead of the in-memory database.
        private static void AssertCacheRegistration(CacheManager cache, G4FlowManifestModel manifest)
        {
            // An absent namespace bucket cannot contain a key or alias collision.
            var isNamespaceBucket = cache.FlowsCache.TryGetValue(GetNormalizedNamespace(manifest.Namespace), out var flows);

            if (!isNamespaceBucket)
            {
                return;
            }

            // Reject an incoming canonical key that currently resolves another flow through an alias.
            var isKeyLookup = flows.TryGetValue(manifest.Key, out var keyModel);
            var isCanonicalLookup = isKeyLookup && keyModel.Manifest.Key.Equals(
                manifest.Key,
                StringComparison.OrdinalIgnoreCase);

            if (isKeyLookup && !isCanonicalLookup)
            {
                var message = $"Flow key '{manifest.Key}' conflicts with an alias owned by " +
                    $"'{keyModel.Manifest.Key}'.";
                throw new InvalidOperationException(message);
            }

            // Reject aliases that would replace another flow's canonical cache entry.
            foreach (var alias in manifest.Aliases ?? [])
            {
                var isAliasLookup = flows.TryGetValue(alias, out var aliasModel);
                var isOtherCanonical = isAliasLookup &&
                    !ReferenceEquals(aliasModel, keyModel) &&
                    aliasModel.Manifest.Key.Equals(alias, StringComparison.OrdinalIgnoreCase);

                if (isOtherCanonical)
                {
                    var message = $"Flow alias '{alias}' conflicts with the canonical key owned by " +
                        $"'{aliasModel.Manifest.Key}'.";
                    throw new InvalidOperationException(message);
                }
            }
        }

        // Decodes a flow's Base64 automation into a new automation instance, or returns null when it cannot be decoded.
        // The Base64 extension returns its input unchanged on failure, so an unchanged value marks a decode failure;
        // a real decode always differs, because JSON begins with '{', which is not a Base64 character. Each call
        // returns a new instance, which is why invocation needs no separate deep copy.
        private static G4AutomationModel ConvertToAutomation(string encodedAutomation)
        {
            // Decode the Base64 value to the UTF-8 JSON produced by System.Text.Json serialization.
            var json = ConvertToJson(encodedAutomation);

            if (json == null)
            {
                return default;
            }

            try
            {
                // Rebuild the automation exactly as the API deserializes a posted automation.
                return JsonSerializer.Deserialize<G4AutomationModel>(json, s_jsonOptions);
            }
            catch (JsonException)
            {
                // Treat JSON that does not describe an automation as undecodable.
                return default;
            }
        }

        // Decodes a flow's Base64 automation into its JSON text, or returns null when it cannot be decoded.
        // The Base64 extension returns its input unchanged on failure, so an unchanged value marks a decode failure.
        private static string ConvertToJson(string encodedAutomation)
        {
            // Reject a missing flow body before decoding.
            if (string.IsNullOrWhiteSpace(encodedAutomation))
            {
                return default;
            }

            // Decode the Base64 value; an unchanged value means it was not Base64.
            var json = encodedAutomation.ConvertFromBase64();

            return json == encodedAutomation ? default : json;
        }

        // Converts a parameter value to the text that replaces its placeholder: strings as they are, numbers and
        // booleans as their JSON text, objects and arrays as their JSON, and null as an empty string.
        private static string ConvertToText(object value) => value switch
        {
            null => string.Empty,
            string text => text,
            bool flag => flag ? "true" : "false",
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => element.GetRawText()
            },
            IFormattable formattable => formattable.ToString(format: null, CultureInfo.InvariantCulture),
            _ => JsonSerializer.Serialize(value)
        };

        // Fills the members a target object leaves out (missing or null) from a source object, recursively; target
        // values always win. Member names are matched ignoring case, because flows may be encoded in either casing.
        private static void FillGaps(JsonObject target, JsonObject source)
        {
            foreach (var (name, value) in source.ToList())
            {
                // A null source value has nothing to add.
                if (value == null)
                {
                    continue;
                }

                // Find the target member regardless of casing, so the flow keeps its own member name.
                var targetName = target
                    .Select(member => member.Key)
                    .FirstOrDefault(member => member.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (targetName == null || target[targetName] == null)
                {
                    target[targetName ?? name] = value.DeepClone();
                }
                else if (target[targetName] is JsonObject targetObject && value is JsonObject sourceObject)
                {
                    FillGaps(targetObject, sourceObject);
                }
            }
        }

        // Retrieves the flows collection and the stored documents matching one namespace and key.
        // Namespace matching happens in memory because omitted namespaces normalize to G4.System.
        private static (ILiteCollection<G4FlowManifestModel> Collection, G4FlowManifestModel[] Documents) GetDocuments(
            ILiteDatabase liteDatabase,
            string @namespace,
            string key)
        {
            // Retrieve the flows collection with GUID automatic IDs so inserted documents receive their identifier.
            var collection = liteDatabase.GetCollection<G4FlowManifestModel>(
                name: CollectionName,
                autoId: BsonAutoId.Guid);

            // A missing key cannot identify a stored flow.
            if (string.IsNullOrEmpty(key))
            {
                return (collection, []);
            }

            // Search by type and key in storage, then narrow to the normalized namespace in memory.
            var documents = collection
                .Find(i => i.Type.Equals(nameof(G4FlowManifestModel), StringComparison.OrdinalIgnoreCase) &&
                    i.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .Where(i => TestSameNamespace(i.Namespace, @namespace))
                .ToArray();

            // Return the collection and documents as a tuple.
            return (collection, documents);
        }

        // Collects the parameter values of one invocation: the JSON object first, then the command-line argument, whose
        // values replace those of the same name, then the default of every declared parameter that still has no value.
        // Names are matched ignoring case. Undeclared names are kept (lenient, like templates).
        private static Dictionary<string, string> GetParameterValues(G4FlowManifestModel manifest, G4FlowInvokeModel invokeModel)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (name, value) in invokeModel.Parameters ?? new Dictionary<string, object>())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    values[name] = ConvertToText(value);
                }
            }

            foreach (var (name, value) in s_cliFactory.ConvertToDictionary(invokeModel.Argument, normalize: false))
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    values[name] = value;
                }
            }

            foreach (var parameter in manifest.Parameters ?? [])
            {
                var isDefaultUsed = !string.IsNullOrWhiteSpace(parameter?.Name)
                    && parameter.Default != null
                    && !values.ContainsKey(parameter.Name);

                if (isDefaultUsed)
                {
                    values[parameter.Name] = parameter.Default;
                }
            }

            return values;
        }

        // Normalizes an omitted namespace to the system identity used by CacheManager and lexical retrieval.
        private static string GetNormalizedNamespace(string @namespace)
        {
            // Preserve explicit namespaces while assigning omitted flows to G4.System.
            return string.IsNullOrEmpty(@namespace)
                ? SystemNamespace
                : @namespace;
        }

        // Invokes automation tasks based on the provided array of queue models with a specified maximum degree of parallelism.
        private static IDictionary<string, G4AutomationResponseModel> Invoke(
            FlowsClient client,
            IEnumerable<AutomationQueueModel> automationQueueModels,
            int maxParallel)
        {
            // CancellationTokenSource to allow cancellation of ongoing operations.
            using var cancellationTokenSource = new CancellationTokenSource();

            // Set up parallel options with the cancellation token and maximum parallelism limit.
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = cancellationTokenSource.Token,
                MaxDegreeOfParallelism = maxParallel
            };

            // Collection to store responses as tuples of (GroupId, Response).
            var responseCollection = new ConcurrentBag<(string GroupId, G4AutomationResponseModel Response)>();

            // Collection to hold tasks for parallel processing of each automation queue model.
            var tasks = new ConcurrentBag<Task>();

            // Create a task for each automation queue model to process its automation invocation in parallel.
            foreach (var queueModel in automationQueueModels)
            {
                // Create a linked CancellationTokenSource for each task to allow individual cancellation if needed.
                var tokenSource = new CancellationTokenSource();

                // Safely resolve the automation ID (fallback = invalid marker)
                var automationId = queueModel.Status.Automation?.Reference?.Id ?? "-1";

                // Track only valid automation IDs
                if (automationId != "-1")
                {
                    // Store the CTS to allow later cancellation via automationId
                    s_cancellationTokenSources.TryAdd(automationId, tokenSource);
                }

                // Create a new task to process the automation invocation for the current queue model.
                tasks.Add(new Task(
                    action: () =>
                    {
                        // Prepare rule callback arguments including cancellation token, client reference, and current queue.
                        var ruleCallbackArgs = new RuleCallbackEventArgs
                        {
                            CancellationTokenSource = cancellationTokenSource,
                            FlowsClient = client,
                            Queue = automationQueueModels
                        };

                        // Subscribe to the RuleInvoked event to trigger the rule callback.
                        queueModel.Invoker.RuleInvoked += (_, args) =>
                        {
                            // Update callback arguments with event data.
                            ruleCallbackArgs.RuleEventArgs = args;

                            // Trigger the rule callback event on the client.
                            client.RuleCallback?.Invoke(sender: client, e: ruleCallbackArgs);

                            // Check for cancellation and throw if cancellation is requested.
                            ruleCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                        };

                        // Prepare automation callback arguments including cancellation token, client reference, and current queue.
                        var automationCallbackArgs = new AutomationCallbackEventArgs
                        {
                            CancellationTokenSource = cancellationTokenSource,
                            FlowsClient = client,
                            Queue = automationQueueModels
                        };

                        // Subscribe to the AutomationInvoked event to trigger the automation callback.
                        queueModel.Invoker.AutomationInvoked += (_, args) =>
                        {
                            // Create a new response based on the event response and the queue model.
                            var response = queueModel.NewResponse(args.Response);

                            // Add the response to the collection for later processing.
                            responseCollection.Add(response);

                            // Update the automation callback arguments with event data.
                            automationCallbackArgs.AutomationEventArgs = args;

                            // Trigger the automation callback event on the client.
                            client.AutomationCallback?.Invoke(sender: client, e: automationCallbackArgs);

                            // Check for cancellation and throw if cancellation is requested.
                            automationCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                        };

                        // Invoke automation processing for the current queue model.
                        queueModel.Invoker.Invoke();
                    },
                    cancellationToken: tokenSource.Token,
                    creationOptions: TaskCreationOptions.LongRunning));
            }

            // Process all tasks in parallel until they complete or cancellation is requested.
            try
            {
                Parallel.ForEach(tasks, parallelOptions, task =>
                {
                    task.Start();
                    task.Wait();
                });
            }
            // Log information if the process is canceled.
            catch (OperationCanceledException e)
            {
                client.Logger.LogInformation(exception: e, "The flow process was canceled by the user.");
            }
            // Log any other errors that occur during invocation.
            catch (Exception e)
            {
                client.Logger.LogError(exception: e, "An error occurred while invoking the flow process.");
            }

            // Convert the collected responses into a dictionary and return the result.
            return responseCollection.NewAutomationResponse();
        }

        // Builds the automation of one invocation from the cached flow, or returns null when it cannot be built:
        // decode the flow, replace its {{$ parameters.Name }} placeholders, fill its gaps from the caller's
        // authentication, driver parameters, and settings, then apply the caller's token and automation ID, which always win.
        // Each call returns a new instance, so the cached flow is never changed.
        private static G4AutomationModel NewAutomation(G4FlowManifestModel manifest, G4FlowInvokeModel invokeModel)
        {
            // Decode the stored flow into its JSON text.
            var json = ConvertToJson(manifest.Automation);

            if (json == null)
            {
                return default;
            }

            // Replace the parameter placeholders anywhere in the flow; values are inserted as they are.
            json = ResolveParameters(json, values: GetParameterValues(manifest, invokeModel));

            try
            {
                // A value with a quote or a backslash can leave the flow without valid JSON.
                if (JsonNode.Parse(json) is not JsonObject automationNode)
                {
                    return default;
                }

                // The caller's values only fill what the flow leaves out; they are not searched for placeholders.
                FillGaps(automationNode, source: new JsonObject
                {
                    [nameof(G4AutomationModel.Authentication)] = JsonSerializer.SerializeToNode(invokeModel.Authentication),
                    [nameof(G4AutomationModel.DriverParameters)] = JsonSerializer.SerializeToNode(invokeModel.DriverParameters),
                    [nameof(G4AutomationModel.Settings)] = JsonSerializer.SerializeToNode(invokeModel.Settings)
                });

                var automation = automationNode.Deserialize<G4AutomationModel>(s_jsonOptions);

                if (automation == null)
                {
                    return default;
                }

                // The caller's token always wins.
                var token = invokeModel.Authentication?.Token;

                if (!string.IsNullOrWhiteSpace(token))
                {
                    automation.Authentication ??= new AuthenticationModel();
                    automation.Authentication.Token = token;
                }

                // The caller's automation ID replaces the flow's, so the caller can stop this run by it.
                if (!string.IsNullOrWhiteSpace(invokeModel.ReferenceId))
                {
                    automation.Reference ??= new G4AutomationReferenceModel();
                    automation.Reference.Id = invokeModel.ReferenceId;
                }

                return automation;
            }
            catch (JsonException)
            {
                // Treat a flow that is not valid JSON, or does not describe an automation, as undecodable.
                return default;
            }
        }

        // Creates a list of new automation queue models based on the provided client and automations.
        // Each automation is initialized against the client's own cache, which is the cache the flow was read from.
        private static ConcurrentBag<AutomationQueueModel> NewAutomationRequests(
            FlowsClient client,
            IEnumerable<(G4AutomationModel Automation, IDictionary<string, object> DataProvider)> automations,
            bool registerStatusEvents)
        {
            // Initialize a list to store the resulting automation queue models
            var queueModels = new ConcurrentBag<AutomationQueueModel>();

            // Process each automation and its corresponding data provider
            for (int i = 0; i < automations.Count(); i++)
            {
                // Deconstruct the tuple into separate variables for the automation and data provider
                var (automation, dataProvider) = automations.ElementAt(i);

                // Create a new queue model from the automation model and data provider
                var status = automation.NewQueueModel(properties: dataProvider);

                // Initialize the automation using the client's cache manager
                status.Automation.Initialize(client._cache, dataProvider);

                // Set the iteration number for the automation
                status.Automation.Iteration = i;
                status.Automation.Reference.Iteration = i;

                // Create a new automation invoker based on the queue model's automation
                var invoker = new AutomationInvoker(status.Automation);

                // Create a new automation queue model with the invoker and status
                var queueModel = new AutomationQueueModel(invoker, status);

                // Set up new invoker events using the client and the newly created queue model
                if (registerStatusEvents)
                {
                    RegisterInvokerEvents(client, automation, invoker);
                }

                // Invoke the AutomationRequestInitialized event on the client
                client.AutomationRequestInitialized?.Invoke(sender: client, e: queueModel);

                // Add the queue model to the list of results
                queueModels.Add(queueModel);
            }

            // Return the list of new automation queue models
            return queueModels;
        }

        // Registers event handlers for the invoker by wrapping the client's event handlers with exception handling and logging.
        private static void RegisterInvokerEvents(FlowsClient client, G4AutomationModel automation, AutomationInvoker invoker)
        {
            // Get the logger for the client and the G4Logger type
            var logger = client.Logger.FindLogger<G4Logger>();

            // Register the client's event handlers with the invoker, wrapping them with the NewDelegate for exception handling
            invoker.AutomationInvoked += NewDelegate(client, client.AutomationInvoked);
            invoker.AutomationInvoking += NewDelegate(client, client.AutomationInvoking);
            invoker.AutomationStatusChanged += NewDelegate(client, client.AutomationStatusChanged);
            invoker.JobInvoked += NewDelegate(client, client.JobInvoked);
            invoker.JobInvoking += NewDelegate(client, client.JobInvoking);
            invoker.JobStatusChanged += NewDelegate(client, client.JobStatusChanged);
            invoker.OnRuleError += NewDelegate(client, client.OnRuleError);
            invoker.PluginCreated += NewDelegate(client, client.PluginCreated);
            invoker.RuleInvoked += NewDelegate(client, client.RuleInvoked);
            invoker.RuleInvoking += NewDelegate(client, client.RuleInvoking);
            invoker.RuleStatusChanged += NewDelegate(client, client.RuleStatusChanged);
            invoker.StageInvoked += NewDelegate(client, client.StageInvoked);
            invoker.StageInvoking += NewDelegate(client, client.StageInvoking);
            invoker.StageStatusChanged += NewDelegate(client, client.StageStatusChanged);

            // Forward created log entries to the client with the automation and invoker context.
            logger.LogCreated += (sender, args) =>
            {
                try
                {
                    // Safely invoke the event handler if it's not null
                    client.LogCreated?.Invoke(sender, e: new LogEventArgs
                    {
                        Automation = automation,
                        Invoker = invoker.Reference,
                        LogMessage = args
                    });
                }
                catch (Exception e)
                {
                    // Log the error if an exception occurs during event invocation
                    client.Logger.LogError(
                        exception: e,
                        message: "Error in event handler. Sender: {Sender}, EventArgs: {EventArgs}",
                        sender?.GetType().FullName, typeof(object).Name);
                }
            };

            // Forward log entries about to be created to the client with the automation and invoker context.
            logger.LogCreating += (sender, args) =>
            {
                try
                {
                    // Safely invoke the event handler if it's not null
                    client.LogCreating?.Invoke(sender, e: new LogEventArgs
                    {
                        Automation = automation,
                        Invoker = invoker.Reference,
                        LogMessage = args
                    });
                }
                catch (Exception e)
                {
                    // Log the error if an exception occurs during event invocation
                    client.Logger.LogError(
                        exception: e,
                        message: "Error in event handler. Sender: {Sender}, EventArgs: {EventArgs}",
                        sender?.GetType().FullName, typeof(object).Name);
                }
            };

            return;

            // Creates a new delegate that wraps an event handler with exception handling and logging.
            // A failing subscriber is logged and never interrupts the running flow.
            static EventHandler<T> NewDelegate<T>(FlowsClient client, EventHandler<T> eventHandler)
            {
                // Return a new delegate that wraps the event handler
                return (sender, args) =>
                {
                    try
                    {
                        // Safely invoke the event handler if it's not null
                        eventHandler?.Invoke(sender, e: args);
                    }
                    catch (Exception e)
                    {
                        // Log the error if an exception occurs during event invocation
                        client.Logger.LogError(
                            exception: e,
                            message: "Error in event handler. Sender: {Sender}, EventArgs: {EventArgs}",
                            sender?.GetType().FullName, typeof(T).Name);
                    }
                };
            }
        }

        // Removes one persisted flow from the cache only when the cached entry still represents that flow identity.
        // A key that now resolves another flow through an alias is left untouched; removals publish through CacheManager.
        private static void RemoveCachedFlow(CacheManager cache, G4FlowManifestModel manifest)
        {
            // Stop when the namespace bucket or key is already absent from the authoritative cache.
            var isCached = cache.TryGetFlow(manifest.Namespace, manifest.Key, out var cacheModel);

            if (!isCached)
            {
                return;
            }

            // Preserve an entry reached through an alias, because it belongs to a different canonical flow.
            var isSameIdentity = cacheModel.Manifest.Key.Equals(manifest.Key, StringComparison.OrdinalIgnoreCase);

            if (!isSameIdentity)
            {
                return;
            }

            // Remove the canonical flow and every owned alias through the event-producing operation.
            cache.RemoveFlow(manifest.Namespace, manifest.Key);
        }

        // Deletes the specified stored flow documents by their identifiers; an empty set leaves the collection unchanged.
        private static void RemoveDocuments(ILiteCollection<G4FlowManifestModel> collection, G4FlowManifestModel[] documents)
        {
            // Skip the delete operation when there is nothing to remove.
            if (documents.Length == 0)
            {
                return;
            }

            // Select the Ids of the documents to be removed and convert them to BsonValue.
            var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

            // Delete all documents with the selected Ids from the collection.
            collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));
        }

        // Replaces every {{$ parameters.Name }} placeholder in a flow's JSON with the value of that name, using the
        // template placeholder pattern. Names are escaped, so they match only themselves; values are inserted as they
        // are, like template values. A placeholder without a value stays as it is.
        private static string ResolveParameters(string json, IDictionary<string, string> values)
        {
            foreach (var (name, value) in values)
            {
                var pattern = @"(?si){{2}[$]\s{1}(parameters)[.]" + Regex.Escape(name) + @"\s{1}}}";

                // The evaluator inserts the value literally ('$' in a value is not a substitution).
                json = Regex.Replace(json, pattern, evaluator: _ => value);
            }

            return json;
        }

        // Tests namespace equality after normalizing omitted values to G4.System.
        private static bool TestSameNamespace(string left, string right)
        {
            // Compare normalized namespaces using the cache's case-insensitive identity behavior.
            return GetNormalizedNamespace(left).Equals(
                GetNormalizedNamespace(right),
                StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
