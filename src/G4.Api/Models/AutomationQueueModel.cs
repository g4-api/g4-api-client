using G4.Plugins.Engine;

using System.Text.Json.Serialization;
using System.Threading;

namespace G4.Models
{
    /// <summary>
    /// Represents a model for an automation queue.
    /// </summary>
    /// <param name="invoker">The automation invoker used to initialize the instance.</param>
    /// <param name="status">The queue model used to initialize the instance.</param>
    /// <param name="cancellationTokenSource">The cancellation token source used for cancelling operations.</param>
    public sealed class AutomationQueueModel(AutomationInvoker invoker, G4QueueModel status, CancellationTokenSource cancellationTokenSource)
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationQueueModel"/> class with default values.
        /// </summary>
        public AutomationQueueModel() : this(
            invoker: new AutomationInvoker(),
            status: new G4QueueModel(),
            cancellationTokenSource: new CancellationTokenSource())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationQueueModel"/> class with a specified queue model.
        /// </summary>
        /// <param name="status">The queue model used to initialize the instance.</param>
        public AutomationQueueModel(G4QueueModel status) : this(
            invoker: new AutomationInvoker(status.Automation),
            status,
            cancellationTokenSource: new CancellationTokenSource())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutomationQueueModel"/> class with specified invoker and queue model.
        /// </summary>
        /// <param name="invoker">The automation invoker used to initialize the instance.</param>
        /// <param name="status">The queue model used to initialize the instance.</param>
        public AutomationQueueModel(AutomationInvoker invoker, G4QueueModel status)
            : this(invoker, status, cancellationTokenSource: new CancellationTokenSource())
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the cancellation token source used for cancelling operations.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public CancellationTokenSource CancellationTokenSource { get; set; } = cancellationTokenSource;

        /// <summary>
        /// Gets the automation invoker.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public IAutomationInvoker Invoker { get; set; } = invoker;

        /// <summary>
        /// Gets or sets the automation item status.
        /// </summary>
        public G4QueueModel Status { get; set; } = status;
        #endregion
    }
}
