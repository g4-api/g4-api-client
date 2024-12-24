using G4.Models;
using G4.Plugins;

using System;
using System.Collections.Generic;
using System.Linq;

using static G4.UnitTests.Framework.TestBase;

namespace G4.UnitTests.Extensions
{
    /// <summary>
    /// Provides extension methods for the PluginBase class.
    /// </summary>
    internal static class PluginExtensions
    {
        // Define the constant keys used for accessing values in dictionaries
        private const string EvaluationKey = "Evaluation"; // Key for evaluation value
        private const string ActualKey = "Actual";         // Key for actual message
        private const string ExpectedKey = "Expected";     // Key for expected message

        /// <summary>
        /// Gets the actual and expected messages from the plugin.
        /// </summary>
        /// <param name="plugin">The plugin to get messages from.</param>
        /// <returns>A tuple containing the actual message and expected message as strings.</returns>
        public static (string ActualMessage, string ExpectedMessage) GetAssertMessages(this PluginBase plugin)
        {
            // Extract content from the plugin's entities
            var entity = plugin.Extractions.First().Entities.First().Content;

            // Create a dictionary for easy access to content with case-insensitive keys
            var content = new Dictionary<string, object>(entity, StringComparer.OrdinalIgnoreCase);

            // Get the actual message from the content, or an empty string if not found
            var actualMessage = content.TryGetValue(ActualKey, out object actualMessageOut)
                ? actualMessageOut.ToString()
                : string.Empty;

            // Get the expected message from the content, or an empty string if not found
            var expectedMessage = content.TryGetValue(ExpectedKey, out object expectedMessageOut)
                ? expectedMessageOut.ToString()
                : string.Empty;

            // Return a tuple containing the actual and expected messages
            return (actualMessage, expectedMessage);
        }

        /// <summary>
        /// Retrieves the actual and expected messages from a collection of <see cref="G4EntityModel"/> entities.
        /// </summary>
        /// <param name="entities">An enumerable collection of <see cref="G4EntityModel"/> entities.</param>
        /// <returns>
        /// A tuple containing the actual and expected messages as strings:
        /// <c>ActualMessage</c>—the actual message.
        /// <c>ExpectedMessage</c>—the expected message.
        /// </returns>
        public static (string ActualMessage, string ExpectedMessage) GetAssertMessages(this IEnumerable<G4ExtractionModel> entities)
        {
            // Get the content of the first entity in the collection
            var entity = entities.First().Entities.First().Content;

            // Create a dictionary from the entity content with case-insensitive keys
            var content = new Dictionary<string, object>(entity, StringComparer.OrdinalIgnoreCase);

            // Try to retrieve the actual message; if not found, use an empty string
            var actualMessage = content.TryGetValue(ActualKey, out object actualMessageOut)
                ? actualMessageOut.ToString()
                : string.Empty;

            // Try to retrieve the expected message; if not found, use an empty string
            var expectedMessage = content.TryGetValue(ExpectedKey, out object expectedMessageOut)
                ? expectedMessageOut.ToString()
                : string.Empty;

            // Return the actual and expected messages as a tuple
            return (actualMessage, expectedMessage);
        }

        /// <summary>
        /// Gets the environment context from a test result model.
        /// </summary>
        /// <param name="testResult">The test result containing the responses and sessions.</param>
        /// <returns>The environment context model of the first response's first session.</returns>
        public static EnvironmentContextModel GetEnvironment(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult)
        {
            // Retrieve the first response model from the test result.
            // The test result contains a dictionary of responses, we take the first one.
            var response = testResult.Response.First().Value;

            // Get the first session from the first response model.
            var session = response.Sessions.First().Value;

            // Return the environment context from the first session.
            return session.Environment;
        }

        /// <summary>
        /// Gets the evaluation result from the plugin.
        /// </summary>
        /// <param name="testResult">The test results to get the evaluation result from.</param>
        /// <returns>The evaluation result as a boolean value.</returns>
        public static bool GetEvaluation(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult)
        {
            // Retrieve the first response model from the test result.
            // The test result contains a dictionary of responses, we take the first one.
            var response = testResult.Response.First().Value;

            // Get the first entity from the first session in the first response model.
            var entity = response.Sessions.First().Value.ResponseData.Extractions.First().Entities.First().Content;

            // Create a dictionary for easy access to content with case-insensitive keys
            var content = new Dictionary<string, object>(entity, StringComparer.OrdinalIgnoreCase);

            // Get the actual evaluation value from the content
            var actual = content[EvaluationKey];

            // Try to parse the actual value as a boolean, default to false if parsing fails
            _ = bool.TryParse($"{actual}", out bool actualOut);

            // Return the parsed boolean value as the evaluation result
            return actualOut;
        }

        /// <summary>
        /// Gets the evaluation result from the plugin.
        /// </summary>
        /// <param name="plugin">The plugin to get the evaluation result from.</param>
        /// <returns>The evaluation result as a boolean value.</returns>
        public static bool GetEvaluation(this PluginBase plugin)
        {
            // Extract content from the plugin's entities
            var entity = plugin.Extractions.First().Entities.First().Content;

            // Create a dictionary for easy access to content with case-insensitive keys
            var content = new Dictionary<string, object>(entity, StringComparer.OrdinalIgnoreCase);

            // Get the actual evaluation value from the content
            var actual = content[EvaluationKey];

            // Try to parse the actual value as a boolean, default to false if parsing fails
            _ = bool.TryParse($"{actual}", out bool actualOut);

            // Return the parsed boolean value as the evaluation result
            return actualOut;
        }

        /// <summary>
        /// Retrieves the evaluation result from a collection of <see cref="G4ExtractionModel"/>.
        /// </summary>
        /// <param name="extractions">A collection of <see cref="G4ExtractionModel"/> containing extraction data.</param>
        /// <returns><c>true</c> if the evaluation result is successfully parsed as <see cref="bool"/>; otherwise, <c>false</c>.</returns>
        public static bool GetEvaluation(this IEnumerable<G4ExtractionModel> extractions)
        {
            // Retrieve the content dictionary from the first entity of the first extraction.
            var entity = extractions.First().Entities?.FirstOrDefault()?.Content;

            // If the entity is null, return false.
            if (entity is null)
            {
                return false;
            }

            // Initialize a case-insensitive dictionary using the entity's content.
            var content = new Dictionary<string, object>(entity, StringComparer.OrdinalIgnoreCase);

            // Retrieve the value associated with the specified evaluation key.
            var actual = content[EvaluationKey];

            // Attempt to parse the retrieved value as a boolean.
            // The use of string interpolation ensures that the value is converted to a string before parsing.
            _ = bool.TryParse($"{actual}", out bool actualOut);

            // Return the parsed boolean value.
            return actualOut;
        }

        /// <summary>
        /// Retrieves the collection of exceptions from the first session in the test result.
        /// </summary>
        /// <param name="testResult">The test result containing the response models.</param>
        /// <returns>A collection of <see cref="G4ExceptionModel"/> representing the exceptions encountered during the test.</returns>
        public static IEnumerable<G4ExceptionModel> GetExceptions(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult)
        {
            // Retrieve the collection of exceptions from the first session in the first response model.
            return testResult
                .Response
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .Exceptions;
        }

        /// <summary>
        /// Retrieves the value of a specified parameter from the test result based on the provided scope.
        /// </summary>
        /// <param name="testResult">The test result containing the dictionary of <see cref="G4AutomationResponseModel"/>.</param>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <param name="scope">
        /// The scope from which to retrieve the parameter value (e.g., SESSION, APPLICATION, USER, MACHINE, PROCESS).
        /// </param>
        /// <returns>The value of the specified parameter if found; otherwise, an empty string.</returns>
        public static string GetParameterValue(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult,
            string parameterName,
            string scope)
        {
            // Retrieve the first response model from the test result.
            var responseModel = testResult.Response.First().Value.Sessions.First().Value;

            // Use the existing GetParameterValue method to retrieve the parameter value based on the scope.
            return GetParameterValue(responseModel, parameterName, scope);
        }

        /// <summary>
        /// Retrieves the value of a specified parameter from the given response model based on the provided scope.
        /// </summary>
        /// <param name="responseModel">The response model containing the parameters.</param>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <param name="scope">The scope from which to retrieve the parameter value (e.g., SESSION, APPLICATION, USER, MACHINE, PROCESS).</param>
        /// <returns>The value of the specified parameter if found; otherwise, an empty string.</returns>
        public static string GetParameterValue(this G4ResponseModel responseModel, string parameterName, string scope)
        {
            return GetParameterValue(responseModel, environmentName: "SystemParameters", parameterName, scope);
        }

        /// <summary>
        /// Retrieves the value of a parameter from the specified scope within the test result.
        /// </summary>
        /// <param name="testResult">The test result containing the response models.</param>
        /// <param name="environmentName">The name of the environment (used for application scope).</param>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <param name="scope">The scope from which to retrieve the parameter value (e.g., SESSION, APPLICATION, USER, MACHINE, PROCESS).</param>
        /// <returns>The value of the specified parameter, or an empty string if the parameter cannot be retrieved.</returns>
        public static string GetParameterValue(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult,
            string environmentName,
            string parameterName,
            string scope)
        {
            // Retrieve the first response model from the test result.
            var responseModel = testResult.Response.First().Value.Sessions.First().Value;

            // Call the overloaded GetParameterValue method to get the parameter value.
            return GetParameterValue(responseModel, environmentName, parameterName, scope);
        }

        /// <summary>
        /// Retrieves the value of a parameter from the specified scope within the response model.
        /// </summary>
        /// <param name="responseModel">The response model containing the environment parameters.</param>
        /// <param name="environmentName">The name of the environment (used for application scope).</param>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <param name="scope">The scope from which to retrieve the parameter value (e.g., SESSION, APPLICATION, USER, MACHINE, PROCESS).</param>
        /// <returns>The value of the specified parameter, or an empty string if the parameter cannot be retrieved.</returns>
        public static string GetParameterValue(
            this G4ResponseModel responseModel,
            string environmentName,
            string parameterName,
            string scope)
        {
            try
            {
                // Return the parameter value based on the specified scope.
                return scope.ToUpper() switch
                {
                    "SESSION" => $"{responseModel.Environment.SessionParameters[parameterName]}",
                    "APPLICATION" => $"{responseModel.Environment.ApplicationParameters[environmentName].Parameters[parameterName]}",
                    "USER" => $"{responseModel.Environment.UserParameters[parameterName]}",
                    "MACHINE" => $"{responseModel.Environment.MachineParameters[parameterName]}",
                    "PROCESS" => $"{responseModel.Environment.ProcessParameters[parameterName]}",

                    // If an unsupported scope is specified, throw a NotImplementedException.
                    _ => throw new NotImplementedException()
                };
            }
            catch
            {
                // If the parameter value cannot be retrieved, return an empty string.
                return string.Empty;
            }
        }

        /// <summary>
        /// Retrieves the performance point from the first session in the test result.
        /// </summary>
        /// <param name="testResult"> The test result containing the dictionary of <see cref="G4AutomationResponseModel"/>.</param>
        /// <returns> A <see cref="G4PerformancePointModelBase"/> object representing the performance point.</returns>
        public static G4PerformancePointModelBase GetPerformancePoint(this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult)
        {
            // Retrieve the performance point from the first session in the first response model.
            return testResult.Response.First().Value.Sessions.First().Value.PerformancePoint;
        }

        /// <summary>
        /// Retrieves the performance point model for a specified plugin from the test result.
        /// </summary>
        /// <param name="testResult">The test result containing the response models.</param>
        /// <param name="pluginName">The name of the plugin for which the performance point is retrieved.</param>
        /// <returns>The performance point model for the specified plugin, or null if not found.</returns>
        public static G4PerformancePointModelBase GetPerformancePoint(
            this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult,
            string pluginName)
        {
            // Retrieve the performance point from the first session in the first response model.
            return testResult
                .Response
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints
                .OfType<G4PluginPerformancePointModel>()
                .FirstOrDefault(i => i.Reference.Name.Equals(pluginName));
        }

        /// <summary>
        /// Retrieves the performance points from the first session in the test result.
        /// </summary>
        /// <param name="testResult">The test result containing the dictionary of <see cref="G4AutomationResponseModel"/>.</param>
        /// <returns>An enumerable collection of <see cref="G4PerformancePointModelBase"/> containing the performance points.</returns>
        public static IEnumerable<G4PerformancePointModelBase> GetPerformancePoints(this TestResultModel<IDictionary<string, G4AutomationResponseModel>> testResult)
        {
            // Retrieve the performance points from the first session in the first response model.
            return testResult
                .Response
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints;
        }

        /// <summary>
        /// Retrieves the performance points from the given response model.
        /// </summary>
        /// <param name="responseModel">The response model containing the performance points.</param>
        /// <returns>An enumerable collection of <see cref="G4PerformancePointModelBase"/> containing the performance points.</returns>
        public static IEnumerable<G4PerformancePointModelBase> GetPerformancePoints(this G4ResponseModel responseModel)
        {
            // Retrieve the performance points from the response model.
            return responseModel.ResponseData.PerformancePoints;
        }

        /// <summary>
        /// Sets external capabilities for automation.
        /// </summary>
        /// <param name="automation">The automation model to set external capabilities for.</param>
        /// <param name="capabilities">The external capabilities to set.</param>
        public static void SetExternal(this G4AutomationModel automation, IDictionary<string, object> capabilities)
        {
            // Create an external repository model with default values
            var externalRepository = new G4ExternalRepositoryModel()
            {
                Url = "http://localhost:9002", // Default URL
                Version = 1,                   // Default version
                Capabilities = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };

            // If capabilities are provided, add them to the external repository
            capabilities ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Add each capability from the provided dictionary to the external repository
            foreach (var item in capabilities)
            {
                externalRepository.Capabilities[item.Key] = item.Value;
            }

            // Set the external repository as the sole item in the array of external repositories for automation
            automation.Settings.PluginsSettings.ExternalRepositories = [externalRepository];
        }
    }
}
