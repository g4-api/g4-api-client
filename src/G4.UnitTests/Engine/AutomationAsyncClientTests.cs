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
    public class AutomationAsyncClientTests : TestBase
    {
        [TestMethod(displayName: "Verify that a single automation is enqueued when no data is used")]
        public void QueueNewAutomationTest()
        {
            // Instantiate a new G4Client and retrieve its asynchronous automation client
            var client = new G4Client();
            var asyncClient = client.AutomationAsync;

            // Create an automation model with 1 stage and without data using the test context
            var automation = NewAutomation(TestContext, numberOfStages: 1, useData: false);

            // Retrieve the current pending automations from the async client's queue manager
            var pendingQueue = asyncClient.QueueManager.Pending;

            // Add the new automation to the pending queue
            asyncClient.AddPendingAutomation(automation);

            // Assert that exactly one automation has been added to the pending queue
            Assert.AreEqual(expected: 1, actual: pendingQueue.Count);
        }

        [TestMethod(displayName: "Verify that multiple automations are enqueued when data is provided")]
        public void QueueNewAutomationWithDataTest()
        {
            // Instantiate a new G4Client and retrieve its asynchronous automation client
            var client = new G4Client();
            var asyncClient = client.AutomationAsync;

            // Create an automation model with data using the test context
            var automation = NewAutomation(TestContext);

            // Retrieve the current pending automations from the async client's queue manager
            var pendingQueue = asyncClient.QueueManager.Pending;

            // Add the new automation to the pending queue
            asyncClient.AddPendingAutomation(automation);

            // Assert that exactly three automations (expected based on the provided data) are in the pending queue
            Assert.AreEqual(expected: 3, actual: pendingQueue.Count);
        }

        [TestMethod(displayName: "Verify that pending automation is accepted and moved to active " +
            "queue when automation is queued")]
        public void QueueNewActiveAutomationTest()
        {
            // Instantiate a new G4Client and retrieve its asynchronous automation client.
            var client = new G4Client();
            var asyncClient = client.AutomationAsync;

            // Create an automation model with test context data.
            var automation = NewAutomation(TestContext);

            // Retrieve the pending and active automation queues from the async client's queue manager.
            var pendingQueue = asyncClient.QueueManager.Pending;
            var activeQueue = asyncClient.QueueManager.Active;

            // Add the new automation to the pending queue.
            asyncClient.AddPendingAutomation(automation);

            // Assert that exactly three automations are in the pending queue.
            Assert.AreEqual(expected: 3, actual: pendingQueue.Count);

            // Retrieve the next pending automation.
            var pendingAutomation = asyncClient.GetPendingAutomation();

            // Assert that the pending queue count decreases to two after retrieving one automation.
            Assert.AreEqual(expected: 2, actual: pendingQueue.Count);

            // Verify that the status of the retrieved pending automation is 'Accepted'.
            Assert.AreEqual(
                expected: G4QueueModel.QueueStatusCodes.New,
                actual: pendingAutomation.Status.ProgressStatus.Status);

            // Move the pending automation to the active queue.
            asyncClient.AddActiveAutomation(pendingAutomation);

            // Retrieve the first active automation from the active queue.
            var firstActiveAutomation = activeQueue.First().Value.First().Value;

            // Assert that there is exactly one group in the active queue.
            Assert.AreEqual(expected: 1, actual: activeQueue.Count);

            // Verify that the status of the active automation is 'Processing'.
            Assert.AreEqual(
                expected: G4QueueModel.QueueStatusCodes.Processing,
                actual: firstActiveAutomation.Status.ProgressStatus.Status);
        }

        // Creates a new automation model with the provided testContext.
        private static G4AutomationModel NewAutomation(TestContext testContext)
        {
            return NewAutomation(testContext, numberOfStages: 4, useData: true);
        }

        // Creates a new automation model with the provided testContext.
        private static G4AutomationModel NewAutomation(TestContext testContext, int numberOfStages, bool useData)
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
            for (int i = 0; i < numberOfStages; i++)
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
                Rules = NewLoginRules()
            };

            // Create an assertion job with assertion rules
            var assertionJob = new G4JobModel
            {
                Rules = NewAssertionRules()
            };

            // Create a new stage with login and assertion jobs
            var stage = new G4StageModel
            {
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
                Argument = "{{$ columns.Name }}",
                OnElement = "//positive[@id='{{$New-Date}}']",
                PluginName = "SendKeys"
            },
            // Rule for sending keys to the password input field.
            new ActionRuleModel
            {
                Argument = "{{$ columns.Id }}",
                OnElement = "//input[@id='none']",
                PluginName = "SendKeys"
            },
            // Rule for invoking a click on the login button.
            new ActionRuleModel
            {
                OnElement = "//positive[@id='Login']",
                PluginName = "InvokeClick"
            }
        ];
    }
}
