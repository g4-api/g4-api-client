using G4.Api;
using G4.Models;
using G4.UnitTests.Framework;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Engine")]
    [TestCategory("G4Client")]
    [TestCategory("UnitTest")]
    public class G4ClientEventsTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod(displayName: "Verify PluginCreated event fired for all plugins when invoking automation model")]
        public void G4ClientPluginCreatedEventNoDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Create a list to store the results of the PluginCreated event
            var results = new List<string>();

            // Subscribe to the PluginCreated event and add the plugin type to the results list
            client.Automation.PluginCreated += (_, e) => results.Add(e.Plugin.GetType().FullName);

            // Invoke the automation model
            client.Automation.Invoke(automation);

            // Assert that the PluginCreated event fired for all plugins
            Assert.AreEqual(48, results.Count, "PluginCreated event did not fire for all plugins.");
        }

        [TestMethod(displayName: "Verify the G4Client PluginCreated event is triggered correctly with the expected data.")]
        public void G4ClientPluginCreatedEventWithDataTest()
        {
            // Create automation model using test context, without data
            var automation = NewAutomation(TestContext, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Create a dictionary to store the results of the PluginCreated event
            var results = new Dictionary<string, int>();

            // Subscribe to the PluginCreated event and update the results dictionary with the plugin type
            client.Automation.PluginCreated += (_, e) =>
            {
                var key = e.Plugin.GetType().Name;
                var isKey = results.ContainsKey(key);
                results[key] = isKey ? results[key] + 1 : 1;
            };

            // Invoke the automation model using the client
            client.Automation.Invoke(automation);

            // Assert that the results match the expected values for each plugin type
            Assert.AreEqual(expected: 8, actual: results["SendKeys"], message: "The number of SendKeys plugins created is incorrect.");
            Assert.AreEqual(expected: 4, actual: results["InvokeClick"], message: "The number of InvokeClick plugins created is incorrect.");
            Assert.AreEqual(expected: 12, actual: results["Assert"], message: "The number of Assert plugins created is incorrect.");
            Assert.AreEqual(expected: 12, actual: results["ElementAttribute"], message: "The number of ElementAttribute plugins created is incorrect.");
            Assert.AreEqual(expected: 12, actual: results["EqualOperator"], message: "The number of EqualOperator plugins created is incorrect.");
        }

        // Creates a new automation model with the provided testContext.
        private static G4AutomationModel NewAutomation(TestContext testContext, bool useData)
        {
            // Create authentication model with username from test context
            var authentication = new AuthenticationModel
            {
                Username = $"{testContext.Properties["G4.Username"]}"
            };

            // Create data source using JSON data
            var dataSource = useData ? NewJsonDataSource() : default;

            // Create automation model with authentication, data source, and driver parameters
            var automation = new G4AutomationModel
            {
                Authentication = authentication,
                DataSource = dataSource,
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                },
                Settings = new G4SettingsModel
                {
                    AutomationSettings = new AutomationSettingsModel
                    {
                        SearchTimeout = 1
                    }
                }
            };

            // Apply automation stages
            for (int i = 0; i < 4; i++)
            {
                NewAutomationStage(automation);
            }

            // Return the final automation model after all stages have been applied
            return automation;
        }

        // Creates a new automation stage for the provided automation model.
        private static void NewAutomationStage(G4AutomationModel automation)
        {
            // Create a login job with login rules
            var loginJob = new G4JobModel
            {
                Reference = new()
                {
                    Name = TestBase.NewRandomString(5),
                    Description = TestBase.NewRandomString(5),
                },
                Rules = NewLoginRules()
            };

            // Create an assertion job with assertion rules
            var assertionJob = new G4JobModel
            {
                Reference = new()
                {
                    Name = TestBase.NewRandomString(5),
                    Description = TestBase.NewRandomString(5),
                },
                Rules = NewAssertionRules()
            };

            // Create a new stage with login and assertion jobs
            var stage = new G4StageModel
            {
                Name = TestBase.NewRandomString(10),
                Description = TestBase.NewRandomString(10),
                Jobs =
                [
                    loginJob,
                    assertionJob
                ]
            };

            // Ensure the stages collection is initialized
            automation.Stages ??= [];

            // Add the new stage to the automation model
            automation.Stages = automation.Stages.Concat([stage]);
        }

        // Creates a collection of new assertion rules.
        private static IEnumerable<G4RuleModelBase> NewAssertionRules() =>
        [
            // Rule for asserting the value of the status input field.
            new ActionRuleModel
            {
                Argument = "{{$ --Condition:ElementAttribute --Operator:Eq --Expected:OK}}",
                OnAttribute = "value",
                OnElement = "//input[@id='status']",
                PluginName = "Assert"
            },
            // Rule for asserting the value of the status input field
            new ActionRuleModel
            {
                Argument = "{{$ --Condition:ElementAttribute --Operator:Eq --Expected:OK}}",
                OnAttribute = "value",
                OnElement = "//input[@id='status']",
                PluginName = "Assert"
            },
            // Rule for asserting the value of the status input field
            new ActionRuleModel
            {
                Argument = "{{$ --Condition:ElementAttribute --Operator:Eq --Expected:OK}}",
                OnAttribute = "value",
                OnElement = "//input[@id='status']",
                PluginName = "Assert"
            }
        ];

        // Creates a new data source model with JSON data.
        private static G4DataProviderModel NewJsonDataSource()
        {
            // Sample data for the JSON data source
            var jsonData = new[]
            {
                new
                {
                    Id = 1,
                    Name = "John Doe",
                    Age = 30,
                    Email = "john.doe@example.com",
                    City = "New York"
                },
                new
                {
                    Id = 2,
                    Name = "Jane Smith",
                    Age = 25,
                    Email = "jane.smith@example.com",
                    City = "Los Angeles"
                },
                new
                {
                    Id = 3,
                    Name = "Bob Johnson",
                    Age = 35,
                    Email = "bob.johnson@example.com",
                    City = "Chicago"
                }
            };

            // Serialize the JSON data
            var serializedJsonData = JsonSerializer.Serialize(jsonData);

            // Create and return a new G4DataProviderModel instance
            return new G4DataProviderModel
            {
                Type = "Json",
                Source = serializedJsonData
            };
        }

        // Creates a collection of new login rules.
        private static IEnumerable<G4RuleModelBase> NewLoginRules() =>
        [
            // Rule for sending keys to the username input field.
            new ActionRuleModel
            {
                Argument = "{{@Name}}",
                OnElement = "//input[@id='username']",
                PluginName = "SendKeys"
            },
            // Rule for sending keys to the password input field.
            new ActionRuleModel
            {
                Argument = "{{@Id}}",
                OnElement = "//input[@id='none']",
                PluginName = "SendKeys"
            },
            // Rule for invoking a click on the login button.
            new ActionRuleModel
            {
                OnElement = "//input[@id='Login']",
                PluginName = "InvokeClick"
            }
        ];
    }
}
