/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System;

namespace G4.IntegrationTests.Framework.Attributes
{
    /// <summary>
    /// Attribute used to associate a user story with a test class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    internal sealed class UserStoryAttribute(string story) : Attribute
    {
        /// <summary>
        /// Gets or sets the user story associated with the test class.
        /// </summary>
        public string Story { get; set; } = story;
    }
}
