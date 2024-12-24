using System.Collections.Generic;

namespace G4.Models.Events
{
    /// <summary>
    /// Represents the arguments for a log event in the automation system.
    /// </summary>
    public class LogEventArgs
    {
        /// <summary>
        /// Gets or sets the automation identifier associated with the log event.
        /// </summary>
        public string Automation { get; set; }

        /// <summary>
        /// Gets or sets the automation group associated with the log event.
        /// </summary>
        public string AutomationGroup { get; set; }

        /// <summary>
        /// Gets or sets the invoker of the log event.
        /// </summary>
        public string Invoker { get; set; }

        /// <summary>
        /// Gets or sets the log message.
        /// </summary>
        public IDictionary<string, object> LogMessage { get; set; }
    }
}
