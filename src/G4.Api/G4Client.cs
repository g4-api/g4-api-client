using G4.Abstraction.Logging;
using G4.Api.Abstractions;
using G4.Api.Clients;
using G4.Cache;

using Microsoft.Extensions.Logging;

namespace G4.Api
{
    /// <summary>
    /// Represents the primary client for interacting with various components of the G4 system, including automations, environments, and integrations.
    /// </summary>
    /// <param name="cache">The cache manager for caching operations.</param>
    /// <param name="queueManager">The queue manager responsible for handling automation queues.</param>
    /// <param name="logger">The logger instance for logging.</param>
    public class G4Client(CacheManager cache, IQueueManager queueManager, ILogger logger)
    {
        #region *** Fields       ***
        // Manages the queue operations for handling automation processes.
        private readonly IQueueManager _queueManager = queueManager;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with default LiteDatabase, cache, queue manager, and logger.
        /// </summary>
        public G4Client()
            : this(cache: CacheManager.Instance, queueManager: new BasicQueueManager(), logger: G4Logger.Instance)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with a specified cache manager and default LiteDatabase, queue manager, and logger.
        /// </summary>
        /// <param name="cache">The cache manager for caching operations.</param>
        public G4Client(CacheManager cache)
            : this(cache: cache, queueManager: new BasicQueueManager(), logger: G4Logger.Instance)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with a specified queue manager and default LiteDatabase, cache, and logger.
        /// </summary>
        /// <param name="queueManager">The queue manager responsible for handling automation queues.</param>
        public G4Client(IQueueManager queueManager)
            : this(cache: CacheManager.Instance, queueManager, logger: G4Logger.Instance)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with a specified logger and default LiteDatabase, cache, and queue manager.
        /// </summary>
        /// <param name="logger">The logger instance for logging.</param>
        public G4Client(ILogger logger)
            : this(cache: CacheManager.Instance, queueManager: new BasicQueueManager(), logger)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with specified cache and queue manager, and default LiteDatabase and logger.
        /// </summary>
        /// <param name="cache">The cache manager for caching operations.</param>
        /// <param name="queueManager">The queue manager responsible for handling automation queues.</param>
        public G4Client(CacheManager cache, IQueueManager queueManager)
            : this(cache, queueManager, logger: G4Logger.Instance)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with specified cache and logger, and default LiteDatabase and queue manager.
        /// </summary>
        /// <param name="cache">The cache manager for caching operations.</param>
        /// <param name="logger">The logger instance for logging.</param>
        public G4Client(CacheManager cache, ILogger logger)
            : this(cache, queueManager: new BasicQueueManager(), logger)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="G4Client"/> class with specified queue manager and logger, and default LiteDatabase and cache.
        /// </summary>
        /// <param name="queueManager">The queue manager responsible for handling automation queues.</param>
        /// <param name="logger">The logger instance for logging.</param>
        public G4Client(IQueueManager queueManager, ILogger logger)
            : this(cache: CacheManager.Instance, queueManager, logger)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the automation client responsible for managing and invoking automations within the G4 system.
        /// </summary>
        public IAutomationClient Automation { get; } = new AutomationClient(logger);

        /// <summary>
        /// Gets the environments client responsible for managing different environments within the G4 system.
        /// </summary>
        public IEnvironmentsClient Environments { get; } = new EnvironmentsClient();

        /// <summary>
        /// Gets the integration client responsible for handling integrations within the G4 system.
        /// </summary>
        public IIntegrationClient Integration { get; } = new IntegrationClient();

        /// <summary>
        /// Gets the logger instance used for logging activities within the G4 client.
        /// </summary>
        public ILogger Logger { get; } = logger;

        /// <summary>
        /// Gets the template client responsible for managing plugin templates within the G4 system.
        /// </summary>
        public ITemplateClient Templates { get; } = new TemplatesClient(cache);
        #endregion
    }
}
