/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System;

namespace G4.IntegrationTests.Framework.Attributes
{
    /// <summary>
    /// Attribute used to specify acceptance criteria for test methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    internal sealed class AcceptanceCriteriaAttribute(string criteria) : Attribute
    {
        /// <summary>
        /// Gets the acceptance criteria for the test method.
        /// </summary>
        public string Criteria { get; set; } = criteria;
    }
}
