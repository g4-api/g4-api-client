/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace G4.IntegrationTests.Framework
{
    /// <summary>
    /// Represents an automation environment for testing purposes.
    /// </summary>
    public class AutomationEnvironment
    {
        /// <summary>
        /// Initializes a new instance of the AutomationEnvironment class.
        /// </summary>
        public AutomationEnvironment()
            : this(context: default)
        { }

        /// <summary>
        /// Initializes a new instance of the AutomationEnvironment class with the provided TestContext.
        /// </summary>
        /// <param name="context">The TestContext object that provides information about the current test run.</param>
        public AutomationEnvironment(TestContext context)
        {
            // Setup default dictionaries for context properties, test data, and test parameters
            ContextProperties ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            TestData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            TestParameters ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Synchronize the context properties with the provided TestContext
            foreach (var key in context?.Properties.Keys)
            {
                ContextProperties[$"{key}"] = context.Properties[key];
            }

            // Set the TestContext property
            TestContext = context;
        }

        /// <summary>
        /// Gets or sets the dictionary of context properties.
        /// </summary>
        public IDictionary<string, object> ContextProperties { get; }

        /// <summary>
        /// Gets or sets the TestContext object that provides information about the current test run.
        /// </summary>
        [JsonIgnore, Newtonsoft.Json.JsonIgnore]
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of test data.
        /// </summary>
        public IDictionary<string, object> TestData { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of test parameters.
        /// </summary>
        public IDictionary<string, object> TestParameters { get; set; }
    }
}
