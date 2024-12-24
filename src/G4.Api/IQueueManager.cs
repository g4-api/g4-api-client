using G4.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace G4.Api
{
    /// <summary>
    /// Represents a manager for automation queues within the G4™ framework.
    /// </summary>
    public interface IQueueManager
    {
        #region *** Events     ***
        /// <summary>
        /// Event raised when a model is dequeued.
        /// </summary>
        event EventHandler<AutomationQueueModel> ModelDequeued;

        /// <summary>
        /// Event raised when a model is enqueued.
        /// </summary>
        event EventHandler<AutomationQueueModel> ModelEnqueued;

        /// <summary>
        /// Event raised when a model is enqueuing.
        /// </summary>
        event EventHandler<AutomationQueueModel> ModelEnqueuing;

        /// <summary>
        /// Event raised on an error.
        /// </summary>
        event EventHandler<QueueManagerEventArgs> OnError;
        #endregion

        #region *** Properties ***
        /// <summary>
        /// Gets the concurrent queue of active automation queue models.
        /// </summary>
        ConcurrentDictionary<string, ConcurrentDictionary<string, AutomationQueueModel>> Active { get; }

        /// <summary>
        /// Gets the concurrent queue of pending automation queue models.
        /// </summary>
        ConcurrentQueue<AutomationQueueModel> Pending { get; }

        /// <summary>
        /// Gets the concurrent bag of automation queue models with errors.
        /// </summary>
        ConcurrentBag<G4QueueModel> Errors { get; }
        #endregion

        #region *** Methods    ***
        /// <summary>
        /// Adds one or more automation queue models to the active collection.
        /// </summary>
        /// <param name="queueModels">The automation queue models to add to the active collection.</param>
        void AddActive(params G4QueueModel[] queueModels);

        /// <summary>
        /// Adds an automation queue model to the error queue.
        /// </summary>
        /// <param name="queueModel">The automation queue model to add.</param>
        void AddError(G4QueueModel queueModel);

        /// <summary>
        /// Adds an automation queue model to the pending queue.
        /// </summary>
        /// <param name="queueModels">The automation queue models to add to the pending collection.</param>
        void AddPending(params AutomationQueueModel[] queueModels);

        /// <summary>
        /// Gets the next automation queue model from the active queue.
        /// </summary>
        /// <returns>The next automation queue model from the active queue.</returns>
        G4QueueModel GetActive();

        /// <summary>
        /// Gets the automation queue models associated with errors for a given queue model.
        /// </summary>
        /// <returns>The automation queue models associated with errors.</returns>
        IEnumerable<G4QueueModel> GetErrors();

        /// <summary>
        /// Gets the next automation queue model from the pending queue.
        /// </summary>
        /// <returns>The next automation queue model from the pending queue.</returns>
        AutomationQueueModel GetPending();

        /// <summary>
        /// Pauses the queue manager, preventing further processing of automation queue models.
        /// </summary>
        void Pause();

        /// <summary>
        /// Resets the queue manager by removing all entries from all queues.
        /// </summary>
        void Reset();

        /// <summary>
        /// Resumes the queue manager, allowing the processing of automation queue models to continue.
        /// </summary>
        void Resume();
        #endregion
    }
}
