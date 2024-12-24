using G4.Attributes;
using G4.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace G4.Api
{
    /// <summary>
    /// Represents a manager for automation queues within the G4™ framework.
    /// </summary>
    [G4QueueManager(name: nameof(BasicQueueManager))]
    public class BasicQueueManager : IQueueManager
    {
        // A flag indicating whether the queue manager is paused.
        private bool _paused = false;

        #region *** Events           ***
        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> ModelDequeued;

        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> ModelEnqueued;

        /// <inheritdoc />
        public event EventHandler<AutomationQueueModel> ModelEnqueuing;

        /// <inheritdoc />
        public event EventHandler<QueueManagerEventArgs> OnError;
        #endregion

        #region *** Properties       ***
        /// <inheritdoc />
        public ConcurrentDictionary<string, ConcurrentDictionary<string, AutomationQueueModel>> Active { get; } = new();

        /// <inheritdoc />
        public ConcurrentQueue<AutomationQueueModel> Pending { get; } = new();

        /// <inheritdoc />
        public ConcurrentBag<G4QueueModel> Errors { get; } = [];
        #endregion

        #region *** Methods: Enqueue ***
        /// <inheritdoc />
        public void AddActive(params G4QueueModel[] queueModels)
        {
            foreach (G4QueueModel queueModel in queueModels)
            {
                //// Set the status of the queueModel to "Active".
                //queueModel.ProgressStatus.StatusCode = G4QueueModel.QueueStatusCodes.Accepted;

                //// Create an instance of QueueManagerEventArgs to provide additional information for events.
                //var eventArgs = new QueueManagerEventArgs
                //{
                //    Collection = Active,
                //    CollectionType = nameof(G4QueueModel.QueueStatusCodes.Accepted),
                //    QueueModel = queueModel
                //};

                //// Raise the ModelEnqueuing event with the pre-addition information.
                //ModelEnqueuing?.Invoke(this, eventArgs);

                //// Add the queue model to the active queue.
                //Active.Enqueue(queueModel);

                //// Update the eventArgs with the added queue model.
                //eventArgs.QueueModel = queueModel;

                //// Raise the ModelEnqueued event with the post-addition information.
                //ModelEnqueued?.Invoke(this, eventArgs);
            }
        }

        /// <inheritdoc />
        public void AddError(G4QueueModel queueModel)
        {
            // Update the status of the queueModel to indicate an error.
            queueModel.ProgressStatus.Status = G4QueueModel.QueueStatusCodes.Error;

            // Add the error to the QueueManager.
            NewError(this, queueModel);
        }

        /// <inheritdoc />
        public void AddPending(params AutomationQueueModel[] queueModels)
        {
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
        #endregion

        #region *** Methods: Dequeue ***
        /// <summary>
        /// Dequeues an automation queue model from the active queue.
        /// </summary>
        /// <returns>The dequeued automation queue model, or <c>null</c> if the dequeue operation fails.</returns>
        public G4QueueModel GetActive()
        {
            //// Create an instance of QueueManagerEventArgs to provide additional information for events.
            //var eventArgs = new QueueManagerEventArgs
            //{
            //    Collection = Active,
            //    CollectionType = nameof(G4QueueModel.QueueStatusCodes.Accepted)
            //};

            //// Raise the ModelDequeuing event with the pre-dequeue information.
            //ModelDequeuing?.Invoke(this, eventArgs);

            //// Attempt to dequeue an automation queue model from the active queue.
            //var isDequeue = Active.TryDequeue(out var queueModel);

            //// If the dequeue operation fails, raise an error event and return null.
            //if (!isDequeue)
            //{
            //    NewError(this, queueModel);
            //    return null;
            //}

            //// Update the eventArgs with the dequeued queue model.
            //eventArgs.QueueModel = queueModel;
            //eventArgs.Collection = Active;

            //// Raise the ModelDequeued event with the post-dequeue information.
            //ModelDequeued?.Invoke(this, eventArgs);

            //// Return the dequeued automation queue model.
            //return queueModel;

            return default;
        }

        /// <summary>
        /// Gets all automation queue models in the errors collection.
        /// </summary>
        /// <returns>An IEnumerable of automation queue models in the errors collection.</returns>
        public IEnumerable<G4QueueModel> GetErrors()
        {
            // The errors collection is a concurrent bag, and it doesn't require dequeuing or dequeue events.
            // Return all items in the errors collection.
            return Errors;
        }

        /// <summary>
        /// Dequeues an automation queue model from the pending collection.
        /// </summary>
        /// <returns>The dequeued automation queue model or null if the collection is empty.</returns>
        public AutomationQueueModel GetPending()
        {
            // Try to dequeue an item from the pending collection.
            var isDequeue = Pending.TryDequeue(out var queueModel);

            // If dequeueing fails, log an error and return null.
            if (!isDequeue)
            {
                NewError(this, queueModel.Status);
                return null;
            }

            // Invoke the dequeued event after a successful dequeue operation.
            ModelDequeued?.Invoke(this, queueModel);

            // Return the dequeued automation queue model.
            return queueModel;
        }
        #endregion

        #region *** Methods          ***
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
        #endregion

        // Handles the occurrence of a new error in the queue manager, adding the error to the errors collection.
        private static void NewError(BasicQueueManager queueManager, G4QueueModel queueModel)
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
    }
}
