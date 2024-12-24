using G4.Api;
using G4.Attributes;
using G4.Cache;
using G4.Extensions;
using G4.Models;
using G4.Plugins;
using G4.Plugins.Engine;
using G4.Plugins.Framework;
using G4.WebDriver.Extensions;
using G4.WebDriver.Models;
using G4.WebDriver.Remote;
using G4.WebDriver.Simulator;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace G4.UnitTests.Framework
{
    /// <summary>
    /// An abstract base class for action-related unit tests.
    /// </summary>
    [TestClass]
    [DeploymentItem("Binaries", "Binaries")]
    [DeploymentItem("Pages", "Pages")]
    [DeploymentItem("Resources", "Resources")]
    [DeploymentItem("appsettings.json")]
    public abstract class TestBase
    {
        /// <summary>
        /// A constant for string comparison with a specified comparison type.
        /// </summary>
        public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// A constant representing the name of the action method ("Send").
        /// </summary>
        public const string ActionMethodName = "Send";

        /// <summary>
        /// A constant representing the MacroResult key.
        /// </summary>
        public const string MacroResultKey = "MacroResult";

        // Initialize a random number generator for generating random strings
        private static readonly Random s_random = new();

        /// <summary>
        /// JSON serialization options for unit tests.
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Performs one-time setup for the test assembly.
        /// </summary>
        /// <param name="context">The test context.</param>
        [AssemblyInitialize]
        public static void OneTimeSetup(TestContext context)
        {
            // Remove any existing web host.
            WebServer.RemoveWebHost();

            // Create a new web host for testing.
            WebServer.NewWebHost();

            // Start the web host.
            WebServer.StartWebHost();

            // Pause the test execution for 5 seconds (to allow the web server to start).
            Thread.Sleep(TimeSpan.FromSeconds(5));

            // Write a message to the test context indicating that a new test server was created.
            context.WriteLine("New-TestServer = Created");
        }

        /// <summary>
        /// Initializes the test by setting up the web automation environment.
        /// </summary>
        [TestInitialize]
        public virtual void TestSetup()
        {
            // Set up web automation (details not shown in this code snippet)
            Automation = SetWebAutomation(TestContext);
        }

        /// <summary>
        /// Initializes a new instance of the ActionTests class and performs class setup tasks.
        /// </summary>
        protected TestBase()
        {
            // Call the ClassSetup method to perform class setup tasks
            ClassSetup();
        }

        #region *** Properties     ***
        /// <summary>
        /// Gets or sets the AutomationModel associated with the test.
        /// </summary>
        public G4AutomationModel Automation { get; private set; }

        /// <summary>
        /// Gets or sets the Stopwatch used to measure elapsed time in the test.
        /// </summary>
        public Stopwatch Stopwatch { get; private set; }

        /// <summary>
        /// Gets or sets the TestContext for the test.
        /// </summary>
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets or sets the SimulatorDriver used in the test.
        /// </summary>
        public SimulatorDriver WebDriver { get; set; }
        #endregion

        #region *** Invoke Plugin  ***
        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>()
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel: default, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel: default, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>([StringSyntax(StringSyntaxAttribute.Json)] string ruleJson)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(ruleJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>([StringSyntax(StringSyntaxAttribute.Json)] string ruleJson, By by)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(ruleJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, [StringSyntax(StringSyntaxAttribute.Json)] string ruleJson)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(ruleJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(By by)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel: default, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, By by)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel: default, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel: default, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel: default, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="rulesJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, [StringSyntax(StringSyntaxAttribute.Json)] string rulesJson, By by)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(rulesJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="rulesJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(
            [StringSyntax(StringSyntaxAttribute.Json)] string rulesJson,
            IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(rulesJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="rulesJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(
            SimulatorDriver driver,
            [StringSyntax(StringSyntaxAttribute.Json)] string rulesJson,
            IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(rulesJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(
            [StringSyntax(StringSyntaxAttribute.Json)] string ruleJson,
            By by,
            IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(ruleJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="rulesJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(
            SimulatorDriver driver,
            [StringSyntax(StringSyntaxAttribute.Json)] string rulesJson,
            By by,
            IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(rulesJson, JsonOptions);

            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(G4RuleModelBase ruleModel)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, G4RuleModelBase ruleModel)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by: default, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(G4RuleModelBase ruleModel, By by)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, G4RuleModelBase ruleModel, By by)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by, capabilities: default);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(G4RuleModelBase ruleModel, IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver: default, ruleModel, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="driver">The simulator driver to be used by the plugin.</param>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, G4RuleModelBase ruleModel, IDictionary<string, object> capabilities)
            where T : PluginBase
        {
            // Invoke the plugin with the specified parameters and return the test result.
            return Invoke<T>(driver, ruleModel, by: default, capabilities);
        }

        /// <summary>
        /// Invokes a plugin with the specified parameters and returns the test result.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to be invoked, which must inherit from <see cref="PluginBase"/>.</typeparam>
        /// <param name="ruleModel">The rule model base to be passed to the plugin.</param>
        /// <param name="by">The criteria to find the element.</param>
        /// <param name="capabilities">A dictionary of capabilities to be used by the simulator driver.</param>
        /// <returns>A <see cref="TestResultModel"/> containing the plugin, response, and execution time.</returns>
        public TestResultModel<PluginResponseModel> Invoke<T>(SimulatorDriver driver, G4RuleModelBase ruleModel, By by, IDictionary<string, object> capabilities)
            where T: PluginBase
        {
            // Invokes a plugin with the specified parameters.
            static PluginResponseModel Invoke(SimulatorDriver driver, PluginBase plugin, G4RuleModelBase ruleModel, By by)
            {
                // Find the element based on the criteria specified by 'by'. If 'by' is not provided, set element to default.
                var element = by == default ? default : driver.FindElement(by);

                // Create a PluginDataModel with the rule and the found element.
                var pluginData = new PluginDataModel
                {
                    Rule = ruleModel,
                    Element = element
                };

                try
                {
                    // Send the plugin data to the plugin and return the response.
                    return plugin.Send(pluginData);
                }
                catch (Exception e)
                {
                    // Rethrow the base exception in case of an error.
                    throw e.GetBaseException();
                }
            }

            // Perform class setup.
            ClassSetup();

            // Instantiate a new PluginResponseModel.
            // This will be returned after invoking the plugin.
            var responseModel = new PluginResponseModel();

            // Instantiate a new Stopwatch to measure execution time.
            var stopwatch = new Stopwatch();

            // Instantiate a new SessionModel with the provided capabilities.
            var sessionModel = new SessionModel
            {
                Capabilities = new CapabilitiesModel
                {
                    AlwaysMatch = capabilities ?? new Dictionary<string, object>()
                }
            };

            // Instantiate a new SimulatorDriver.
            // This is a mock WebDriver for testing.
            driver ??= new SimulatorDriver(sessionModel);

            // Check if the NullDriver capability is set to true.
            driver = capabilities.Get(key: "NullDriver", defaultValue: false) ? null : driver;

            // Instantiate a new WebAutomation instance.
            // This is a mock automation model for testing.
            var automation = NewAutomation(TestContext, ruleModels: [], capabilities);

            // Instantiate a new plugin of type T with the provided driver, automation, and capabilities.
            var plugin = NewPlugin<T>(driver, automation, capabilities);

            // Initialize the rule model if it is not already set.
            ruleModel ??= new ActionRuleModel();

            // Set the plugin name in the rule if it is not already set.
            ruleModel.PluginName = string.IsNullOrEmpty(ruleModel.PluginName)
                ? plugin.GetManifest().Key
                : ruleModel.PluginName;

            // Initialize the rule with automation.
            InitializeRule(automation, ruleModel);

            // Start the stopwatch to measure execution time.
            stopwatch.Start();

            try
            {
                // Invoke the plugin and get the response model.
                responseModel = Invoke(driver, plugin, ruleModel, by);
            }
            finally
            {
                // Stop the stopwatch after invocation.
                stopwatch.Stop();
            }

            // Return a new TestResultModel containing the plugin, response, and execution time.
            return new TestResultModel<PluginResponseModel>
            {
                Automation = automation,
                Plugin = plugin,
                Response = responseModel,
                Stopwatch = stopwatch
            };
        }

        /// <summary>
        /// Invokes the automation process with the specified rule models and capabilities.
        /// </summary>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <returns>A Dictionary containing the automation model, response, and stopwatch.</returns>
        public TestResultModel<IDictionary<string, G4AutomationResponseModel>> Invoke([StringSyntax(StringSyntaxAttribute.Json)] string ruleJson)
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = (G4RuleModelBase)JsonSerializer.Deserialize<ActionRuleModel>(ruleJson, JsonOptions);

            // Invoke the automation process with the specified rule models and capabilities.
            return Invoke([ruleModel], capabilities: new Dictionary<string, object>());
        }

        /// <summary>
        /// Invokes the automation process with the specified rule models and capabilities.
        /// </summary>
        /// <param name="ruleJson">The JSON string representing the rules to be applied by the plugin.</param>
        /// <param name="capabilities">The dictionary of capabilities to be used in the automation.</param>
        /// <returns>A Dictionary containing the automation model, response, and stopwatch.</returns>
        public TestResultModel<IDictionary<string, G4AutomationResponseModel>> Invoke(
            [StringSyntax(StringSyntaxAttribute.Json)] string ruleJson,
            IDictionary<string, object> capabilities)
        {
            // Deserialize the rules JSON string into an ActionRuleModel object.
            var ruleModel = JsonSerializer.Deserialize<G4RuleModelBase>(ruleJson, JsonOptions);

            // Invoke the automation process with the specified rule models and capabilities.
            return Invoke([ruleModel], capabilities);
        }

        /// <summary>
        /// Invokes the automation process with the specified rule models and capabilities.
        /// </summary>
        /// <param name="ruleModels">The collection of rule models to be applied in the automation.</param>
        /// <returns>A Dictionary containing the automation model, response, and stopwatch.</returns>
        public TestResultModel<IDictionary<string, G4AutomationResponseModel>> Invoke(IEnumerable<G4RuleModelBase> ruleModels)
        {
            return Invoke(ruleModels, capabilities: new Dictionary<string, object>());
        }

        /// <summary>
        /// Invokes the automation process with the specified rule models and capabilities.
        /// </summary>
        /// <param name="ruleModels">The collection of rule models to be applied in the automation.</param>
        /// <param name="capabilities">The dictionary of capabilities to be used in the automation.</param>
        /// <returns>A Dictionary containing the automation model, response, and stopwatch.</returns>
        public TestResultModel<IDictionary<string, G4AutomationResponseModel>> Invoke(
            IEnumerable<G4RuleModelBase> ruleModels, IDictionary<string, object> capabilities)
        {
            // Create a new automation model using the provided rule models and capabilities.
            var automation = NewAutomation(TestContext, ruleModels, capabilities);

            // Initialize a stopwatch to measure the duration of the invocation.
            var stopwatch = new Stopwatch();

            // Initialize the client to invoke the automation.
            var client = new G4Client().Automation;

            // Initialize the response dictionary.
            IDictionary<string, G4AutomationResponseModel> response = default;

            // Start the stopwatch before invoking the automation.
            stopwatch.Start();

            try
            {
                // Invoke the automation and get the response.
                response = client.Invoke(automation);
            }
            finally
            {
                // Stop the stopwatch after the invocation.
                stopwatch.Stop();
            }

            // Return the test result model containing the automation, response, and stopwatch.
            return new TestResultModel<IDictionary<string, G4AutomationResponseModel>>
            {
                Automation = automation,
                Response = response,
                Stopwatch = stopwatch
            };
        }

        // Creates a new instance of <see cref="G4AutomationModel"/> based on the provided parameters.
        private static G4AutomationModel NewAutomation(
            TestContext testContext, IEnumerable<G4RuleModelBase> ruleModels, IDictionary<string, object> capabilities)
        {
            // Initialize external repository model with default values.
            var externalRepository = new G4ExternalRepositoryModel()
            {
                Url = "http://localhost:9002",
                Version = 1,
                Capabilities = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };

            // Ensure capabilities is not null, initializing it if necessary.
            capabilities ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Populate the external repository's capabilities with provided capabilities.
            foreach (var item in capabilities)
            {
                externalRepository.Capabilities[item.Key] = item.Value;
            }

            // Create authentication model using the username from the test context properties.
            var authentication = new AuthenticationModel
            {
                Username = $"{testContext.Properties["G4.Username"]}"
            };

            // Create driver parameters dictionary with capabilities and driver information.
            var driverParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Capabilities"] = new CapabilitiesModel
                {
                    AlwaysMatch = capabilities
                },
                ["Driver"] = "SimulatorDriver",
                ["DriverBinaries"] = "."
            };

            // Configure settings for the G4™ automation model.
            var settings = new G4SettingsModel
            {
                // Configure environment settings to return the environment.
                EnvironmentsSettings = new EnvironmentsSettingsModel
                {
                    ReturnEnvironment = true
                },

                // Configure performance points settings to return performance points.
                PerformancePointsSettings = new PerformancePointsSettingsModel
                {
                    ReturnPerformancePoints = true
                },

                // Configure plugins settings with the external repository.
                PluginsSettings = new PluginsSettingsModel
                {
                    ExternalRepositories = [externalRepository]
                }
            };

            // Define stages and jobs for the automation, associating rule models.
            var stages = new[]
            {
                new G4StageModel
                {
                    Jobs =
                    [
                        new G4JobModel
                        {
                            Rules = ruleModels
                        }
                    ]
                }
            };

            // Initialize the G4™ automation model with the provided parameters.
            var automation = new G4AutomationModel
            {
                Authentication = authentication,
                DriverParameters = driverParameters,
                Settings = settings,
                Stages = stages
            };

            // Initialize and return the automation model.
            return automation.Initialize();
        }
        #endregion

        #region *** Tests: Common  ***
        /// <summary>
        /// Test case to verify the compliance of the plugin with its manifest.
        /// </summary>
        public virtual void ManifestComplianceTest()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Test case to verify the creation of a new plugin instance.
        /// </summary>
        public virtual void NewPluginTest()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region *** Documentation  ***
        /// <summary>
        /// Asserts the manifest of a specified plugin type.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to assert the manifest for.</typeparam>
        public void AssertManifest<T>() where T : PluginBase
        {
            // Get the name of the plugin type
            var pluginName = typeof(T).Name;

            // Construct the resource name based on the plugin type
            var resource = $"{pluginName}.json";

            // Call the method to assert the manifest using the plugin name and resource name
            AssertManifest<T>(pluginName, resource);
        }

        /// <summary>
        /// Asserts the manifest of a specified plugin type using a custom plugin name.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to assert the manifest for.</typeparam>
        /// <param name="pluginName">A custom plugin name to use for assertion.</param>
        public void AssertManifest<T>(string pluginName) where T : PluginBase
        {
            // Call the method to assert the manifest using the custom plugin name and a default resource
            AssertManifest<T>(pluginName, resource: default);
        }

        /// <summary>
        /// Asserts the manifest attributes of a specified plugin type with optional resource and custom plugin name.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to assert the manifest for.</typeparam>
        /// <param name="pluginName">A custom plugin name to use for assertion (optional).</param>
        /// <param name="resource">The name of the resource file to read plugin attributes (optional).</param>
        public void AssertManifest<T>(string pluginName, string resource)
            where T : PluginBase
        {
            // Create an instance of the plugin action
            var action = NewPlugin<T>(automation: Automation, capabilities: default);

            // Get the PluginAttribute of the plugin action
            var attribute = action.GetType().GetCustomAttribute<G4PluginAttribute>();

            if (!string.IsNullOrEmpty(resource))
            {
                // If a resource name is provided, read the PluginAttribute from the resource
                var assembly = typeof(T).Assembly;
                var actionAttribute = ReadEmbeddedResource<G4PluginAttribute>(assembly, resource);

                // Check if the plugin name matches the action name in the resource file
                var isName = attribute.Key.Equals(actionAttribute.Key);
                Assert.IsTrue(isName, "Plugin name does not match the action name specified in the resource file.");
            }

            // Assert that the plugin has a description
            Assert.IsTrue(attribute.Description?.Any() == true, "Plugin must have a description.");

            // Assert that the plugin has a summary
            Assert.IsTrue(attribute.Summary?.Any() == true, "Plugin must have a summary.");

            // If a custom plugin name is provided, check if it matches the plugin's key
            if (pluginName != null)
            {
                Assert.IsTrue(attribute.Key.Equals(pluginName), "Plugin name does not match the specified plugin name.");
            }

            // Assert that the plugin has at least one example
            Assert.IsTrue(attribute.Examples.Any(), "Plugin must have at least one example.");

            // Assert that all examples have at least one action rule
            Assert.IsTrue(attribute.Examples.All(i => i.Rule != null), "Plugin example must have at least one action rule.");

            // Assert that all examples have a valid action name
            Assert.IsTrue(attribute.Examples.All(i => !string.IsNullOrEmpty(i.Rule.PluginName)), "Example action rule must have a valid action name.");
        }
        #endregion

        #region *** Creation       ***
        /// <summary>
        /// Asserts that a plugin of type T has been generated correctly.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to assert.</typeparam>
        public void AssertPlugin<T>() where T : PluginBase
        {
            // Generate an instance of the plugin of type T
            var action = NewPlugin<T>(automation: Automation, capabilities: default);

            // Get the WebDriver property of the generated action
            var webDriver = action.GetType().GetProperty("WebDriver");

            // Assert that the plugin object is not the default value (null)
            Assert.IsTrue(action != default, "Plugin was not generated correctly.");

            // Assert that plugin types have been loaded into the cache manager
            Assert.IsTrue(CacheManager.Types.Count != 0, "Plugin types were not loaded.");

            // If the action is derived from PluginBase, assert that its WebDriver property is not null
            if (action is PluginBase)
            {
                Assert.IsTrue(webDriver?.GetValue(action) != null, "Plugin WebDriver was not generated correctly.");
            }
        }
        #endregion

        #region *** Action Rule    ***
        public static G4RuleModelBase InitializeRule(G4AutomationModel automation, G4RuleModelBase ruleModel)
        {
            // Create a new job reference model with the stage reference and random values
            var jobReference = automation.Cache.Jobs.First().Value.Reference;

            // Generate a new reference for the action rule model using the job reference
            ruleModel.Reference = ruleModel.NewReference(jobReference);

            // Return the populated action rule model
            return ruleModel;
        }
        #endregion

        #region *** Action Factory ***
        /// <summary>
        /// Creates a new instance of a specified plugin type with default capabilities.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to create.</typeparam>
        /// <returns>A new instance of the specified plugin type.</returns>
        public T NewPlugin<T>() where T : PluginBase
        {
            // Call the method to create a new instance of the plugin with default WebDriver, Automation, and capabilities
            return NewPlugin<T>(WebDriver, Automation, capabilities: default);
        }

        /// <summary>
        /// Creates a new instance of a specified plugin type with default capabilities and a custom AutomationModel.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to create.</typeparam>
        /// <param name="automation">The custom AutomationModel to use.</param>
        /// <returns>A new instance of the specified plugin type.</returns>
        public T NewPlugin<T>(G4AutomationModel automation) where T : PluginBase
        {
            // Call the method to create a new instance of the plugin with default WebDriver, the provided automation model, and default capabilities
            return NewPlugin<T>(WebDriver, automation, capabilities: default);
        }

        /// <summary>
        /// Creates a new instance of a specified plugin type with custom capabilities and a custom AutomationModel.
        /// </summary>
        /// <typeparam name="T">The type of the plugin to create.</typeparam>
        /// <param name="automation">The custom AutomationModel to use.</param>
        /// <param name="capabilities">A dictionary of custom capabilities for the plugin.</param>
        /// <returns>A new instance of the specified plugin type.</returns>
        public T NewPlugin<T>(G4AutomationModel automation, IDictionary<string, object> capabilities) where T : PluginBase
        {
            // Call the method to create a new instance of the plugin with default WebDriver, the provided automation model, and custom capabilities
            return NewPlugin<T>(WebDriver, automation, capabilities);
        }

        // Creates a new instance of a specified plugin type with custom capabilities, an AutomationModel, and a SimulatorDriver.
        private static T NewPlugin<T>(
            SimulatorDriver driver, G4AutomationModel automation, IDictionary<string, object> capabilities)
        {
            // Apply custom capabilities to the WebDriver if provided
            if (capabilities != null)
            {
                driver = driver.AddCapabilities(capabilities);
            }

            // Call the method to create a new instance of the plugin with the provided AutomationModel and WebDriver
            return NewPlugin<T>(automation, driver, new());
        }

        // Creates a new instance of a specified plugin type with custom AutomationModel, WebDriver, and repository (if applicable).
        private static T NewPlugin<T>(G4AutomationModel automation, IWebDriver driver, G4ExternalRepositoryModel repository)
        {
            // Define a local function to assert constructor type compatibility
            static bool AssertConstructorType(Type source, Type target)
            {
                // Check if the source type is exactly the same as the target type
                var isType = source == target;

                // Check if the source type is an interface and is assignable to the target type
                var isAssignable = source.IsInterface && source.IsAssignableFrom(target);

                // Return true if either the types match or the source is assignable to the target
                return isType || isAssignable;
            }

            // Define a local function to retrieve parameters for the plugin constructor
            static object[] GetParameters(ConstructorInfo info, object[] parameters)
            {
                var parametersResult = new List<object>();

                // Get the parameter types expected by the constructor
                var types = info.GetParameters().Select(i => i.ParameterType);

                foreach (var parameterType in types)
                {
                    // Find a parameter from the available parameters that matches the expected type
                    var type = Array.Find(parameters, i => i == null || AssertConstructorType(parameterType, i.GetType()));
                    parametersResult.Add(type);
                }

                // Filter out null parameters and return the matched parameters as an array
                return parametersResult.Where(i => i != null).ToArray();
            }

            // Get the constructor with the most parameters for the specified plugin type
            var constructor = typeof(T)
                .GetConstructors()
                .MaxBy(i => i.GetParameters().Length);

            // Create an instance of the AutomationInvoker with the provided automation model
            var invoker = new AutomationInvoker(automation);

            // Set the WebDriver property of the plugin factory to the provided WebDriver
            // This is necessary for the plugin to interact with the WebDriver
            // Unit tests does not trigger the WebDriver initialization, so we need to set it manually
            foreach (var factory in invoker.PluginFactoryAdapter.Factories.Values)
            {
                // factory.WebDriver = driver;
            }

            // Define the target parameters for the constructor
            var targetParameters = new G4PluginSetupModel
            {
                Automation = automation,
                Context = new G4Environment(),
                Invoker = invoker,
                WebDriver = driver
            };

            // Get the parameters required for the constructor
            var parameters = GetParameters(constructor, [targetParameters]);

            // If the type is ExternalPlugin, include the repository as a parameter
            parameters = typeof(T) == typeof(ExternalPlugin)
                ? [.. parameters, .. new[] { repository }]
                : parameters;

            // Create and return a new instance of the specified plugin type
            return (T)Activator.CreateInstance(typeof(T), parameters);
        }
        #endregion

        #region *** Resources      ***
        /// <summary>
        /// Reads an embedded resource from the specified assembly and deserializes it into an object of type T if it's valid JSON.
        /// </summary>
        /// <typeparam name="T">The type of object to deserialize the resource into.</typeparam>
        /// <param name="assembly">The assembly containing the embedded resource.</param>
        /// <param name="name">The name of the embedded resource to read.</param>
        /// <returns>
        /// An object of type T if the embedded resource is valid JSON; otherwise, the default value for type T.
        /// </returns>
        public static T ReadEmbeddedResource<T>(Assembly assembly, string name)
        {
            // Read the embedded resource as a string
            var resource = ReadEmbeddedResource(assembly, name);

            // Check if the resource is valid JSON
            return resource.AssertJson()
                ? JsonSerializer.Deserialize<T>(resource, JsonOptions)
                : default;
        }

        /// <summary>
        /// Reads the content of an embedded resource as a string from the specified assembly.
        /// </summary>
        /// <param name="assembly">The assembly containing the embedded resource.</param>
        /// <param name="name">The name of the embedded resource to read.</param>
        /// <returns>The content of the embedded resource as a string.</returns>
        public static string ReadEmbeddedResource(Assembly assembly, string name)
        {
            // Delegate the resource reading to the other ReadEmbeddedResource method
            return ReadEmbeddedResource(name, assembly);
        }

        // Reads the content of an embedded resource as a string from the specified assembly.
        private static string ReadEmbeddedResource(string name, Assembly assembly)
        {
            // If the name is empty or null, return an empty string
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            // Local function to check if a resource name matches the provided name
            static bool AssertName(string name, string fileName)
            {
                return name.EndsWith($".{fileName}", StringComparison.OrdinalIgnoreCase);
            }

            // Find the resource name that matches the provided name
            var fileReference = Array.Find(assembly.GetManifestResourceNames(), i => AssertName(i, name));

            // If no matching resource is found, return an empty string
            if (string.IsNullOrEmpty(fileReference))
            {
                return string.Empty;
            }

            // Read the content of the resource and return it as a string
            var stream = assembly.GetManifestResourceStream(fileReference);
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
        #endregion

        #region *** Testing        ***
        /// <summary>
        /// Invokes an action a specified number of times, retrying in case of exceptions.
        /// </summary>
        /// <param name="attempts">The number of times to attempt invoking the action.</param>
        /// <param name="test">The action to be invoked.</param>
        /// <exception cref="Exception">Throws the exception if it occurs in all attempts.</exception>
        public static void Invoke(int attempts, Action test)
        {
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    test.Invoke();
                    return; // Exit the loop if the action is successful
                }
                catch (Exception e)
                {
                    // Print a message indicating that an exception occurred
                    Console.WriteLine($"Attempt {i + 1} failed: {e.Message}");

                    if (i == attempts - 1)
                    {
                        // If in the last attempt and still failing, throw the exception
                        throw;
                    }
                }
            }
        }
        #endregion

        #region *** Utilities      ***
        /// <summary>
        /// Constructs an <see cref="G4AutomationModel"/> with a single action using the provided macro.
        /// </summary>
        /// <param name="context">The <see cref="TestContext[RuleProperties.WebElement]/> containing test-specific information.</param>
        /// <param name="macro">The macro to be used as an argument for the action.</param>
        /// <returns>An <see cref="G4AutomationModel"/> configured with the provided macro action.</returns>
        public static G4AutomationModel GetAutomation(TestContext context, string macro)
        {
            // Create an array of actions with a single action using the provided macro
            var actions = new[]
            {
                new ActionRuleModel
                {
                    PluginName = "NoAction",
                    Argument = macro
                }
            };

            // Construct an AutomationModel with the configured actions, authentication, and engine configuration
            var automation = new G4AutomationModel
            {
                Authentication = new()
                {
                    Username = $"{context.Properties["G4.Username"]}"
                },
                Settings = new()
                {
                    EnvironmentsSettings = new()
                    {
                        ReturnEnvironment = true
                    }
                }
            };

            return automation.NewStage(actions);
        }

        /// <summary>
        /// Gets a dictionary of driver parameters based on provided capabilities.
        /// </summary>
        /// <param name="capabilities">A dictionary of driver capabilities.</param>
        /// <returns>A dictionary of driver parameters.</returns>
        /// <remarks>
        /// This method constructs a dictionary of driver parameters for a simulator driver based on the provided capabilities.
        /// It encapsulates the driver type, driver binaries path, and capabilities within the returned dictionary.
        /// </remarks>
        public static IDictionary<string, object> GetDriverParameters(IDictionary<string, object> capabilities)
        {
            // Initialize capabilities if not provided.
            capabilities ??= new Dictionary<string, object>();

            // Construct and return a dictionary of driver parameters.
            return new Dictionary<string, object>
            {
                ["driver"] = "SimulatorDriver",
                ["driverBinaries"] = ".",
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["alwaysMatch"] = capabilities
                }
            };
        }

        /// <summary>
        /// Creates a new random string of the specified length.
        /// </summary>
        /// <param name="length">The length of the random string to generate.</param>
        /// <returns>A randomly generated string of the specified length.</returns>
        public static string NewRandomString(int length)
        {
            // The character pool from which to pick random characters
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder stringBuilder = new(length);

            // Append random characters to the string
            for (int i = 0; i < length; i++)
            {
                // Select a random character from the character pool and append it
                stringBuilder.Append(chars[s_random.Next(chars.Length)]);
            }

            // Convert StringBuilder to string and return
            return stringBuilder.ToString();
        }
        #endregion

        #region *** Setup: Class   ***
        // Performs setup tasks for the class, initializing a SimulatorDriver, setting up web automation,
        // and starting a stopwatch.
        private void ClassSetup()
        {
            // Initialize a SimulatorDriver for web automation
            WebDriver ??= new SimulatorDriver();

            // Start a stopwatch for measuring time (if needed)
            Stopwatch = new Stopwatch();
        }

        // Configures and returns an AutomationModel for web automation based on the provided test context.
        private static G4AutomationModel SetWebAutomation(TestContext testContext)
        {
            // Define the engine configuration for web automation
            var settings = new G4SettingsModel
            {
                // Initialize the automation settings with custom values
                AutomationSettings = new()
                {
                    SearchTimeout = 15000, // Set the search timeout to 15 seconds
                    LoadTimeout = 15000,   // Set the load timeout to 15 seconds
                    MaxParallel = 1        // Set the max parallel to 1 thread
                },
                // Initialize the environment settings with custom values
                EnvironmentsSettings = new()
                {
                    ReturnEnvironment = true  // Set the return environment flag to true
                }
            };

            // Retrieve authentication details from the test context
            var authentication = new AuthenticationModel
            {
                Username = $"{testContext.Properties["G4.Username"]}",
                Password = ""
            };

            // Initialize the automation model with the authentication and engine configuration
            var automation = new G4AutomationModel
            {
                Authentication = authentication,
                Settings = settings
            };

            // Return the configured automation model
            return automation.Initialize();
        }
        #endregion

        /// <summary>
        /// Represents the result of a test, including automation details, plugin information, response data, and timing.
        /// </summary>
        /// <typeparam name="T">The type of the response data associated with the test result.</typeparam>
        public class TestResultModel<T>
        {
            /// <summary>
            /// Gets or sets the automation model associated with the test.
            /// </summary>
            /// <value>The <see cref="G4AutomationModel"/> instance representing the automation context.</value>
            public G4AutomationModel Automation { get; set; }

            /// <summary>
            /// Gets or sets the plugin instance used during the test.
            /// </summary>
            /// <value>The <see cref="PluginBase"/> instance representing the plugin used.</value>
            public PluginBase Plugin { get; set; }

            /// <summary>
            /// Gets or sets the response data returned by the test.
            /// </summary>
            /// <value>The response data of type <typeparamref name="T"/>.</value>
            public T Response { get; set; }

            /// <summary>
            /// Gets or sets the stopwatch used to measure the duration of the test.
            /// </summary>
            /// <value>The <see cref="Stopwatch"/> instance that tracks the time taken by the test.</value>
            public Stopwatch Stopwatch { get; set; }
        }
    }
}
