using G4.Api.Models;
using G4.Models;
using G4.Models.Events;
using G4.Plugins;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Defines a contract for automation clients that manage and monitor automation processes within the G4 system.
    /// </summary>
    public interface IAutomationAsyncClient
    {
        //#region *** Events     ***
        ///// <summary>
        ///// Occurs when an automation synchronization callback is triggered.
        ///// </summary>
        //event EventHandler<AutomationCallbackEventArgs> AutomationCallback;

        /// <summary>
        /// Occurs after an automation has been invoked.
        /// </summary>
        event EventHandler<AutomationEventArgs> AutomationInvoked;

        ///// <summary>
        ///// Occurs before invoking an automation.
        ///// </summary>
        //event EventHandler<AutomationEventArgs> AutomationInvoking;

        /// <summary>
        /// Occurs when an automation request is initialized.
        /// </summary>
        event EventHandler<AutomationQueueModel> AutomationRequestInitialized;

        ///// <summary>
        ///// Occurs whenever the status of an automation changes.
        ///// </summary>
        //event EventHandler<AutomationEventArgs> AutomationStatusChanged;

        ///// <summary>
        ///// Occurs after a job has been invoked.
        ///// </summary>
        //event EventHandler<JobEventArgs> JobInvoked;

        ///// <summary>
        ///// Occurs before invoking a job.
        ///// </summary>
        //event EventHandler<JobEventArgs> JobInvoking;

        ///// <summary>
        ///// Occurs whenever the status of a job within an automation changes.
        ///// </summary>
        //event EventHandler<JobEventArgs> JobStatusChanged;

        ///// <summary>
        ///// Occurs after a log entry has been created.
        ///// </summary>
        //event EventHandler<LogEventArgs> LogCreated;

        ///// <summary>
        ///// Occurs before a log entry is created.
        ///// </summary>
        //event EventHandler<LogEventArgs> LogCreating;

        ///// <summary>
        ///// Occurs when an error occurs during action execution.
        ///// </summary>
        //event EventHandler<(RuleEventArgs EventArguments, Exception Exception)> OnRuleError;

        ///// <summary>
        ///// Occurs after a new plugin is created.
        ///// </summary>
        //event EventHandler<(PluginBase Plugin, G4RuleModelBase Rule)> PluginCreated;

        ///// <summary>
        ///// Occurs when a rule synchronization callback is triggered.
        ///// </summary>
        //event EventHandler<RuleCallbackEventArgs> RuleCallback;

        ///// <summary>
        ///// Occurs after an action has been invoked.
        ///// </summary>
        //event EventHandler<RuleEventArgs> RuleInvoked;

        ///// <summary>
        ///// Occurs before invoking an action.
        ///// </summary>
        //event EventHandler<RuleEventArgs> RuleInvoking;

        ///// <summary>
        ///// Occurs whenever the status of a rule within an automation changes.
        ///// </summary>
        //event EventHandler<RuleEventArgs> RuleStatusChanged;

        ///// <summary>
        ///// Occurs after a stage has been invoked.
        ///// </summary>
        //event EventHandler<StageEventArgs> StageInvoked;

        ///// <summary>
        ///// Occurs before invoking a stage.
        ///// </summary>
        //event EventHandler<StageEventArgs> StageInvoking;

        ///// <summary>
        ///// Occurs whenever the status of a stage within an automation changes.
        ///// </summary>
        //event EventHandler<StageEventArgs> StageStatusChanged;
        //#endregion

        #region *** Properties ***
        /// <summary>
        /// Gets the concurrent queue of active automation queue models.
        /// </summary>
        ConcurrentDictionary<string, AutomationQueueModel> Active { get; }

        /// <summary>
        /// Gets the logger instance used for logging within the automation client.
        /// </summary>
        ILogger Logger { get; }

        /// <summary>
        /// Gets the queue manager instance used for managing automation queues.
        /// </summary>
        IQueueManager QueueManager { get; }
        #endregion

        #region *** Methods    ***
        ///// <summary>
        ///// Adds an automation queue model to the active queue, organizing it under the appropriate group.
        ///// </summary>
        ///// <param name="queueModel">The <see cref="G4QueueModel"/> instance representing the automation to be marked as active.</param>
        //void AddActiveAutomation(G4QueueModel queueModel);

        /// <summary>
        /// Adds new pending automation requests to the queue based on the provided automation model.
        /// </summary>
        /// <param name="automation">The automation model containing the configuration and parameters for generating automation tasks.</param>
        void AddPendingAutomation(G4AutomationModel automation);

        /// <summary>
        /// Enables pending automation by retrieving the next pending automation queue model, 
        /// creating corresponding automation queue models, setting their status to 'Processing',
        /// and adding them to the active collection.
        /// </summary>
        void EnablePendingAutomation();

        /// <summary>
        /// Retrieves the next active automation queue model from the active queue.
        /// </summary>
        /// <returns>The next <see cref="AutomationQueueModel"/> in the active queue, or <c>null</c> if no active automation is available.</returns>
        AutomationQueueModel GetActiveAutomation();

        /// <summary>
        /// Retrieves an active automation queue model by its identifier from the active queue.
        /// </summary>
        /// <param name="id">The identifier of the automation to retrieve.</param>
        /// <returns>The <see cref="AutomationQueueModel"/> matching the specified identifier, or <c>null</c> if not found.</returns>
        AutomationQueueModel GetActiveAutomation(string id);

        Task<IDictionary<string, G4AutomationResponseModel>> StartAsync();
        #endregion
    }
}
