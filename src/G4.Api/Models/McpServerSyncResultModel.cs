using System;
using System.Collections.Generic;

namespace G4.Api.Models
{
    /// <summary>
    /// Describes the outcome of synchronizing the plugins of one Model Context Protocol (MCP) server into the cache.
    /// </summary>
    /// <remarks>
    /// One result is returned per configured server, so a caller can report a dead or slow server by name while the
    /// plugins of every other server are still synchronized.
    /// </remarks>
    public class McpServerSyncResultModel
    {
        /// <summary>
        /// Gets or sets why the server was not synchronized, or <c>null</c> when it was.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server answered and its plugins were synchronized.
        /// </summary>
        public bool IsSynced { get; set; }

        /// <summary>
        /// Gets or sets the configured name (key) of the server.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the number of the server's plugins the engine added to the cache; 0 when it was not
        /// synchronized.
        /// </summary>
        public int PluginCount { get; set; }

        /// <summary>
        /// Gets or sets the server's plugins the engine rejected, by plugin key, with the reason (a key or alias that
        /// conflicts with a cached plugin, or documentation the engine could not render).
        /// </summary>
        public IDictionary<string, string> RejectedPlugins { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the server URL, as configured.
        /// </summary>
        public string Url { get; set; }
    }
}
