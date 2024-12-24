using G4.Api.Abstractions;

using System.Collections.Generic;
using System.Threading;

namespace G4.Models
{
    /// <summary>
    /// Represents the event arguments for automation callbacks.
    /// </summary>
    public class AutomationCallbackEventArgs
    {
        /// <summary>
        /// Gets or sets the event arguments for the automation invoked event.
        /// </summary>
        public AutomationEventArgs AutomationEventArgs { get; set; }

        /// <summary>
        /// Gets or sets the cancellation token source used to cancel the automation.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Gets or sets the client managing the automation.
        /// </summary>
        public IAutomationClient Client { get; set; }

        /// <summary>
        /// Gets or sets the collection of automation queue models.
        /// </summary>
        public IEnumerable<AutomationQueueModel> Queue { get; set; }
    }
}
