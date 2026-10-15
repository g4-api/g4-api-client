using G4.Api.Models;
using G4.Models;
using G4.Models.Events;
using G4.Plugins;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines the contract for managing and invoking G4™ Flows: complete automations persisted in LiteDB,
    /// cached by namespace and key, and invoked as one unit.
    /// </summary>
    /// <remarks>
    /// Flow management follows the template client contract: persistence changes first, then the in-memory flows cache,
    /// and status codes report the outcome. Invocation follows the automation client contract, except that the automation
    /// is read from the flows cache instead of being supplied by the caller. A null or empty namespace selects
    /// <c>G4.System</c> in every operation.
    /// </remarks>
    public interface IFlowsClient
    {
        #region *** Events     ***
        /// <summary>
        /// Occurs when an automation synchronization callback is triggered.
        /// </summary>
        event EventHandler<AutomationCallbackEventArgs> AutomationCallback;

        /// <summary>
        /// Occurs after an automation has been invoked.
        /// </summary>
        event EventHandler<AutomationEventArgs> AutomationInvoked;

        /// <summary>
        /// Occurs before invoking an automation.
        /// </summary>
        event EventHandler<AutomationEventArgs> AutomationInvoking;

        /// <summary>
        /// Occurs when an automation request is initialized.
        /// </summary>
        event EventHandler<AutomationQueueModel> AutomationRequestInitialized;

        /// <summary>
        /// Occurs whenever the status of an automation changes.
        /// </summary>
        event EventHandler<AutomationEventArgs> AutomationStatusChanged;

        /// <summary>
        /// Occurs when the automation process has stopped, providing the exit code of the operation.
        /// </summary>
        event EventHandler<int> AutomationStopped;

        /// <summary>
        /// Occurs after a job has been invoked.
        /// </summary>
        event EventHandler<JobEventArgs> JobInvoked;

        /// <summary>
        /// Occurs before invoking a job.
        /// </summary>
        event EventHandler<JobEventArgs> JobInvoking;

        /// <summary>
        /// Occurs whenever the status of a job within an automation changes.
        /// </summary>
        event EventHandler<JobEventArgs> JobStatusChanged;

        /// <summary>
        /// Occurs after a log entry has been created.
        /// </summary>
        event EventHandler<LogEventArgs> LogCreated;

        /// <summary>
        /// Occurs before a log entry is created.
        /// </summary>
        event EventHandler<LogEventArgs> LogCreating;

        /// <summary>
        /// Occurs when an error occurs during action execution.
        /// </summary>
        event EventHandler<(RuleEventArgs EventArguments, Exception Exception)> OnRuleError;

        /// <summary>
        /// Occurs after a new plugin is created.
        /// </summary>
        event EventHandler<(PluginBase Plugin, G4RuleModelBase Rule)> PluginCreated;

        /// <summary>
        /// Occurs when a rule synchronization callback is triggered.
        /// </summary>
        event EventHandler<RuleCallbackEventArgs> RuleCallback;

        /// <summary>
        /// Occurs after an action has been invoked.
        /// </summary>
        event EventHandler<RuleEventArgs> RuleInvoked;

        /// <summary>
        /// Occurs before invoking an action.
        /// </summary>
        event EventHandler<RuleEventArgs> RuleInvoking;

        /// <summary>
        /// Occurs whenever the status of a rule within an automation changes.
        /// </summary>
        event EventHandler<RuleEventArgs> RuleStatusChanged;

        /// <summary>
        /// Occurs after a stage has been invoked.
        /// </summary>
        event EventHandler<StageEventArgs> StageInvoked;

        /// <summary>
        /// Occurs before invoking a stage.
        /// </summary>
        event EventHandler<StageEventArgs> StageInvoking;

        /// <summary>
        /// Occurs whenever the status of a stage within an automation changes.
        /// </summary>
        event EventHandler<StageEventArgs> StageStatusChanged;
        #endregion

        #region *** Properties ***
        /// <summary>
        /// Gets the logger instance used for logging within the flows client.
        /// </summary>
        ILogger Logger { get; }
        #endregion

        #region *** Methods    ***
        /// <summary>
        /// Adds or replaces a flow by updating the LiteDB <c>Flows</c> collection and the in-memory flows cache.
        /// </summary>
        /// <param name="manifest">
        /// The flow manifest to persist and cache. Its <see cref="G4FlowManifestModel.Automation"/> is the Base64 encoding
        /// of the System.Text.Json serialization of a <see cref="G4AutomationModel"/>.
        /// </param>
        /// <returns>
        /// <see cref="StatusCodes.Status204NoContent"/> when the flow is added or replaced, or
        /// <see cref="StatusCodes.Status400BadRequest"/> when the manifest is null, has no key, or its automation is
        /// missing, is not valid Base64, or does not deserialize into an automation.
        /// </returns>
        /// <remarks>
        /// A stored flow with the same namespace and key is replaced. The automation is validated and cache conflicts
        /// are checked before persistence changes, so a rejected flow is never stored.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the key or an alias conflicts with another cached flow.</exception>
        int AddFlow(G4FlowManifestModel manifest);

        /// <summary>
        /// Removes every flow from the LiteDB <c>Flows</c> collection and from the in-memory flows cache.
        /// </summary>
        void ClearFlows();

        /// <summary>
        /// Retrieves a persisted flow by its namespace and key.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects <c>G4.System</c>.</param>
        /// <param name="key">The canonical key of the flow.</param>
        /// <returns>
        /// <see cref="StatusCodes.Status200OK"/> and the manifest when the flow is stored; otherwise
        /// <see cref="StatusCodes.Status404NotFound"/> and <c>null</c>.
        /// </returns>
        (int StatusCode, G4FlowManifestModel Manifest) GetFlow(string @namespace, string key);

        /// <summary>
        /// Retrieves every persisted flow.
        /// </summary>
        /// <returns>All stored flow manifests, or an empty collection when no flows are stored.</returns>
        IEnumerable<G4FlowManifestModel> GetFlows();

        /// <summary>
        /// Retrieves every persisted flow in the specified namespace.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects <c>G4.System</c>.</param>
        /// <returns>The stored flow manifests of the namespace, or an empty collection when it has none.</returns>
        IEnumerable<G4FlowManifestModel> GetFlows(string @namespace);

        /// <summary>
        /// Invokes a cached flow by its namespace and key and returns the automation responses.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects <c>G4.System</c>.</param>
        /// <param name="key">The canonical key or any alias of the flow.</param>
        /// <returns>
        /// <see cref="StatusCodes.Status200OK"/> and a dictionary mapping group IDs to their
        /// <see cref="G4AutomationResponseModel"/> responses when the flow is cached and runs;
        /// <see cref="StatusCodes.Status404NotFound"/> and <c>null</c> when it is not cached; or
        /// <see cref="StatusCodes.Status400BadRequest"/> and <c>null</c> when its stored automation cannot be decoded.
        /// </returns>
        /// <remarks>
        /// Every invocation decodes a new automation instance from the cached Base64 body, so an invocation never
        /// changes the cached flow.
        /// </remarks>
        (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(string @namespace, string key);

        /// <summary>
        /// Invokes a cached flow by its namespace and key with parameter values and caller overrides, and returns the
        /// automation responses.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects <c>G4.System</c>.</param>
        /// <param name="key">The canonical key or any alias of the flow.</param>
        /// <param name="invokeModel">The parameter values and the caller's authentication, driver parameters, and settings.</param>
        /// <returns>
        /// <see cref="StatusCodes.Status200OK"/> and a dictionary mapping group IDs to their
        /// <see cref="G4AutomationResponseModel"/> responses when the flow is cached and runs;
        /// <see cref="StatusCodes.Status404NotFound"/> and <c>null</c> when it is not cached; or
        /// <see cref="StatusCodes.Status400BadRequest"/> and <c>null</c> when its stored automation cannot be decoded,
        /// or is no longer valid JSON after its parameters were applied.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Parameter values replace the <c>{{$ parameters.Name }}</c> placeholders anywhere in the flow, as a template's
        /// argument does for its rules. Values come from <see cref="G4FlowInvokeModel.Parameters"/> and
        /// <see cref="G4FlowInvokeModel.Argument"/>; on a name in both, the argument value is used. A declared parameter
        /// without a value receives its default. Values are inserted as they are, and a placeholder without a value
        /// stays as it is.
        /// </para>
        /// <para>
        /// The caller's token always replaces the flow's token, and the caller's <see cref="G4FlowInvokeModel.ReferenceId"/>,
        /// when set, replaces the flow's automation ID, so the run can be stopped with <see cref="StopAutomation"/>.
        /// The caller's other authentication values, driver
        /// parameters, and settings only fill what the flow leaves out. Every invocation builds a new automation
        /// instance, so an invocation never changes the cached flow.
        /// </para>
        /// </remarks>
        (int StatusCode, IDictionary<string, G4AutomationResponseModel> Responses) Invoke(
            string @namespace,
            string key,
            G4FlowInvokeModel invokeModel);

        /// <summary>
        /// Removes a flow identified by its namespace and key from the LiteDB <c>Flows</c> collection and the flows cache.
        /// </summary>
        /// <param name="namespace">The flow namespace; null or empty selects <c>G4.System</c>.</param>
        /// <param name="key">The canonical key of the flow.</param>
        /// <returns>
        /// <see cref="StatusCodes.Status204NoContent"/> when the flow is removed, or
        /// <see cref="StatusCodes.Status404NotFound"/> when no stored flow matches.
        /// </returns>
        int RemoveFlow(string @namespace, string key);

        /// <summary>
        /// Stops the flow invocation identified by the specified automation ID.
        /// </summary>
        /// <param name="automationId">The unique identifier of the automation process to stop. Cannot be null or empty.</param>
        /// <returns>An integer indicating the result of the operation. A value of 0 indicates success; any other value indicates an error.</returns>
        int StopAutomation(string automationId);
        #endregion
    }
}
