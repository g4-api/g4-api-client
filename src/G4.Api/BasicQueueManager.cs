using G4.Attributes;
using G4.Extensions;
using G4.Models;

using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace G4.Api
{
    /// <summary>
    /// Represents a manager for automation queues within the G4™ framework.
    /// </summary>
    [G4QueueManager(name: nameof(BasicQueueManager))]
    public class BasicQueueManager(ILogger logger) : IQueueManager
    {
        // Logger instance for logging queue manager activities.
        private readonly ILogger _logger = logger;

        // A flag indicating whether the queue manager is paused.
        private bool _paused;

        #region *** Events       ***
        /// <inheritdoc />
        public event EventHandler<G4QueueModel> ModelDequeued;

        /// <inheritdoc />
        public event EventHandler<QueueManagerEventArgs> ModelDequeuing;

        /// <inheritdoc />
        public event EventHandler<G4QueueModel> ModelEnqueued;

        /// <inheritdoc />
        public event EventHandler<G4QueueModel> ModelEnqueuing;

        /// <inheritdoc />
        public event EventHandler<QueueManagerEventArgs> OnError;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="BasicQueueManager"/> class.
        /// </summary>
        public BasicQueueManager()
            : this(logger: null)
        { }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> Active { get; } = [];

        /// <inheritdoc />
        public ConcurrentQueue<G4QueueModel> Pending { get; } = [];

        /// <inheritdoc />
        public ConcurrentBag<G4QueueModel> Errors { get; } = [];
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void AddActive(params G4QueueModel[] queueModels)
        {
            // Check if the queue manager is paused.
            if (_paused)
            {
                return;
            }

            // Iterate through each automation queue model in the collection.
            foreach (var queueModel in queueModels)
            {
                Active.Add(queueModel);
            }
        }

        /// <inheritdoc />
        public void AddError(G4QueueModel queueModel)
        {
            // Check if the queue manager is paused.
            if (_paused)
            {
                return;
            }

            // Update the status of the queueModel to indicate an error.
            queueModel.ProgressStatus.Status = G4QueueModel.QueueStatusCodes.Error;

            // Add the error to the QueueManager.
            NewError(this, queueModel);
        }

        /// <inheritdoc />
        public void AddPending(params G4QueueModel[] queueModels)
        {
            // Check if the queue manager is paused.
            if (_paused)
            {
                return;
            }

            // Iterate through each automation queue model in the collection.
            foreach (var queueModel in queueModels)
            {
                // Raise the ModelEnqueuing event with the pre-addition information.
                ModelEnqueuing?.Invoke(this, queueModel);

                // Add the queue model to the pending queue.
                Pending.Enqueue(queueModel);

                // Raise the ModelEnqueued event with the post-addition information.
                ModelEnqueued?.Invoke(this, queueModel);
            }
        }

        /// <inheritdoc />
        public G4QueueModel GetActive()
        {
            // Try to dequeue an item from the active collection.
            Active.TryDequeue(out var queueModel);

            // Return the dequeued automation queue model.
            return queueModel;
        }

        /// <inheritdoc />
        public IEnumerable<G4QueueModel> GetErrors()
        {
            // The errors collection is a concurrent bag, and it doesn't require dequeuing or dequeue events.
            // Return all items in the errors collection.
            return Errors;
        }

        /// <inheritdoc />
        public G4QueueModel GetPending()
        {
            // Check if the pending collection is empty.
            if (Pending?.IsEmpty == true)
            {
                return null;
            }

            // Try to dequeue an item from the pending collection.
            var isDequeue = Pending.TryDequeue(out var queueModel);

            // If dequeueing fails, log an error and return null.
            if (!isDequeue)
            {
                NewError(this, queueModel);
                return null;
            }

            // Invoke the dequeued event after a successful dequeue operation.
            ModelDequeued?.Invoke(this, queueModel);

            // Return the dequeued automation queue model.
            return queueModel;
        }

        /// <inheritdoc />
        public void Pause()
        {
            // Check if the queue manager is not already paused.
            if (!_paused)
            {
                // Set the paused flag to true, indicating the queue manager is paused.
                _paused = true;
            }
        }

        /// <inheritdoc />
        public void Reset()
        {
            // Lock to ensure thread safety while clearing the active queue.
            lock (Active)
            {
                Active.Clear();
            }

            // Lock to ensure thread safety while clearing the error queue.
            lock (Errors)
            {
                Errors.Clear();
            }

            // Lock to ensure thread safety while clearing the pending queue.
            lock (Pending)
            {
                Pending.Clear();
            }
        }

        /// <inheritdoc />
        public void Resume()
        {
            // Check if the queue manager is currently paused.
            if (_paused)
            {
                // Set the paused flag to false, indicating the queue manager is resumed.
                _paused = false;
            }
        }

        /// <inheritdoc />
        public void UpdateActive(G4QueueModel queueModel)
        {
            // Check if the queue manager is paused.
            if (_paused)
            {
                return;
            }

            // Update the active queue model.
            Active.Update(queueModel);
        }

        /// <inheritdoc />
        public void UpdateActive(string group, string id, G4QueueModel queueModel)
        {
            // Check if the queue manager is paused.
            if (_paused)
            {
                return;
            }

            // Update the active queue model.
            Active.Update(group, id, queueModel);
        }
        #endregion

        // Handles the occurrence of a new error in the queue manager, adding the error to the errors collection.
        private static void NewError(BasicQueueManager queueManager, G4QueueModel queueModel)
        {
            try
            {
                // Create a new event args instance for the error.
                var eventArgs = new QueueManagerEventArgs
                {
                    Collection = queueManager.Errors,
                    CollectionType = nameof(G4QueueModel.QueueStatusCodes.Error),
                    QueueModel = queueModel
                };

                // Invoke the OnError event, notifying subscribers about the error.
                queueManager.OnError?.Invoke(queueManager, eventArgs);

                // Add the error to the errors collection.
                queueManager.Errors.Add(queueModel);
            }
            catch (Exception e)
            {
                queueManager
                    ._logger?
                    .LogError(e, "An error occurred while handling a new error in the queue manager.");
            }
        }
    }
}
