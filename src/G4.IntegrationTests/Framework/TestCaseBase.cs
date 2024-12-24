using G4.Api;
using G4.Converters;
using G4.Extensions;
using G4.IntegrationTests.Extensions;
using G4.Models;
using G4.WebDriver.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace G4.IntegrationTests.Framework
{
    /// <summary>
    /// An abstract class representing a test case.
    /// </summary>
    public abstract class TestCaseBase
    {
        // A collection of automation environments used in the test case.
        private readonly ConcurrentBag<AutomationEnvironment> _environments;

        // The time interval between test case attempts.
        private readonly int _attemptsDelay;

        // The number of test case attempts.
        private int _attempts;

        /// <summary>
        /// Initializes a new instance of the TestCase class with the provided test context.
        /// </summary>
        /// <param name="context">The test context for the test case.</param>
        protected TestCaseBase(TestContext context)
        {
            // Assign the provided test context to the _context field
            TestContext = context;

            // Initialize the collection of automation environments or use an existing one
            _environments ??= [];

            // Parse and set the number of test case attempts
            _attempts = context.Properties.Contains(key: "Integration.NumberOfAttempts")
                ? int.Parse($"{context.Properties["Integration.NumberOfAttempts"]}")
                : int.Parse("1");

            // Parse and set the time interval between test case attempts
            _attemptsDelay= context.Properties.Contains(key: "Integration.AttemptsDelay")
                ? int.Parse($"{context.Properties["Integration.AttemptsDelay"]}")
                : int.Parse("15000");

            // Set the application under test from the test context properties
            ApplicationUnderTest = $"{context.Properties["Integration.ApplicationUnderTest"]}";
        }

        #region *** Test: Properties    ***
        /// <summary>
        /// Gets or sets the name of the application under test.
        /// </summary>
        public virtual string ApplicationUnderTest { get; set; }

        /// <summary>
        /// Gets the JSON serializer options for serializing objects to JSON.
        /// </summary>
        public static JsonSerializerOptions SerializerOptions
            => G4Utilities.NewJsonSettings(new ExceptionConverter());

        /// <summary>
        /// Gets the context information for the current test.
        /// </summary>
        public TestContext TestContext { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the test is a negative test.
        /// </summary>
        public virtual bool IsNegativeTest { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the test is a web test.
        /// </summary>
        public virtual bool IsWebTest { get; set; } = true;
        #endregion

        #region *** Test: Environments  ***
        /// <summary>
        /// Adds an AutomationEnvironment to the collection of environments associated with this test case.
        /// </summary>
        /// <param name="environment">The AutomationEnvironment to add.</param>
        /// <returns>The current instance of the TestCase for method chaining.</returns>
        public TestCaseBase AddEnvironment(AutomationEnvironment environment)
        {
            // Ensure that TestData and TestParameters dictionaries are initialized if not already
            environment.TestData ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            environment.TestParameters ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Add the provided environment to the collection
            _environments.Add(environment);

            // Return the current instance of the TestCase for method chaining
            return this;
        }
        #endregion

        #region *** Test: User Plugins  ***
        /// <summary>
        /// Sets up the preconditions for the test case. This method can be overridden
        /// by derived classes to specify their own preconditions.
        /// </summary>
        /// <param name="environment">The automation environment to use for precondition setup.</param>
        public void Preconditions(AutomationEnvironment environment)
        {
            // Call the method for setting up preconditions.
            OnPreconditions(environment);
        }

        /// <summary>
        /// Allows derived classes to specify custom preconditions for the test case.
        /// This method is called by the <see cref="Preconditions"/> method.
        /// </summary>
        /// <param name="environment">The automation environment to use for precondition setup.</param>
        protected virtual void OnPreconditions(AutomationEnvironment environment)
        {
            // This is a placeholder method. Derived classes can override this to implement
            // custom precondition setup operations if needed.
        }

        /// <summary>
        /// Cleans up the resources and performs necessary operations at the end of the test case execution.
        /// This method can be overridden by derived classes to specify custom cleanup operations.
        /// </summary>
        /// <param name="environment">The automation environment to use for cleanup.</param>
        public void Cleanup(AutomationEnvironment environment)
        {
            try
            {
                // Log the automation environment for debugging and analysis using JSON serialization.
                TestContext.WriteLine(JsonSerializer.Serialize(environment, SerializerOptions));

                // Call the method for performing cleanup operations.
                OnCleanup(environment);
            }
            catch (Exception e)
            {
                // Log any exceptions that occur during cleanup.
                TestContext.WriteLine($"Cleanup for '{GetType().Name}' completed. " +
                    $"Automation environment details:\n{JsonSerializer.Serialize(environment, SerializerOptions)}");

                // Log any exceptions that occurred during cleanup, if applicable.
                // GetBaseException() is used to retrieve the most specific exception and provide better error details.
                TestContext.WriteLine($"Exception during cleanup: {e.GetBaseException()}");
            }
        }

        /// <summary>
        /// Allows derived classes to specify custom cleanup operations for the test case.
        /// This method is called by the <see cref="Cleanup"/> method.
        /// </summary>
        /// <param name="environment">The automation environment to use for cleanup.</param>
        protected virtual void OnCleanup(AutomationEnvironment environment)
        {
            // This is a placeholder method. Derived classes can override this to implement
            // custom cleanup operations if needed.
        }

        /// <summary>
        /// Hook for processing automation results after execution.
        /// </summary>
        /// <param name="environment">The automation environment used for processing.</param>
        /// <param name="responses">A collection of response models from the automation.</param>
        /// <returns>True if the automation is considered successful, false otherwise.</returns>
        protected virtual bool OnTeardown(
            AutomationEnvironment environment, IEnumerable<G4ResponseModel> responses)
        {
            // By default, the automation is considered successful if responses pass the Assert method.
            return responses.Assert();
        }
        #endregion

        #region *** Test: Invocation    ***
        /// <summary>
        /// Invokes the test case with multiple environments and returns true if any environment passes the test.
        /// </summary>
        /// <returns>True if the test passes in at least one environment, otherwise false.</returns>
        public bool Invoke()
        {
            // If there are no environments, invoke the test case with the default environment
            if (_environments?.IsEmpty == true)
            {
                return Invoke(testCase: this, environment: default);
            }

            // Iterate through the collection of environments
            foreach (var environment in _environments)
            {
                // Invoke the test case with the current environment
                var actual = Invoke(testCase: this, environment);

                // If the test passes in the current environment, return true
                if (actual)
                {
                    return actual;
                }
            }

            // Return false if the test does not pass in any environment
            return false;
        }

        // Invokes a test case for a specific environment, allowing for multiple attempts.
        private static bool Invoke(TestCaseBase testCase, AutomationEnvironment environment)
        {
            // Retrieve the number of attempts from the automation environment or use the default value from the test case
            testCase._attempts = environment.ContextProperties.TryGetValue("attempts", out object value)
                ? int.Parse($"{value}")
                : testCase._attempts;

            // Iterate through the test case attempts
            for (int i = 1; i <= testCase._attempts; i++)
            {
                // Invoke the test case for the current iteration
                var actual = InvokeTestIteration(testCase, environment);

                // Invert the actual result if the test case is a negative test
                actual = testCase.IsNegativeTest ? !actual : actual;

                // If the test case passes, return true
                if (actual)
                {
                    return actual;
                }
            }

            // Determine the overall result based on the 'actual' test parameter from the environment
            var result = environment.TestParameters.Get(key: "actual", defaultValue: false) ? "Pass" : "Fail";

            // Write the completion message to the test context
            testCase.TestContext.WriteLine($"Test case '{testCase.GetType().Name}' has completed with a result of '{result}'.");

            // Return false if the test case does not pass after all attempts
            return false;
        }

        // Invokes a single iteration of a test case within a specific environment.
        private static bool InvokeTestIteration(TestCaseBase testCase, AutomationEnvironment environment)
        {
            try
            {
                // Execute the preconditions for the test case
                testCase.Preconditions(environment);

                // Execute the automation test for the test case and capture actual results and responses
                var (actual, responses) = InvokeAutomation(testCase, environment);
                environment.TestParameters["actual"] = actual;
                environment.TestParameters["responses"] = responses;
            }
            catch (Exception e) when (e is NotImplementedException || e is AssertInconclusiveException)
            {
                // Handle exceptions that indicate inconclusive results
                var testParameters = environment.TestParameters;
                var message = $"Unable to determine test results for the '{testParameters["driver"]}' driver.";

                // Mark the test as inconclusive with the provided message
                Assert.Inconclusive(message);
            }
            catch (Exception e)
            {
                // Handle other exceptions that may occur during the test case iteration
                testCase.TestContext.WriteLine($"Failed to invoke '{testCase.GetType().Name}' iteration. Exception: {e.GetBaseException()}");

                // Sleep for a specified time before retrying the iteration
                Thread.Sleep(testCase._attemptsDelay);
            }
            finally
            {
                // Execute cleanup operations for the test case
                testCase.Cleanup(environment);
            }

            // Check if the "actual" key exists in the environment's test parameters and return its boolean value
            var isKey = environment.TestParameters.ContainsKey("actual");
            return isKey && (bool)environment.TestParameters["actual"];
        }
        #endregion

        #region *** Test: Automation    ***
        // Invokes the test case, executes automation, and processes the results.
        private static (bool Actual, IEnumerable<G4ResponseModel> Responses) InvokeAutomation(
            TestCaseBase testCase, AutomationEnvironment environment)
        {
            // Get the automation instance for the test case.
            var automation = NewAutomation(testCase, environment);

            // Create a client instance.
            var client = new G4Client().Automation;

            // Invoke the automation and get the response models.
            var results = client.Invoke(automation);

            // Extract response models from the results.
            var responses = results.SelectMany(i => i.Value.Sessions).Select(i => i.Value);

            // Store session keys in the environment's test parameters.
            environment.TestParameters["sessions"] = results.SelectMany(i => i.Value.Sessions).Select(i => i.Key);

            // Log the response models using JSON serialization.
            testCase.TestContext.WriteLine(JsonSerializer.Serialize(responses, SerializerOptions));

            // Process the responses and determine the actual result.
            bool actual = testCase.OnTeardown(environment, responses);

            // If the result is false, mark the test as inconclusive.
            if (!actual)
            {
                responses.AssertInconclusive();
            }

            // Return a tuple with the actual result and the response models.
            return (actual, responses);
        }

        // Gets the automation model for a given test case, which defines the automation steps and parameters.
        private static G4AutomationModel NewAutomation(TestCaseBase testCase, AutomationEnvironment environment)
        {
            // Retrieve authentication and configuration settings for the test case.
            var authentication = testCase.OnAuthentication(environment);
            var configuration = testCase.OnConfiguration(environment);

            // Get the application under test and page to navigate to.
            var application = testCase.ApplicationUnderTest;
            environment.TestParameters.TryGetValue("page", out object pageOut);
            var page = $"{pageOut}";

            // Combine the application URL with the specified page.
            application = application.EndsWith('/')
                ? $"{application[..^1]}/{page}"
                : $"{application}/{page}";

            // Retrieve a list of actions specified by the test case.
            var actions = testCase.OnActions(environment).ToArray();
            var actionsList = new List<ActionRuleModel>();

            // For web tests, add a "GoToUrl" action to navigate to the application URL.
            if (testCase.IsWebTest)
            {
                actionsList.Add(new ActionRuleModel { PluginName = "GoToUrl", Argument = application });
            }

            // Add other specified actions and a "CloseBrowser" action.
            actionsList.AddRange(actions);
            if (!environment.ContextProperties.ContainsKey("SkipCloseBrowser"))
            {
                actionsList.Add(new ActionRuleModel { PluginName = "CloseBrowser" });
            }

            // Retrieve and configure driver parameters.
            var driverParams = NewDriverParameters(environment);
            driverParams = testCase.OnDriver(environment, driverParams).ToDictionary();

            // Create an AutomationModel instance with the collected data.
            var automation = new G4AutomationModel
            {
                Stages =
                [
                    new G4StageModel
                    {
                        Description = "Main stage for invoking integration tests.",
                        Jobs =
                        [
                            new G4JobModel
                            {
                                Reference = new()
                                {
                                    Description = "Job responsible for invoking integration a single integration test.",
                                    Name = $"Invoking Test {testCase.GetType().FullName}",
                                },
                                Rules = actionsList
                            }
                        ],
                        Name = "Integration Tests",
                    }
                ],
                Authentication = authentication,
                Settings = configuration,
                DriverParameters = driverParams
            };

            // Allow the test case to further customize the automation model.
            testCase.OnAutomation(automation, environment);

            // Return the constructed AutomationModel instance.
            return automation;
        }

        // Gets the driver parameters required for configuring the automation driver.
        private static Dictionary<string, object> NewDriverParameters(AutomationEnvironment environment)
        {
            // Retrieve context properties and test parameters from the environment.
            var properties = environment.ContextProperties;
            var testParameters = environment.TestParameters;

            // Determine the path to the driver binaries based on the execution context.
            var binariesPath = $"{properties["Integration.Local"]}".Equals("true", StringComparison.OrdinalIgnoreCase)
                ? $"{properties["Grid.Endpoint"]}"
                : $"{properties["Grid.Endpoint"]}";

            // Get the desired driver capabilities for the automation.
            var capabilities = NewCapabilities(environment);

            // Create a CapabilitiesModel to encapsulate the driver capabilities.
            var capabilitiesModel = new CapabilitiesModel
            {
                AlwaysMatch = capabilities,
                FirstMatch = [new Dictionary<string, object>()]
            };

            // Build and return a dictionary of driver parameters.
            testParameters.TryGetValue("driver", out object driver);
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["driver"] = $"{driver}",
                ["driverBinaries"] = binariesPath,
                ["capabilities"] = capabilitiesModel
            };
        }

        // Gets the driver capabilities required for configuring the automation driver.
        private static Dictionary<string, object> NewCapabilities(AutomationEnvironment environment)
        {
            // Retrieve context properties and test parameters from the environment.
            var properties = environment.ContextProperties;
            var testParameters = environment.TestParameters;

            // Determine the build name based on the "Build.Name" property.
            var buildName = properties.Get(key: "Build.Name", defaultValue: string.Empty);
            buildName = string.IsNullOrEmpty(buildName)
                ? $"Development.{DateTime.UtcNow:yyyy.MM.dd}"
                : buildName;

            // Configure browserStackOptions with specific capabilities.
            testParameters.TryGetValue("resolution", out object resolution);
            testParameters.TryGetValue("browserVersion", out object browserVersion);
            testParameters.TryGetValue("osVersion", out object osVersion);
            testParameters.TryGetValue("os", out object os);
            var browserStackOptions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["resolution"] = $"{resolution}",
                ["browserVersion"] = $"{browserVersion}",
                ["osVersion"] = $"{osVersion}",
                ["os"] = $"{os}",
                ["local"] = "true",
                ["sessionName"] = GetTestName(),
                ["buildName"] = buildName,
                ["projectName"] = $"{properties["Project.Name"]}",
                ["seleniumVersion"] = $"{properties["Integration.Selenium.Version"]}"
            };

            // Build and return a dictionary of driver capabilities.
            _ = bool.TryParse($"{properties["Integration.Local"]}", out bool isLocal);
            testParameters.TryGetValue("browserName", out object browserName);
            return isLocal
                ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["browserName"] = browserName
                }
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["buildName"] = buildName,                         // For Mobile
                    ["projectName"] = $"{properties["Project.Name"]}", // For Mobile
                    ["browserName"] = browserName,
                    ["bstack:options"] = browserStackOptions
                };
        }

        // Gets the display name of the currently executing test method if available
        // otherwise, "Test method name not found".
        private static string GetTestName()
        {
            // Define a constant string to hold the message indicating that the test method name was not found
            const string MethodNameNotFound = "Test method name not found";

            // Initialize variables
            var counter = 0;
            var method = new StackFrame(counter++).GetMethod();
            var isTestMethod = method.GetCustomAttribute<TestMethodAttribute>() != null;

            // Iterate through the call stack to find the currently executing test method
            while (!isTestMethod)
            {
                method = new StackFrame(counter++).GetMethod();
                if (method == null)
                {
                    // If no test method is found, return default value
                    return MethodNameNotFound;
                }

                var attribute = method.GetCustomAttribute<TestMethodAttribute>();

                // Check if the current method is a test method
                isTestMethod = attribute != default;
                if (isTestMethod)
                {
                    // Return the display name of the test method if available
                    return string.IsNullOrEmpty(attribute.DisplayName)
                        ? MethodNameNotFound
                        : attribute.DisplayName;
                }
            }

            // Return the default message indicating that the test method name was not found
            return MethodNameNotFound;
        }
        #endregion

        #region *** Automation Plugins  ***
        /// <summary>
        /// Retrieves the automation actions to be executed.
        /// </summary>
        /// <param name="environment">The automation environment used for action retrieval.</param>
        /// <returns>An enumerable of action rules to be executed during automation.</returns>
        protected abstract IEnumerable<ActionRuleModel> OnActions(AutomationEnvironment environment);

        /// <summary>
        /// Retrieves the authentication model for the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment used for authentication.</param>
        /// <returns>An AuthenticationModel containing authentication details.</returns>
        protected virtual AuthenticationModel OnAuthentication(AutomationEnvironment environment)
        {
            // Retrieve the username from context properties.
            var username = $"{environment.ContextProperties["G4.Username"]}";

            // Create and return an AuthenticationModel with the username.
            return new AuthenticationModel
            {
                Username = username
            };
        }

        /// <summary>
        /// Retrieves the engine configuration for the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment used for configuration.</param>
        /// <returns>An EngineConfigurationModel containing configuration details.</returns>
        protected virtual G4SettingsModel OnConfiguration(AutomationEnvironment environment)
        {
            // Attempt to parse the load and search timeout values from context properties.
            _ = int.TryParse($"{environment.ContextProperties["Integration.LoadTimeout"]}", out int loadOut);
            _ = int.TryParse($"{environment.ContextProperties["Integration.SearchTimeout"]}", out int searchOut);

            // Create and return an EngineConfigurationModel with the timeout values.
            return new G4SettingsModel
            {
                AutomationSettings = new()
                {
                    LoadTimeout = loadOut,
                    SearchTimeout = searchOut
                },
                EnvironmentsSettings = new()
                {
                    ReturnEnvironment = true
                }
            };
        }

        /// <summary>
        /// Allows customization of driver parameters for the automation environment.
        /// </summary>
        /// <param name="environment">The automation environment used for driver parameter customization.</param>
        /// <param name="driverParams">The existing driver parameters to be customized.</param>
        /// <returns>A dictionary of customized driver parameters.</returns>
        protected virtual IDictionary<string, object> OnDriver(
            AutomationEnvironment environment, IDictionary<string, object> driverParams)
        {
            // By default, no customization is applied, so the existing driver parameters are returned.
            return driverParams;
        }

        /// <summary>
        /// Retrieves extraction rules to be applied during automation execution.
        /// </summary>
        /// <param name="environment">The automation environment used for extraction rules.</param>
        /// <returns>An enumerable of ExtractionRuleModel defining extraction rules.</returns>
        protected virtual IEnumerable<ExtractionRuleModel> OnExtractions(AutomationEnvironment environment)
        {
            // By default, no extraction rules are defined, so an empty enumerable is returned.
            return [];
        }

        /// <summary>
        /// Allows customization of the automation model before execution.
        /// </summary>
        /// <param name="automation">The AutomationModel representing automation steps and parameters.</param>
        /// <param name="environment">The automation environment used for extraction rules.</param>
        protected virtual void OnAutomation(G4AutomationModel automation, AutomationEnvironment environment)
        {
            // This is a placeholder method. Derived classes can override it to customize the automation model.
        }
        #endregion
    }
}
