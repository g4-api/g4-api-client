using G4.Abstraction.Logging;
using G4.Api.Abstractions;
using G4.Api.Models;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Models.Events;
using G4.Plugins;
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
    internal class AutomationClient(ILogger logger) : ClientBase, IAutomationClient
    {
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
        /// <summary>
        /// Invokes automation tasks based on the provided <see cref="G4AutomationModel"/> and returns a dictionary of automation responses.
        /// </summary>
        /// <param name="automation">The automation model containing configuration and parameters for generating new automations.</param>
        /// <returns>
        /// A dictionary mapping group IDs to their corresponding <see cref="G4AutomationResponseModel"/> responses.
        /// </returns>
        public IDictionary<string, G4AutomationResponseModel> Invoke(G4AutomationModel automation)
        {
            // Generate new automation queue models from the provided automation model.
            var automations = automation.NewAutomations();

            // Retrieve the maximum degree of parallelism from the automation settings.
            var maxParallel = automation.GetMaxParallel();

            // Create automation queue models and register status events.
            var automationQueueModels = NewAutomationRequests(client: this, automations, registerStatusEvents: true);

            // Invoke automation tasks using the created queue models and the specified maximum degree of parallelism.
            return Invoke(client: this, automationQueueModels, maxParallel);
        }

        /// <summary>
        /// Invokes automation tasks based on the provided array of queue models using default parallelism settings.
        /// </summary>
        /// <param name="queueModels">An array of <see cref="G4QueueModel"/> instances representing automation tasks.</param>
        /// <returns>A dictionary mapping group IDs to their corresponding <see cref="G4AutomationResponseModel"/> responses.</returns>
        public IDictionary<string, G4AutomationResponseModel> Invoke(params G4QueueModel[] queueModels)
        {
            // Create automation queue models from the provided queue models and register status events.
            var automationQueueModels = NewAutomationRequests(client: this, queueModels, registerStatusEvents: true);

            // Determine the default maximum degree of parallelism based on available processors.
            var maxParallel = (Environment.ProcessorCount / 2) < 1 ? 1 : Environment.ProcessorCount / 2;

            // Invoke the overload with the computed maximum degree of parallelism.
            return Invoke(client: this, automationQueueModels, maxParallel);
        }

        /// <summary>
        /// Invokes automation tasks based on the provided array of queue models with a specified maximum degree of parallelism.
        /// </summary>
        /// <param name="maxParallel">The maximum number of tasks to run concurrently.</param>
        /// <param name="queueModels">An array of <see cref="G4QueueModel"/> instances representing automation tasks.</param>
        /// <returns>A dictionary mapping group IDs to their corresponding <see cref="G4AutomationResponseModel"/> responses.</returns>
        public IDictionary<string, G4AutomationResponseModel> Invoke(int maxParallel, params G4QueueModel[] queueModels)
        {
            // Create automation queue models from the provided queue models and register status events.
            var automationQueueModels = NewAutomationRequests(client: this, queueModels, registerStatusEvents: true);

            // Invoke automation tasks based on the created automation queue models.
            return Invoke(client: this, automationQueueModels, maxParallel);
        }

        // Invokes automation tasks based on the provided array of queue models with a specified maximum degree of parallelism.
        private static IDictionary<string, G4AutomationResponseModel> Invoke(
            AutomationClient client,
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
                tasks.Add(new Task(() =>
                {
                    // Prepare rule callback arguments including cancellation token, client reference, and current queue.
                    var ruleCallbackArgs = new RuleCallbackEventArgs
                    {
                        CancellationTokenSource = cancellationTokenSource,
                        Client = client,
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
                        Client = client,
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
                }));
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
                client.Logger.LogInformation(exception: e, "The automation process was canceled by the user.");
            }
            // Log any other errors that occur during invocation.
            catch (Exception e)
            {
                client.Logger.LogError(exception: e, "An error occurred while invoking the automation process.");
            }

            // Convert the collected responses into a dictionary and return the result.
            return responseCollection.NewAutomationResponse();
        }

        // Creates a list of new automation queue models based on the provided client and automations.
        private static ConcurrentBag<AutomationQueueModel> NewAutomationRequests(
            AutomationClient client,
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

                // Initialize the automation using the cache manager
                status.Automation.Initialize(CacheManager.Instance, dataProvider);

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

        // Creates new automation queue models from the provided queue models and registers status events if specified.
        private static ConcurrentBag<AutomationQueueModel> NewAutomationRequests(
            AutomationClient client,
            IEnumerable<G4QueueModel> queueModels,
            bool registerStatusEvents)
        {
            // Initialize a concurrent collection to store the resulting automation queue models.
            var automationQueueModels = new ConcurrentBag<AutomationQueueModel>();

            // Process each queue model in the provided collection.
            for (int i = 0; i < queueModels.Count(); i++)
            {
                // Retrieve the automation instance from the current queue model.
                var automation = queueModels.ElementAt(i).Automation;

                // Create a new automation invoker using the retrieved automation instance.
                var invoker = new AutomationInvoker(automation);

                // Construct a new automation queue model using the invoker and the current queue model.
                var queueModel = new AutomationQueueModel(invoker, queueModels.ElementAt(i));

                // If registration of status events is enabled, attach the necessary event handlers.
                if (registerStatusEvents)
                {
                    RegisterInvokerEvents(client, automation, invoker);
                }

                // Invoke the AutomationRequestInitialized event on the client to notify that a new automation request has been initialized.
                client.AutomationRequestInitialized?.Invoke(sender: client, e: queueModel);

                // Add the constructed queue model to the concurrent collection.
                automationQueueModels.Add(queueModel);
            }

            // Return the collection of new automation queue models.
            return automationQueueModels;
        }

        // Registers event handlers for the invoker by wrapping the client's event handlers with exception handling and logging.
        private static void RegisterInvokerEvents(AutomationClient client, G4AutomationModel automation, AutomationInvoker invoker)
        {
            // Creates a new delegate that wraps an event handler with exception handling and logging.
            static EventHandler<T> NewDelegate<T>(AutomationClient client, EventHandler<T> eventHandler)
            {
                // Return a new Action delegate that wraps the event handler
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
        }
        #endregion
    }
}
