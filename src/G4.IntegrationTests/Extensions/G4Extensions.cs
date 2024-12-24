/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using G4.Extensions;
using G4.IntegrationTests.Framework;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace G4.IntegrationTests.Extensions
{
    /// <summary>
    /// Provides extension methods for working with framework components in integration tests.
    /// </summary>
    internal static class G4Extensions
    {
        /// <summary>
        /// Adds a context parameter to the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        /// <param name="key">The key of the context parameter.</param>
        /// <param name="value">The value of the context parameter.</param>
        /// <returns>The automation environment with the added context parameter.</returns>
        public static AutomationEnvironment AddContextParameter(
            this AutomationEnvironment environment, string key, object value)
        {
            // If the environment is null, create a new instance of AutomationEnvironment
            environment ??= new AutomationEnvironment();

            // Add the test parameter to the environment's ContextProperties dictionary
            environment.ContextProperties[key] = value;

            // Return the updated environment
            return environment;
        }

        /// <summary>
        /// Adds a test parameter to the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        /// <param name="key">The key of the test parameter.</param>
        /// <param name="value">The value of the test parameter.</param>
        /// <returns>The automation environment with the added test parameter.</returns>
        public static AutomationEnvironment AddTestParameter(
            this AutomationEnvironment environment, string key, object value)
        {
            // If the environment is null, create a new instance of AutomationEnvironment
            environment ??= new AutomationEnvironment();

            // Add the test parameter to the environment's TestParameters dictionary
            environment.TestParameters[key] = value;

            // Return the updated environment
            return environment;
        }

        /// <summary>
        /// Asserts whether the extraction responses have all evaluations set to true.
        /// </summary>
        /// <param name="responses">The collection of extraction response models.</param>
        /// <returns>True if all evaluations are true; otherwise, false.</returns>
        public static bool Assert(this IEnumerable<G4ResponseModel> responses)
        {
            // Check if all response has null extractions.
            if (responses.All(i => i.ResponseData.Extractions == null))
            {
                return false;
            }

            // Check if all extraction entities have valid evaluations set to true.
            return responses
                .SelectMany(i => i?.ResponseData.Extractions)
                .Where(i => i != null)
                .SelectMany(i => i.Entities)
                .SelectMany(i => i.Content)
                .Where(i => i.Key.Equals("Evaluation", StringComparison.OrdinalIgnoreCase))
                .All(i => i.Value is bool evaluation && evaluation);
        }

        /// <summary>
        /// Asserts inconclusive results based on extraction responses.
        /// </summary>
        /// <param name="responses">The collection of extraction response models.</param>
        public static void AssertInconclusive(this IEnumerable<G4ResponseModel> responses)
        {
            // Get all exception messages from the response's G4Request exceptions.
            var exceptions = responses
                .SelectMany(i => i.ResponseData.Exceptions)
                .Select(i => i.Exception.Message);

            // If any extraction contains evaluation content, exit the method.
            if (!responses.All(i => i.ResponseData.Extractions == null))
            {
                return;
            }

            // Serialize the exception messages to a JSON string.
            var message = JsonSerializer.Serialize(exceptions);

            // Throw an AssertInconclusiveException with the serialized exception messages.
            throw new AssertInconclusiveException(message);
        }

        /// <summary>
        /// Filters the performance points by plugin name and returns the matching points as a dictionary.
        /// </summary>
        /// <typeparam name="T">The type of performance point.</typeparam>
        /// <param name="points">The dictionary of performance points.</param>
        /// <param name="pluginName">The name of the plugin to filter by.</param>
        /// <returns>The filtered performance points of type T as a dictionary.</returns>
        public static IDictionary<string, T> FilterByPluginName<T>(
            this IDictionary<string, G4PerformancePointModelBase> points, string pluginName)
            where T : G4PluginPerformancePointModel
        {
            // Define comparison type for case-insensitive comparison
            const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

            // Filter the performance points by plugin name and convert them to a dictionary
            return points
                .Where(i => ((T)i.Value).Reference.Name.Equals(pluginName, Compare) || ((T)i.Value).Reference.Alias.Equals(pluginName, Compare))
                .ToDictionary(i => i.Key, k => (T)k.Value);
        }

        /// <summary>
        /// Filters the collection of G4PerformancePointModelBase objects by plugin
        /// name and returns a filtered collection of PluginPerformancePointModel objects.
        /// </summary>
        /// <typeparam name="T">The type of PluginPerformancePointModel.</typeparam>
        /// <param name="points">The collection of G4PerformancePointModelBase objects.</param>
        /// <param name="pluginName">The name of the plugin to filter by.</param>
        /// <returns>The filtered collection of PluginPerformancePointModel objects.</returns>
        public static IEnumerable<T> FilterByPluginName<T>(this IEnumerable<G4PerformancePointModelBase> points, string pluginName)
            where T : G4PluginPerformancePointModel
        {
            // Specify the string comparison type for case-insensitive comparison.
            const StringComparison Compare = StringComparison.OrdinalIgnoreCase;

            // Filter the collection by plugin name and cast the result to the specified type.
            return points
                .Where(i => ((T)i).Reference.Name.Equals(pluginName, Compare) || ((T)i).Reference.Alias.Equals(pluginName, Compare))
                .Cast<T>();
        }

        /// <summary>
        /// Retrieves entities extracted from the responses in the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        /// <returns>An enumerable collection of <see cref="G4EntityModel"/>.</returns>
        public static IEnumerable<G4EntityModel> GetEntities(this AutomationEnvironment environment)
        {
            // Retrieve responses from the environment test parameters
            var responses = environment
                .TestParameters
                .Get(key: "Responses", defaultValue: Enumerable.Empty<G4ResponseModel>());

            // Select entities from extractions in responses and flatten the result
            return responses.SelectMany(i => i.ResponseData.Extractions).SelectMany(i => i.Entities);
        }

        /// <summary>
        /// Retrieves the collection of G4ResponseModel objects stored in the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        /// <returns>The collection of G4ResponseModel objects.</returns>
        public static IEnumerable<G4ResponseModel> GetResponses(this AutomationEnvironment environment)
        {
            // Retrieve the collection of G4ResponseModel objects from the test parameters.
            return environment
                .TestParameters
                .Get(key: "Responses", defaultValue: Enumerable.Empty<G4ResponseModel>());
        }

        /// <summary>
        /// Reads an XML document specified in the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        /// <returns>The XDocument read from the specified file, or null if the file is not found.</returns>
        public static XDocument ReadXmlDocument(this AutomationEnvironment environment)
        {
            // Check if the environment contains the key for the XML file name
            if (!environment.TestParameters.ContainsKey("FileName"))
            {
                // If the key is not found, return null
                return null;
            }

            // Read the XML file and parse it into an XDocument
            return XDocument.Parse(File.ReadAllText($"{environment.TestParameters["fileName"]}.xml"));
        }
    }
}
