using G4.Api.Abstractions;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Plugins.Engine;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace G4.Api.Clients
{
    /// <summary>
    /// Represents a client responsible for managing and invoking automations within the G4 system.
    /// </summary>
    /// <param name="logger">The logger instance for logging.</param>
    internal class AutomationAsyncClient(ILogger logger, IQueueManager queueManager) : ClientBase, IAutomationAsyncClient
    {
        /// <inheritdoc />
        public event EventHandler<AutomationEventArgs> AutomationInvoked;

        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> AutomationRequestInitialized;

        /// <summary>
        /// This event is raised whenever the status of an automation changes.
        /// </summary>
        public event EventHandler<AutomationEventArgs> AutomationStatusChanged;

        public AutomationAsyncClient(ILogger logger)
            : this(logger, queueManager: new BasicQueueManager())
        { }

        /// <inheritdoc />
        public ConcurrentDictionary<string, AutomationQueueModel> Active { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public ConcurrentDictionary<string, G4AutomationResponseModel> Completed { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <inheritdoc />
        public IQueueManager QueueManager => queueManager;

        /// <inheritdoc />
        public ILogger Logger => logger;

        /// <summary>
        /// Gets the concurrent queue of pending automation queue models.
        /// </summary>
        public ConcurrentQueue<AutomationQueueModel> Pending { get; }

        /// <summary>
        /// Gets the concurrent bag of automation queue models with errors.
        /// </summary>
        public ConcurrentBag<G4QueueModel> Errors { get; }

        ///// <inheritdoc />
        //public void AddActiveAutomation(G4QueueModel queueModel)
        //{
        //    try
        //    {
        //        // Add the automation queue model to the active queue.
        //        queueManager.AddActive(queueModel);

        //        // Log the successful completion of the operation.
        //        Logger.LogDebug(
        //            message: "Successfully added active automation {Id} to the queue.",
        //            args: queueModel.Automation.Reference.Id);
        //    }
        //    catch (Exception e)
        //    {
        //        // Log the error with exception details for troubleshooting.
        //        Logger.LogError(exception: e, "Failed to add active automation to the queue.");
        //    }
        //}

        /// <inheritdoc />
        public void AddCompletedAutomation(string key, G4AutomationResponseModel response)
        {
            AddCompletedEntry(client: this, key, response);
        }

        /// <inheritdoc />
        public void AddPendingAutomation(G4AutomationModel automation)
        {
            // Generate new automation instances along with their associated data providers from the provided model.
            var automations = automation.NewAutomations();

            // Iterate over each automation instance and its corresponding data provider.
            foreach (var (automationModel, dataProvider) in automations)
            {
                // Initialize the automation model with the shared CacheManager instance and the data provider.
                automationModel.Initialize(CacheManager.Instance, dataProvider);

                // Create a new queue model for the automation model using the associated data provider.
                var queueModel = automationModel.NewQueueModel(dataProvider);

                // Add the newly created queue model to the pending automation queue for later processing.
                QueueManager.AddPending(queueModel);
            }
        }

        /// <inheritdoc />
        public void EnablePendingAutomation()
        {
            EnableOne(client: this);
        }

        /// <inheritdoc />
        public AutomationQueueModel GetActiveAutomation()
        {
            // Retrieve the first active automation from the active collection, if available.
            var automation = Active?.Values.FirstOrDefault();

            // Extract the unique identifier from the active automation's status, if it exists.
            var id = automation?.Status?.Automation?.Reference?.Id;

            // Retrieve and return the active automation by its identifier.
            return GetOne(client: this, id);
        }

        /// <inheritdoc />
        public AutomationQueueModel GetActiveAutomation(string id)
        {
            return GetOne(client: this, id);
        }

        /// <inheritdoc />
        public Task<IDictionary<string, G4AutomationResponseModel>> StartAsync()
            => Task.Run<IDictionary<string, G4AutomationResponseModel>>(() =>
            {
                // Create a thread-safe dictionary to hold automation responses.
                var responseCollection = new ConcurrentDictionary<string, G4AutomationResponseModel>();

                // Enable one pending automation by processing it from the client queue.
                EnableOne(client: this);

                // Retrieve the currently active automation queue model.
                var queueModel = GetActiveAutomation();

                // If no active automation is found, return the empty response collection.
                if (queueModel == null)
                {
                    return responseCollection;
                }

                // Retrieve the automation invoker and the automation model from the queue.
                var invoker = queueModel.Invoker;
                var automation = queueModel.Status.Automation;

                // Use the automation's unique reference id as the key.
                var key = automation.Reference.Id;

                // Invoke the automation and capture the response.
                var response = invoker.Invoke();

                // Store the response in the collection using the unique key.
                responseCollection[key] = response;

                // Add a completed entry to the client log or history for the processed automation.
                AddCompletedEntry(client: this, key, response);

                // Return the collection of responses.
                return responseCollection;
            });

        /// <inheritdoc />
        public void UpdateActive(G4QueueModel queueModel)
        {
            try
            {
                // Attempt to update the active queue model using the QueueManager.
                QueueManager.UpdateActive(queueModel);

                // Log the successful update with the automation id.
                Logger.LogDebug(
                    message: "Successfully updated active automation {Id} in the queue.",
                    args: queueModel.Automation.Reference.Id);
            }
            catch (Exception e)
            {
                // Log any errors encountered during the update operation with exception details.
                Logger.LogError(exception: e, "Failed to update active automation in the queue.");
            }
        }

        // Adds a new completed automation response to the dictionary and ensures that only the last 100 entries are kept.
        private static void AddCompletedEntry(AutomationAsyncClient client, string key, G4AutomationResponseModel response)
        {
            // Add or update the completed entry in the dictionary.
            client.Completed[key] = response;

            // Check if the dictionary has more than 100 entries.
            if (client.Completed.Count < 100)
            {
                return;
            }

            // Determine how many entries to remove.
            var excessCount = client.Completed.Count - 100;

            // Order entries by their timestamp (assuming older entries have lower timestamps)
            // and select the keys of the oldest entries.
            var keysToRemove = client.Completed
                .OrderBy(i => i.Value.PerformancePoint.End)
                .Take(excessCount)
                .Select(i => i.Key);

            // Remove the oldest entries from the dictionary.
            foreach (var oldKey in keysToRemove)
            {
                client.Completed.TryRemove(oldKey, out _);
            }
        }

        // Enables one pending automation by retrieving it from the queue manager,
        // generating corresponding automation queue models, and setting their processing status.
        private static void EnableOne(AutomationAsyncClient client)
        {
            try
            {
                // Retrieve the next pending automation queue model from the queue manager.
                var queueModel = client.QueueManager.GetPending();

                // If there is no pending automation, exit the method.
                if (queueModel == null)
                {
                    return;
                }

                // Create a tuple containing the automation model and its associated properties (data source).
                var activeAutomationModel = (queueModel.Automation, DataSource: queueModel.Properties);

                // Generate one or more automation queue models based on the pending automation.
                var automationQueueModels = NewAutomationRequests(
                    client,
                    automations: activeAutomationModel);

                // Iterate through each generated automation queue model.
                foreach (var automationQueueModel in automationQueueModels)
                {
                    // Extract the automation model and its progress status from the queue model.
                    var automationModel = automationQueueModel.Status.Automation;
                    var statusModel = automationQueueModel.Status.ProgressStatus;

                    // Set the automation's progress status to 'Processing'.
                    statusModel.Status = G4QueueModel.QueueStatusCodes.Processing;

                    // Add the automation to the active collection using its unique reference id.
                    client.Active[automationModel.Reference.Id] = automationQueueModel;
                }
            }
            catch (Exception e)
            {
                // Log any errors encountered during the enabling of the pending automation.
                client.Logger.LogError(exception: e, "Failed to enable pending automation.");
            }
        }

        // Retrieves and removes an automation queue model with the specified identifier from the active queue.
        private static AutomationQueueModel GetOne(AutomationAsyncClient client, string id)
        {
            // Attempt to get the automation model with the specified id.
            // If the id is null, empty, or not found in the active queue, return null.
            if (string.IsNullOrEmpty(id) || !client.Active.TryGetValue(id, out AutomationQueueModel _))
            {
                // Return null if no matching automation model is found.
                return null;
            }

            // Attempt to remove the automation model from the active queue.
            // The removed model (if any) is stored in nextQueueModel.
            client.Active.TryRemove(id, out var nextQueueModel);

            // Return the removed automation model.
            return nextQueueModel;
        }

        // Creates new automation queue models based on the provided automation models and data providers.
        private static ConcurrentBag<AutomationQueueModel> NewAutomationRequests(
            AutomationAsyncClient client,
            params (G4AutomationModel Automation, IDictionary<string, object> DataProvider)[] automations)
        {
            // Create a thread-safe collection to store the resulting automation queue models.
            var queueModels = new ConcurrentBag<AutomationQueueModel>();

            // Iterate over each automation and its corresponding data provider.
            for (int i = 0; i < automations.Length; i++)
            {
                // Deconstruct the tuple into the automation and data provider.
                var (automation, dataProvider) = automations[i];

                // Create a new queue model status using the automation model and provided properties.
                var queueModel = automation.NewQueueModel(properties: dataProvider);

                // Initialize the automation with the cache manager and data provider.
                queueModel.Automation.Initialize(CacheManager.Instance, dataProvider);

                // Set the iteration number for both the automation and its reference.
                queueModel.Automation.Iteration = i;
                queueModel.Automation.Reference.Iteration = i;

                // Update the progress status with details from the automation.
                queueModel.ProgressStatus.GroupId = queueModel.Automation.GroupId;
                queueModel.ProgressStatus.Description = queueModel.Automation.Reference.Description;
                queueModel.ProgressStatus.Iteration = i;
                queueModel.ProgressStatus.Name = queueModel.Automation.Reference.Name;
                queueModel.ProgressStatus.Id = $"{queueModel.Automation.Reference.Id}";

                // Invoke the AutomationRequestInitialized event on the client.
                client.AutomationRequestInitialized?.InvokeSafely(logger: client.Logger, sender: client, e: new()
                {
                    Status = queueModel
                });

                // Create a new automation invoker based on the queue model's automation.
                var invoker = new AutomationInvoker(queueModel.Automation, client.Logger);

                // Register the invoker events for the automation queue model.
                RegisterInvokerEvents(client, invoker);

                // Create a new cancellation token source for the queue model.
                var cancellationTokenSource = new CancellationTokenSource();

                // Create a new automation queue model with the invoker and status.
                var automationQueueModel = new AutomationQueueModel(invoker, queueModel, cancellationTokenSource);

                // Add the newly created queue model to the thread-safe collection.
                queueModels.Add(automationQueueModel);
            }

            // Return the collection of automation queue models.
            return queueModels;
        }

        // Registers event handlers for the automation invoker events, allowing the client to respond
        // to automation invocations in a safe and logged manner.
        private static void RegisterInvokerEvents(AutomationAsyncClient client, IAutomationInvoker invoker)
        {
            // Subscribe to the AutomationInvoked event of the invoker.
            invoker.AutomationInvoked += (_, args) =>
            {
                // Create a new automation event argument using the provided automation details, status, and response.
                var e = AutomationEventArgs.New(args.Automation, args.AutomationStatus, args.Response);

                // Safely invoke the client's AutomationInvoked event, ensuring that any exceptions are logged.
                client.AutomationInvoked?.InvokeSafely(logger: client.Logger, sender: client, e);
            };
        }
    }


















































    //internal class AutomationAsyncClient(IQueueManager queueManager, ILogger logger) : ClientBase(), IAutomationClient
    //{
    //    // Queue manager for handling automation queues
    //    private readonly IQueueManager _queueManager = queueManager;

    //    public AutomationAsyncClient(IQueueManager queueManager)
    //        : this(queueManager, logger: G4Logger.Instance)
    //    { }

    //    public IEnvironmentsClient Environments { get; } = new EnvironmentsClient();

    //    public IIntegrationClient Integration { get; } = new IntegrationClient();

    //    public ILogger Logger { get; } = logger;

    //    #region *** Events  ***

    //    /// <summary>
    //    /// This event is raised whenever the status of an automation changes.
    //    /// </summary>
    //    public event EventHandler<AutomationStatusEventArgs> AutomationStatusChanged;

    //    /// <summary>
    //    /// This event is raised whenever the status of a job within an automation changes.
    //    /// </summary>
    //    public event EventHandler<JobStatusEventArgs> JobStatusChanged;

    //    /// <summary>
    //    /// Event triggered before creating a log entry.
    //    /// </summary>
    //    public event EventHandler<IDictionary<string, object>> LogCreating;

    //    /// <summary>
    //    /// Event triggered after creating a log entry.
    //    /// </summary>
    //    public event EventHandler<IDictionary<string, object>> LogCreated;

    //    /// <summary>
    //    /// Event triggered when an error occurs during logging.
    //    /// </summary>
    //    public event EventHandler<Exception> LogError;

    //    /// <summary>
    //    /// Event raised after a new plugin is created.
    //    /// </summary>
    //    public event EventHandler<(PluginBase Plugin, G4RuleModelBase Rule)> PluginCreated;

    //    /// <summary>
    //    /// This event is raised whenever the status of a rule within an automation changes.
    //    /// </summary>
    //    public event EventHandler<RuleStatusEventArgs> RuleStatusChanged;

    //    /// <summary>
    //    /// This event is raised whenever the status of a stage within an automation changes.
    //    /// </summary>
    //    public event EventHandler<StageStatusEventArgs> StageStatusChanged;

    //    /// <summary>
    //    /// Occurs when an automation sync callback is triggered.
    //    /// </summary>
    //    public event EventHandler<AutomationCallbackEventArgs> AutomationSyncCallback;

    //    /// <summary>
    //    /// Occurs when a rule sync callback is triggered.
    //    /// </summary>
    //    public event EventHandler<RuleCallbackEventArgs> RuleSyncCallback;
    //    #endregion

    //    /// <summary>
    //    /// Asynchronously invokes the automation process for the provided automation model.
    //    /// </summary>
    //    /// <param name="automation">The automation model to be invoked.</param>
    //    /// <returns>A task representing the asynchronous operation, with a dictionary where the keys are group IDs and the values are automation response models.</returns>
    //    public Task<IDictionary<string, G4AutomationResponseModel>> InvokeAsync(G4AutomationModel automation)
    //    {
    //        // 1. create automatin requests
    //        // 2. enqueue requests to queue manager pending queue
    //        // 3. invoke automation for each request in the pending queue
    //        // 4. use while loop to process pending queue until empty or cancellation requested
    //        // 5. use automation invoked event to retrieve response on the client when automation is complete
    //        // 6. group responses by group ID
    //        // 7. return status indicating run has been started
    //        // 8. use parallel processing to invoke automation for each request in the pending queue
    //        // Run the Invoke method asynchronously using a Task
    //        return Task.Run(() => Invoke(automation));
    //    }

    //    /// <summary>
    //    /// Invokes the automation process for the provided automation model.
    //    /// </summary>
    //    /// <param name="automation">The automation model to be invoked.</param>
    //    /// <returns>A dictionary where the keys are group IDs and the values are automation response models.</returns>
    //    public IDictionary<string, G4AutomationResponseModel> Invoke(G4AutomationModel automation)
    //    {
    //        // Concurrent bag to store responses
    //        var responseCollection = new ConcurrentBag<(string GroupId, G4AutomationResponseModel Response)>();

    //        // A CancellationTokenSource that can be used to cancel ongoing operations in the G4Client.
    //        using var cancellationTokenSource = new CancellationTokenSource();

    //        // Generate new automations based on the provided automation model
    //        var automations = automation.NewAutomations();

    //        // Create parallel options for parallel processing
    //        var parallelOptions = NewParallelOptions(automation, cancellationTokenSource.Token);

    //        // Iterate over each automation in the collection
    //        var queueModels = NewAutomationRequests(client: this, automations, registerStatusEvents: false);

    //        var tasks = new ConcurrentBag<Task>();

    //        foreach (var queueModel in queueModels)
    //        {
    //            // Create a new RuleSyncCallbackEventArgs instance to store rule callback
    //            tasks.Add(new Task(() =>
    //            {
    //                // arguments for the client and queue model instance
    //                var ruleCallbackArgs = new RuleCallbackEventArgs
    //                {
    //                    CancellationTokenSource = cancellationTokenSource,
    //                    Client = this,
    //                    Queue = queueModels
    //                };

    //                // Set up the RuleInvoked event for the invoker in the queue model to
    //                // trigger the rule sync callback
    //                queueModel.Invoker.RuleInvoked += (_, args) =>
    //                {
    //                    // Update the rule callback arguments with the event arguments
    //                    ruleCallbackArgs.RuleEventArgs = args;

    //                    // Invoke the rule sync callback event on the client
    //                    RuleSyncCallback?.Invoke(sender: this, e: ruleCallbackArgs);

    //                    // Check for cancellation request and throw if requested
    //                    ruleCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
    //                };

    //                // Create a new AutomationSyncCallbackEventArgs instance to store automation callback
    //                // arguments for the client and queue model instance
    //                var automationCallbackArgs = new AutomationCallbackEventArgs
    //                {
    //                    CancellationTokenSource = cancellationTokenSource,
    //                    Client = this,
    //                    Queue = queueModels
    //                };

    //                // Set up the AutomationInvoked event for the invoker in the queue model to
    //                // trigger the automation sync callback
    //                queueModel.Invoker.AutomationInvoked += (_, args) =>
    //                {
    //                    // New response based on the queue model and automation response model
    //                    var response = NewResponse(queueModel, args.Response);

    //                    // Add the response to the collection of responses for the client to process
    //                    // later on completion of the automation process for the queue model instance
    //                    responseCollection.Add(response);

    //                    // Update the automation callback arguments with the event arguments
    //                    automationCallbackArgs.AutomationEventArgs = args;

    //                    // Invoke the automation sync callback event on the client
    //                    AutomationSyncCallback?.Invoke(sender: this, e: automationCallbackArgs);

    //                    // Check for cancellation request and throw if requested
    //                    automationCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
    //                };

    //                // Invoke automation for the current queue model
    //                queueModel.Invoker.Invoke();
    //            }));
    //        }

    //        // Parallel processing until all automations are processed or cancellation is requested
    //        try
    //        {
    //            Parallel.ForEach(tasks, parallelOptions, task =>
    //            {
    //                task.Start();
    //                task.Wait();
    //            });
    //        }
    //        // Silence the exception if the task was canceled by the
    //        // user or client code and not due to an error in the task itself
    //        catch (OperationCanceledException e)
    //        {
    //            Logger.LogInformation(exception: e, "The automation process was canceled by the user.");
    //        }
    //        catch (Exception e)
    //        {
    //            Logger.LogError(exception: e, "An error occurred while invoking the automation process.");
    //        }

    //        // Return the results
    //        return NewAutomationResponse(responseCollection);
    //    }

    //    // Creates a list of new automation queue models based on the provided client and automations.
    //    private static ConcurrentBag<AutomationQueueModel> NewAutomationRequests(
    //        AutomationAsyncClient client,
    //        IEnumerable<(G4AutomationModel Automation, IDictionary<string, object> DataProvider)> automations,
    //        bool registerStatusEvents)
    //    {
    //        // Initialize a list to store the resulting automation queue models
    //        var queueModels = new ConcurrentBag<AutomationQueueModel>();

    //        // Process each automation and its corresponding data provider
    //        foreach (var (automation, dataProvider) in automations)
    //        {
    //            // Create a new queue model from the automation model and data provider
    //            var status = automation.NewQueueModel(properties: dataProvider);

    //            // Create a new automation invoker based on the queue model's automation
    //            var invoker = new AutomationInvoker(status.Automation);

    //            // Initialize the automation using the cache manager
    //            status.Automation.Initialize(CacheManager.Instance);

    //            // Create a new automation queue model with the invoker and status
    //            var queueModel = new AutomationQueueModel(invoker, status);

    //            // Set up new invoker events using the client and the newly created queue model
    //            if (registerStatusEvents)
    //            {
    //                NewStatusEvents(client, invoker, queueModel);
    //            }

    //            // Set up new logger events using the client, invoker, and the newly created queue model
    //            NewLoggerEvents(client, invoker, queueModel);

    //            // Invoke the AutomationRequestInitialized event on the client
    //            client.AutomationRequestInitialized?.Invoke(sender: client, e: queueModel);

    //            // Add the queue model to the list of results
    //            queueModels.Add(queueModel);
    //        }

    //        // Return the list of new automation queue models
    //        return queueModels;
    //    }

    //    // Creates a new automation response dictionary from a collection of response groups.
    //    private static Dictionary<string, G4AutomationResponseModel> NewAutomationResponse(
    //        IEnumerable<(string GroupId, G4AutomationResponseModel Response)> responseCollection)
    //    {
    //        // Group responses by group ID
    //        var responseGroups = responseCollection.GroupBy(i => i.GroupId);

    //        // Dictionary to store results
    //        var results = new Dictionary<string, G4AutomationResponseModel>();

    //        // Process response groups and create result dictionary
    //        foreach (var responseGroup in responseGroups)
    //        {
    //            // Create a new automation response based on the response group
    //            var (key, response) = NewAutomationResponse(responseGroup);

    //            // Add the response to the results dictionary
    //            results[key] = response;
    //        }

    //        // Return the results
    //        return results;
    //    }

    //    // Creates a new automation response from a grouping of responses.
    //    private static (string Key, G4AutomationResponseModel Response) NewAutomationResponse(
    //        IGrouping<string, (string Key, G4AutomationResponseModel Response)> group)
    //    {
    //        // Dictionary to store merged sessions
    //        var sessions = new Dictionary<string, G4ResponseModel>();

    //        // Merge sessions from each response in the group
    //        foreach (var item in group.Select(i => i.Response))
    //        {
    //            sessions.Merge(item.Sessions);
    //        }

    //        // Calculate performance point
    //        var performancePoint = new G4AutomationPerformancePointModel
    //        {
    //            AuthenticationTime = sessions.Average(i => ((G4AutomationPerformancePointModel)i.Value.PerformancePoint).AuthenticationTime),
    //            End = sessions.Select(i => i.Value.PerformancePoint.End).OrderDescending().First(),
    //            RunTime = sessions.Average(i => i.Value.PerformancePoint.RunTime),
    //            SessionTime = sessions.Average(i => ((G4AutomationPerformancePointModel)i.Value.PerformancePoint).SessionTime),
    //            SetupDelegationTime = sessions.Average(i => i.Value.PerformancePoint.SetupDelegationTime),
    //            SetupTime = sessions.Average(i => i.Value.PerformancePoint.SetupTime),
    //            Start = sessions.Select(i => i.Value.PerformancePoint.Start).Order().First(),
    //            TeardownDelegationTime = sessions.Average(i => i.Value.PerformancePoint.TeardownDelegationTime),
    //            TeardownTime = sessions.Average(i => i.Value.PerformancePoint.TeardownTime),
    //            Timeouts = sessions.Average(i => i.Value.PerformancePoint.Timeouts)
    //        };

    //        // Create a new automation response
    //        var response = new G4AutomationResponseModel
    //        {
    //            DataProvider = group.SelectMany(i => i.Response.DataProvider),
    //            PerformancePoint = performancePoint,
    //            Sessions = sessions,
    //        };

    //        // Return the key and the new automation response
    //        return (group.Key, response);
    //    }

    //    // Creates and returns a new ParallelOptions object based on the settings of the provided automation.
    //    private static ParallelOptions NewParallelOptions(G4AutomationModel automation, CancellationToken cancellationToken)
    //    {
    //        // Get automation settings
    //        var automationSettings = automation.Settings.AutomationSettings;

    //        // Determine the maximum degree of parallelism
    //        var maxDegreeOfParallelism = automationSettings.MaxParallel < 1
    //            ? 1
    //            : automationSettings.MaxParallel;

    //        // Create and return the parallel options object
    //        return new ParallelOptions
    //        {
    //            CancellationToken = cancellationToken,
    //            MaxDegreeOfParallelism = maxDegreeOfParallelism
    //        };
    //    }

    //    // Creates a new response tuple containing the key and the G4AutomationResponseModel.
    //    private static (string Key, G4AutomationResponseModel Response) NewResponse(AutomationQueueModel queueModel, G4AutomationResponseModel response)
    //    {
    //        // If the queue model is null, return null for both key and response
    //        if (queueModel == null)
    //        {
    //            return (null, null);
    //        }

    //        // Calculate the time spent in the queue
    //        queueModel.Status.TimeInQueue = DateTime.UtcNow - queueModel.Status.Created;

    //        // Set the data provider properties for the response and its first session
    //        response.DataProvider = [queueModel.Status.Properties];
    //        response.Sessions.First().Value.DataProvider = [queueModel.Status.Properties];

    //        // Return the group ID and the updated response model
    //        return (queueModel.Status.Automation.GroupId, response);
    //    }

    //    #region *** Invoker ***
    //    // Updates the job model to mark the stage as complete and records performance data.
    //    private static void CompleteStatus(AutomationAsyncClient client, JobEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference, stage reference, and job reference
    //        var reference = args.Automation.Reference.Id;
    //        var stageId = args.Stage.Reference.Id;
    //        var jobId = args.Job.Reference.Id;

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Retrieve the stage status for the specified stage ID
    //        var stageStatus = queueStatus.StagesStatus[stageId];

    //        // Update the status of the job to "Complete"
    //        stageStatus.JobsStatus[jobId].Status = G4QueueModel.QueueStatusCodes.Complete;

    //        // Increment the count of completed jobs in the stage status
    //        stageStatus.CompletedJobs = stageStatus.CompletedJobs >= args.Stage.Jobs.Count()
    //            ? args.Stage.Jobs.Count()
    //            : stageStatus.CompletedJobs + 1;

    //        // Calculate and update the stage progress based on completed jobs
    //        stageStatus.Progress = MeasureProgress(args.Stage, stageStatus);

    //        // Raise the JobStatusChanged event with the updated queue status, stage status, and job status
    //        client.JobStatusChanged?.Invoke(sender: client, e: new JobStatusEventArgs
    //        {
    //            JobStatus = stageStatus.JobsStatus[jobId],
    //            QueueStatus = queueStatus,
    //            Reference = jobId,
    //            StageStatus = stageStatus
    //        });
    //    }

    //    // Completes the status model for a rule.
    //    private static void CompleteStatus(AutomationAsyncClient client, RuleEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference
    //        var reference = args.Automation.Reference.Id;

    //        // Get the stage reference, job reference, and rule reference
    //        var stageId = args.Rule.Reference.JobReference.StageReference.Id;
    //        var jobId = args.Rule.Reference.JobReference.Id;
    //        var ruleId = args.Rule.Reference.Id;

    //        // Get the total number of rules
    //        var totalRules = args.Rule.Rules.GetRulesCount();

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Retrieve the stage status for the specified stage ID
    //        var stageStatus = queueStatus.StagesStatus[stageId];

    //        // Retrieve the job status for the specified job ID
    //        var jobStatus = stageStatus.JobsStatus[jobId];

    //        // Increment the count of completed rules in the job status
    //        jobStatus.CompletedRules = jobStatus.CompletedRules >= totalRules
    //            ? args.Rule.Rules.GetRulesCount()
    //            : jobStatus.CompletedRules + 1;

    //        // Retrieve the rule status for the specified rule ID
    //        var ruleStatus = jobStatus.RulesStatus[ruleId];

    //        // Update the status of the rule to "Complete"
    //        ruleStatus.Status = G4QueueModel.QueueStatusCodes.Complete;

    //        // Measure the progress of rules completion for the job status and rule status
    //        jobStatus.Progress = MeasureProgress(jobStatus);
    //        ruleStatus.Progress = MeasureProgress(ruleStatus);

    //        // Raise the JobStatusChanged event with the updated queue status and stage status
    //        client.JobStatusChanged?.Invoke(sender: client, e: new JobStatusEventArgs
    //        {
    //            JobStatus = stageStatus.JobsStatus[jobId],
    //            QueueStatus = queueStatus,
    //            StageStatus = stageStatus
    //        });
    //    }

    //    // Updates the status model to mark the stage as complete and records performance data.
    //    private static void CompleteStatus(AutomationAsyncClient client, StageEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference and stage reference
    //        var reference = args.Automation.Reference.Id;
    //        var stageId = args.Stage.Reference.Id;

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Increment the count of completed stages in the queue status
    //        queueStatus.CompletedStages = queueStatus.CompletedStages >= args.Automation.Stages.Count()
    //            ? args.Automation.Stages.Count()
    //            : queueStatus.CompletedStages + 1;

    //        // Update the status of the stage to "Complete"
    //        queueStatus.StagesStatus[stageId].Status = G4QueueModel.QueueStatusCodes.Complete;

    //        // Measure the progress of automation stages
    //        queueStatus.Progress = MeasureProgress(args.Automation, queueStatus);

    //        // Invoke the StageStatusChanged event with the updated queue status and stage status
    //        client.StageStatusChanged?.Invoke(sender: client, e: new()
    //        {
    //            QueueStatus = queueStatus,
    //            Reference = stageId,
    //            StageStatus = queueStatus.StagesStatus[stageId]
    //        });
    //    }

    //    // Updates the status model to mark the automation as complete and records performance data.
    //    private static void CompleteStatus(AutomationAsyncClient client, AutomationInvokedEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Store the Automation object for easier access
    //        var automation = args.Automation;

    //        // Get the automation reference
    //        var reference = automation.Reference.Id;

    //        // Retrieve the queue model for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Update the performance data in the status model
    //        queueStatus.PerformancePoint = args.Response.PerformancePoint.Copy<G4AutomationPerformancePointModel>();

    //        // Update the status of the automation to "Complete"
    //        queueStatus.StatusCode = G4QueueModel.QueueStatusCodes.Complete;

    //        // Raise AutomationStatusChanged event with the updated queue status
    //        client.AutomationStatusChanged?.Invoke(sender: client, e: new()
    //        {
    //            GroupId = groupId,
    //            Reference = reference,
    //            QueueStatus = queueStatus
    //        });
    //    }

    //    // Measures the progress of stages in the automation.
    //    private static double MeasureProgress(G4StageModel stage, G4QueueStatusModel.StageStatusModel stageStatus)
    //    {
    //        // Total number of stages in the automation
    //        var jobs = stage.Jobs.Count();

    //        // Number of completed stages
    //        var complete = stageStatus
    //            .JobsStatus
    //            .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

    //        // Calculate progress as a percentage
    //        return complete == 0 ? 0D : (double)complete / jobs * 100;
    //    }

    //    // Measures the progress of rules within a job status.
    //    private static double MeasureProgress(G4QueueStatusModel.JobStatusModel jobStatus)
    //    {
    //        // Get the total number of rules for the job
    //        var rules = jobStatus.TotalRules;

    //        // Count the number of completed rules
    //        var complete = jobStatus
    //            .RulesStatus
    //            .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

    //        // Calculate the progress based on the number of completed rules
    //        return complete == 0 ? 0D : (double)complete / rules * 100;
    //    }

    //    // Measures the progress of a rule within a rule status.
    //    private static double MeasureProgress(G4QueueStatusModel.PluginStatusModel ruleStatus)
    //    {
    //        // Get the total number of rules
    //        var rules = ruleStatus.TotalRules;

    //        // Count the number of completed rules
    //        var complete = ruleStatus
    //            .RulesStatus
    //            .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

    //        // Calculate the progress based on the number of completed rules
    //        return complete == 0 ? 0D : (double)complete / rules * 100;
    //    }

    //    // Measures the progress of stages in the automation.
    //    private static double MeasureProgress(G4AutomationModel automation, G4QueueStatusModel queueStatus)
    //    {
    //        // Total number of stages in the automation
    //        var stages = automation.Stages.Count();

    //        // Number of completed stages
    //        var complete = queueStatus
    //            .StagesStatus
    //            .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

    //        // Calculate progress as a percentage
    //        return complete == 0 ? 0D : (double)complete / stages * 100;
    //    }

    //    // Sets up event handlers for the logger associated with a specific automation invoker and queue model instance
    //    private static void NewLoggerEvents(AutomationAsyncClient client, IAutomationInvoker invoker, AutomationQueueModel queueModel)
    //    {
    //        // Find the logger associated with G4Logger from the AutomationInvoker's logger
    //        var invokerLogger = invoker.Logger.FindLogger<G4Logger>();

    //        // LogCreating event handler: Append group and automation info, then invoke client's LogCreating event
    //        invokerLogger.LogCreating += (_, args) =>
    //        {
    //            // Add group ID and automation ID information to the event arguments
    //            args["Automation"] = queueModel.Status.Automation.Reference.Id;
    //            args["Group"] = queueModel.Status.Automation.GroupId;
    //            args["Ietration"] = queueModel.Status.Automation.Iteration;
    //            args["Runtime"] = $"{queueModel.Status.Automation.Reference.Id}_{queueModel.Status.Automation.Iteration}";

    //            // Invoke the LogCreating event on the client, passing the sender and event arguments
    //            client.LogCreating?.Invoke(sender: client, e: args);
    //        };

    //        // LogCreated event handler: Invoke client's LogCreated event
    //        invokerLogger.LogCreated += (_, args) => client.LogCreated?.Invoke(sender: client, e: args);

    //        // LogError event handler: Invoke client's LogError event
    //        invokerLogger.LogError += (_, args) => client.LogError?.Invoke(sender: client, e: args);

    //        // Subscribe to the PluginCreated event on the invoker
    //        invoker.PluginCreated += (_, e) =>
    //        {
    //            // Find the G4Logger instance associated with the plugin
    //            var pluginLogger = e.Plugin.Logger.FindLogger<G4Logger>();

    //            // Handle the LogCreating event
    //            pluginLogger.LogCreating += (_, logEntries) =>
    //            {
    //                // Add group ID and automation ID information to the event arguments
    //                logEntries["Automation"] = queueModel.Status.Automation.Reference.Id;
    //                logEntries["Group"] = queueModel.Status.Automation.GroupId;
    //                logEntries["Ietration"] = queueModel.Status.Automation.Iteration;
    //                logEntries["Runtime"] = $"{queueModel.Status.Automation.Reference.Id}_{queueModel.Status.Automation.Iteration}";
    //                logEntries["RuleRefernce"] = e.Rule.Reference.Id;

    //                // Check if the rule has a parent reference
    //                if (e.Rule.Reference?.ParentReference != null)
    //                {
    //                    // Add parent rule information to the event arguments
    //                    logEntries["RuleParent"] = e.Rule.Reference.ParentReference.Name;
    //                    logEntries["RuleParentReference"] = e.Rule.Reference.ParentReference.Id;
    //                }

    //                // Invoke the LogCreating event on the client, passing the sender and event arguments
    //                client.LogCreating?.Invoke(sender: client, e: logEntries);
    //            };

    //            // Handle the LogCreated event
    //            pluginLogger.LogCreated += (_, logEntries) => client.LogCreated?.Invoke(sender: client, e: logEntries);

    //            // Handle the LogError event
    //            pluginLogger.LogError += (_, logEntries) => client.LogError?.Invoke(sender: client, e: logEntries);

    //            // Invoke the PluginCreated event on the client, passing the sender and event arguments
    //            client.PluginCreated?.Invoke(sender: client, e);
    //        };
    //    }

    //    // Creates a new instance of the AutomationInvoker class and sets up event handlers for various automation events
    //    private static void NewStatusEvents(AutomationAsyncClient client, AutomationInvoker invoker, AutomationQueueModel queueModel)
    //    {
    //        // When an automation is about to be invoked, register its status model with the client
    //        invoker.AutomationInvoking += (_, args) => RegisterStatus(client, queueModel, args);

    //        // When a stage within the automation is about to be invoked, register its status model with the client
    //        invoker.StageInvoking += (_, args) => RegisterStatus(client, args);

    //        // When a job within a stage is about to be invoked, register its status model with the client
    //        invoker.JobInvoking += (_, args) => RegisterStatus(client, args);

    //        // When a rule (action) within a job is about to be invoked, register its status model with the client
    //        invoker.RuleInvoking += (_, args) => RegisterStatus(client, args);

    //        // When a rule (action) within a job has been invoked and completed, update its status model
    //        invoker.RuleInvoked += (_, args) => CompleteStatus(client, args);

    //        // When a job within a stage has been invoked and completed, update its status model
    //        invoker.JobInvoked += (_, args) => CompleteStatus(client, args);

    //        // When a stage within the automation has been invoked and completed, update its status model
    //        invoker.StageInvoked += (_, args) => CompleteStatus(client, args);

    //        // When the entire automation has been invoked and completed, update its status model
    //        invoker.AutomationInvoked += (_, args) => CompleteStatus(client, args);
    //    }

    //    // Registers a new rule status model for the specified rule and updates the queue status.
    //    private static void RegisterStatus(AutomationAsyncClient client, RuleEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference
    //        var reference = args.Automation.Reference.Id;

    //        // Get the stage reference, job reference, and rule reference
    //        var stageId = args.Rule.Reference.JobReference.StageReference.Id;
    //        var jobId = args.Rule.Reference.JobReference.Id;
    //        var ruleId = args.Rule.Reference.Id;

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Retrieve the stage status for the specified stage ID
    //        var stageStatus = queueStatus.StagesStatus[stageId];

    //        // Retrieve the job status for the specified job ID
    //        var jobStatus = stageStatus.JobsStatus[jobId];

    //        // Decrement the count of pending rules in the job status
    //        jobStatus.PendingRules = jobStatus.PendingRules <= 0
    //            ? 0
    //            : jobStatus.PendingRules - 1;

    //        // Create a new rule status model with Processing status and add it to the job status
    //        jobStatus.RulesStatus[ruleId] = G4QueueStatusModel.PluginStatusModel.New(args.Rule, G4QueueModel.QueueStatusCodes.Processing);

    //        // Raise the RuleStatusChanged event with the updated queue status, stage status, job status, and rule status
    //        client.RuleStatusChanged?.Invoke(sender: client, e: new RuleStatusEventArgs
    //        {
    //            JobStatus = jobStatus,
    //            QueueStatus = queueStatus,
    //            Reference = ruleId,
    //            RuleStatus = jobStatus.RulesStatus[ruleId],
    //            StageStatus = stageStatus
    //        });
    //    }

    //    // Registers a new job status model for the specified job and updates the queue and stage statuses.
    //    private static void RegisterStatus(AutomationAsyncClient client, JobEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference, stage reference, and job reference
    //        var reference = args.Automation.Reference.Id;
    //        var stageId = args.Stage.Reference.Id;
    //        var jobId = args.Job.Reference.Id;

    //        // Calculate the total number of rules for the job
    //        var rules = args.Job.GetRulesCount();

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Retrieve the stage status for the specified stage ID
    //        var stageStatus = queueStatus.StagesStatus[stageId];

    //        // Decrement the count of pending jobs in the stage status
    //        stageStatus.PendingJobs = stageStatus.PendingJobs <= 0
    //            ? 0
    //            : stageStatus.PendingJobs - 1;

    //        // Initialize a new job status model for the job
    //        stageStatus.JobsStatus[jobId] = new G4QueueStatusModel.JobStatusModel
    //        {
    //            CompletedRules = 0,
    //            Description = args.Stage.Description,
    //            Name = args.Stage.Name,
    //            PendingRules = rules,
    //            Progress = 0,
    //            Id = stageId,
    //            RulesStatus = [],
    //            Status = G4QueueModel.QueueStatusCodes.Processing,
    //            TotalRules = rules
    //        };

    //        // Raise the JobStatusChanged event with the updated queue status, stage status, and job status
    //        client.JobStatusChanged?.Invoke(sender: client, e: new JobStatusEventArgs
    //        {
    //            JobStatus = stageStatus.JobsStatus[jobId],
    //            QueueStatus = queueStatus,
    //            StageStatus = stageStatus
    //        });
    //    }

    //    // Registers a new stage model for the specified automation and updates the queue status.
    //    private static void RegisterStatus(AutomationAsyncClient client, StageEventArgs args)
    //    {
    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference and stage reference
    //        var reference = args.Automation.Reference.Id;
    //        var stageId = args.Stage.Reference.Id;

    //        // Retrieve the queue status for the specified group and reference
    //        var queueStatus = client._queueManager.Active[groupId][reference].Status.ProgressStatus;

    //        // Calculate the total number of jobs in the stage
    //        var jobs = args.Stage.Jobs.Count();

    //        // Calculate the total number of rules in the stage
    //        var rules = args.Stage.GetRulesCount();

    //        // Initialize jobs status dictionary for the stage
    //        var jobsStatus = new ConcurrentDictionary<string, G4QueueStatusModel.JobStatusModel>();

    //        // Decrement the count of pending stages in the queue status
    //        queueStatus.PendingStages = queueStatus.PendingStages <= 0
    //            ? 0
    //            : queueStatus.PendingStages - 1;

    //        // Create and initialize a new stage status model
    //        queueStatus.StagesStatus[stageId] = new G4QueueStatusModel.StageStatusModel
    //        {
    //            CompletedJobs = 0,
    //            CompletedRules = 0,
    //            Description = args.Stage.Description,
    //            JobsStatus = jobsStatus,
    //            Name = args.Stage.Name,
    //            PendingJobs = jobs,
    //            PendingRules = rules,
    //            Progress = 0,
    //            Id = stageId,
    //            Status = G4QueueModel.QueueStatusCodes.Processing,
    //            TotalJobs = jobs,
    //            TotalRules = rules
    //        };

    //        // Invoke the StageStatusChanged event with the updated queue status and stage status
    //        client.StageStatusChanged?.Invoke(sender: client, e: new()
    //        {
    //            QueueStatus = queueStatus,
    //            StageStatus = queueStatus.StagesStatus[stageId]
    //        });
    //    }

    //    // Registers a new status model for the specified queue model and updates the status to indicate it is running.
    //    private static void RegisterStatus(AutomationAsyncClient client, AutomationQueueModel queueModel, AutomationInvokingEventArgs args)
    //    {
    //        // Creates a new queue status model for the specified automation.
    //        static G4QueueStatusModel NewQueueStatusModel(G4AutomationModel automation)
    //        {
    //            // Get the reference to the automation
    //            var automationReference = automation.Reference;

    //            // Calculate the total number of stages in the automation
    //            var stages = automation.Stages.Count();

    //            // Calculate the total number of jobs in the automation
    //            var jobs = automation.Stages.SelectMany(i => i.Jobs).Count();

    //            // Calculate the total number of rules in the automation
    //            var rules = automation.Stages.Sum(i => i.GetRulesCount());

    //            // Initialize stages status dictionary
    //            var stagesStatus = new ConcurrentDictionary<string, G4QueueStatusModel.StageStatusModel>();

    //            // Create and initialize a new queue status model
    //            return new G4QueueStatusModel()
    //            {
    //                CompletedJobs = 0,
    //                CompletedRules = 0,
    //                CompletedStages = 0,
    //                Description = automationReference.Description,
    //                Name = automationReference.Name,
    //                PendingJobs = jobs,
    //                PendingRules = rules,
    //                PendingStages = stages,
    //                Progress = 0,
    //                Id = automationReference.Id,
    //                StagesStatus = stagesStatus,
    //                TotalJobs = jobs,
    //                TotalRules = rules,
    //                TotalStages = stages
    //            };
    //        }

    //        // Get the group ID
    //        var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
    //            ? args.Automation.Reference.Id
    //            : args.Automation.GroupId;

    //        // Get the automation reference
    //        var reference = args.Automation.Reference.Id;

    //        // If the group ID does not exist in the status dictionary, create a new one
    //        if (!client._queueManager.Active.TryGetValue(groupId, out ConcurrentDictionary<string, AutomationQueueModel> value))
    //        {
    //            value = [];
    //            client._queueManager.Active[groupId] = value;
    //        }

    //        // Initialize the queue status for the queue model's automation
    //        queueModel.Status.ProgressStatus = NewQueueStatusModel(queueModel.Status.Automation);

    //        // Update the status dictionary with the new queue status model
    //        value[reference] = queueModel;

    //        // Update the status of the queue to indicate it is running
    //        value[reference].Status.ProgressStatus.StatusCode = G4QueueModel.QueueStatusCodes.Processing;

    //        // Raise AutomationStatusChanged event with the updated queue status
    //        client.AutomationStatusChanged?.Invoke(sender: client, e: new()
    //        {
    //            GroupId = groupId,
    //            Reference = reference,
    //            QueueStatus = queueModel.Status.ProgressStatus
    //        });
    //    }
    //    #endregion
    //}
}
