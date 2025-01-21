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
        /// <inheritdoc />
        public IDictionary<string, G4AutomationResponseModel> Invoke(G4AutomationModel automation)
        {
            // Concurrent bag to store responses
            var responseCollection = new ConcurrentBag<(string GroupId, G4AutomationResponseModel Response)>();

            // A CancellationTokenSource that can be used to cancel ongoing operations in the G4Client.
            using var cancellationTokenSource = new CancellationTokenSource();

            // Generate new automations based on the provided automation model
            var automations = automation.NewAutomations();

            // Create parallel options for parallel processing
            var parallelOptions = automation.NewParallelOptions(cancellationTokenSource.Token);

            // Iterate over each automation in the collection
            var queueModels = NewAutomationRequests(client: this, automations, registerStatusEvents: true);

            var tasks = new ConcurrentBag<Task>();

            foreach (var queueModel in queueModels)
            {
                // Create a new RuleSyncCallbackEventArgs instance to store rule callback
                tasks.Add(new Task(() =>
                {
                    // arguments for the client and queue model instance
                    var ruleCallbackArgs = new RuleCallbackEventArgs
                    {
                        CancellationTokenSource = cancellationTokenSource,
                        Client = this,
                        Queue = queueModels
                    };

                    // Set up the RuleInvoked event for the invoker in the queue model to
                    // trigger the rule sync callback
                    queueModel.Invoker.RuleInvoked += (_, args) =>
                    {
                        // Update the rule callback arguments with the event arguments
                        ruleCallbackArgs.RuleEventArgs = args;

                        // Invoke the rule sync callback event on the client
                        RuleCallback?.Invoke(sender: this, e: ruleCallbackArgs);

                        // Check for cancellation request and throw if requested
                        ruleCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                    };

                    // Create a new AutomationSyncCallbackEventArgs instance to store automation callback
                    // arguments for the client and queue model instance
                    var automationCallbackArgs = new AutomationCallbackEventArgs
                    {
                        CancellationTokenSource = cancellationTokenSource,
                        Client = this,
                        Queue = queueModels
                    };

                    // Set up the AutomationInvoked event for the invoker in the queue model to
                    // trigger the automation sync callback
                    queueModel.Invoker.AutomationInvoked += (_, args) =>
                    {
                        // New response based on the queue model and automation response model
                        var response = queueModel.NewResponse(args.Response);

                        // Add the response to the collection of responses for the client to process
                        // later on completion of the automation process for the queue model instance
                        responseCollection.Add(response);

                        // Update the automation callback arguments with the event arguments
                        automationCallbackArgs.AutomationEventArgs = args;

                        // Invoke the automation sync callback event on the client
                        AutomationCallback?.Invoke(sender: this, e: automationCallbackArgs);

                        // Check for cancellation request and throw if requested
                        automationCallbackArgs.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                    };

                    // Invoke automation for the current queue model
                    queueModel.Invoker.Invoke();
                }));
            }

            // Parallel processing until all automations are processed or cancellation is requested
            try
            {
                Parallel.ForEach(tasks, parallelOptions, task =>
                {
                    task.Start();
                    task.Wait();
                });
            }
            // Silence the exception if the task was canceled by the
            // user or client code and not due to an error in the task itself
            catch (OperationCanceledException e)
            {
                Logger.LogInformation(exception: e, "The automation process was canceled by the user.");
            }
            catch (Exception e)
            {
                Logger.LogError(exception: e, "An error occurred while invoking the automation process.");
            }

            // Return the results
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
                status.Automation.Initialize(CacheManager.Instance);

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
