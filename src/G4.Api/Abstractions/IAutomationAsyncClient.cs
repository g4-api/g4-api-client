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
    /// Defines a contract for automation clients that manage and monitor automation processes within the G4 system.
    /// </summary>
    public interface IAutomationAsyncClient
    {
        //#region *** Events     ***
        ///// <summary>
        ///// Occurs when an automation synchronization callback is triggered.
        ///// </summary>
        //event EventHandler<AutomationCallbackEventArgs> AutomationCallback;

        ///// <summary>
        ///// Occurs after an automation has been invoked.
        ///// </summary>
        //event EventHandler<AutomationEventArgs> AutomationInvoked;

        ///// <summary>
        ///// Occurs before invoking an automation.
        ///// </summary>
        //event EventHandler<AutomationEventArgs> AutomationInvoking;

        ///// <summary>
        ///// Occurs when an automation request is initialized.
        ///// </summary>
        //event EventHandler<AutomationQueueModel> AutomationRequestInitialized;

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
        /// Gets the logger instance used for logging within the automation client.
        /// </summary>
        ILogger Logger { get; }

        IQueueManager QueueManager { get; }
        #endregion

        #region *** Methods    ***
        ///// <summary>
        ///// Invokes the automation process for the provided automation model.
        ///// </summary>
        ///// <param name="automation">The automation model to be invoked.</param>
        ///// <returns>
        ///// A dictionary where the keys are group IDs and the values are automation response models.
        ///// </returns>
        //IDictionary<string, G4AutomationResponseModel> Invoke(G4AutomationModel automation);

        /// <summary>
        /// Adds an automation queue model to the active queue, organizing it under the appropriate group.
        /// </summary>
        /// <param name="queueModel">The <see cref="AutomationQueueModel"/> instance representing the automation to be marked as active.</param>
        void AddActiveAutomation(AutomationQueueModel queueModel);

        /// <summary>
        /// Adds a new automation by generating a set of automation requests from the provided automation model,
        /// creating corresponding queue models, and enqueuing them as pending tasks.
        /// </summary>
        /// <param name="automation">
        /// The <see cref="G4AutomationModel"/> instance representing the automation configuration from which
        /// individual automation requests will be derived.
        /// </param>
        void AddPendingAutomation(G4AutomationModel automation);

        /// <summary>
        /// Retrieves the next pending automation queue model.
        /// </summary>
        /// <returns>The next <see cref="AutomationQueueModel"/> from the pending queue.</returns>
        AutomationQueueModel GetPendingAutomation();
        #endregion
    }
}
