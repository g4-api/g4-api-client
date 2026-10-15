using G4.Api.Abstractions;
using G4.Models;

using System.Collections.Generic;
using System.Threading;

namespace G4.Api.Models
{
    /// <summary>
    /// Represents the event arguments for rule callbacks.
    /// </summary>
    public class RuleCallbackEventArgs
    {
        /// <summary>
        /// Gets or sets the cancellation token source used to cancel the rule execution.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Gets or sets the client managing the automation.
        /// </summary>
        public IAutomationClient Client { get; set; }

        /// <summary>
        /// Gets or sets the flows client managing the automation when the callback comes from a flow invocation.
        /// </summary>
        /// <remarks>
        /// Set only for flow invocations, where <see cref="Client"/> is <c>null</c>; null for plain automation invocations.
        /// </remarks>
        public IFlowsClient FlowsClient { get; set; }

        /// <summary>
        /// Gets or sets the collection of automation queue models.
        /// </summary>
        public IEnumerable<AutomationQueueModel> Queue { get; set; }

        /// <summary>
        /// Gets or sets the event arguments for the rule event.
        /// </summary>
        public RuleEventArgs RuleEventArgs { get; set; }
    }
}
