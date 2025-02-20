using G4.Extensions;
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
        event EventHandler<G4QueueModel> ModelDequeued;

        /// <summary>
        /// Event raised right before a model is dequeued.
        /// </summary>
        event EventHandler<QueueManagerEventArgs> ModelDequeuing;

        /// <summary>
        /// Event raised when a model is enqueued.
        /// </summary>
        event EventHandler<G4QueueModel> ModelEnqueued;

        /// <summary>
        /// Event raised when a model is enqueuing.
        /// </summary>
        event EventHandler<G4QueueModel> ModelEnqueuing;

        /// <summary>
        /// Event raised on an error.
        /// </summary>
        event EventHandler<QueueManagerEventArgs> OnError;
        #endregion

        #region *** Properties ***
        /// <summary>
        /// Gets the concurrent queue of active automation queue models.
        /// </summary>
        ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> Active { get; }

        /// <summary>
        /// Gets the concurrent queue of pending automation queue models.
        /// </summary>
        ConcurrentQueue<G4QueueModel> Pending { get; }

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
        void AddPending(params G4QueueModel[] queueModels);

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
        G4QueueModel GetPending();

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

        /// <summary>
        /// Updates the active queue model with the provided <see cref="G4QueueModel"/>.
        /// </summary>
        /// <param name="queueModel">The new <see cref="G4QueueModel"/> to update the active queue with.</param>
        void UpdateActive(G4QueueModel queueModel);

        /// <summary>
        /// Updates the active queue model for the specified group and identifier with the provided <see cref="G4QueueModel"/>.
        /// </summary>
        /// <param name="group">The group identifier for the queue model.</param>
        /// <param name="id">The unique identifier of the queue model.</param>
        /// <param name="queueModel">The new <see cref="G4QueueModel"/> to update the active queue with.</param>
        void UpdateActive(string group, string id, G4QueueModel queueModel);
        #endregion
    }
}
