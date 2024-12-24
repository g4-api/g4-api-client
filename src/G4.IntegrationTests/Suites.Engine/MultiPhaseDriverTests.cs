using G4.Extensions;
using G4.IntegrationTests.Engine;
using G4.IntegrationTests.Extensions;
using G4.IntegrationTests.Framework;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Linq;

namespace G4.IntegrationTests.Suites.Engine
{
    [TestClass]
    [TestCategory("System")]
    [TestCategory("Integration")]
    [TestCategory("Engine")]
    public class MultiPhaseDriverTests : TestBase
    {
        [TestMethod(displayName: "Verify that two WebDriver instances are initiated when a " +
            "job takes the driver from its parent stage.")]
        #region *** Data Set ***
        [DataRow("Default()")]
        [DataRow("Stage()")]
        [DataRow(null)]
        [DataRow("")]
        #endregion
        public void GetJobDriverTest(string jobDriver)
        {
            // Initialize the automation environment with the current test context
            var environment = new AutomationEnvironment(TestContext).AddTestParameter(key: "jobDriver", jobDriver);

            // Create test options for a non-UI test scenario
            var testOptions = new NonUiTestOptions(environment);

            // Invoke the test case C0001 with the specified test options and retrieve the response
            var response = Invoke<C0001>(testOptions);

            // Extract driver session IDs from the performance points in the flat request
            var driverSessions = response.ResponseData.PerformancePoints.Select(i => i.DriverSession);

            // Extract the automation driver session ID from the main performance point
            var automationDriverSession = response.PerformancePoint.DriverSession;

            // Combine both driver session IDs and ensure they are distinct
            var sessions = driverSessions.Concat([automationDriverSession]).Distinct().ToArray();

            // Assert that exactly two unique driver sessions were created
            Assert.IsTrue(
                condition: sessions.Length == 2,
                message: "Expected two distinct WebDriver sessions to be initiated.");

            // Assert that none of the plugins within the job are using the automation driver
            // They must be using the driver created by the stage instead
            Assert.IsTrue(
                condition: response.ResponseData.PerformancePoints.All(i => i.DriverSession != automationDriverSession),
                message: "All plugins within the job must use the stage driver, not the automation driver."
            );

            // Assert that the 'TestParameter' has the expected value after base64 decoding
            Assert.AreEqual(
                expected: "Foo Bar",
                actual: $"{response.Environment.SessionParameters["TestParameter"]}".ConvertFromBase64(),
                message: "The 'TestParameter' should be 'Foo Bar' after decoding.");
        }

        [TestMethod(displayName: "Verify that a stage can take a driver from the preceding " +
            "stage or use the default driver from the parent automation.")]
        #region *** Data Set ***
        [DataRow("Default()")]
        [DataRow("Stage(1)")]
        [DataRow(null)]
        [DataRow("")]
        #endregion
        public void GetStageDriver(string stageDriver)
        {
            // Initialize the automation environment with the current test context and add the stageDriver parameter
            var environment = new AutomationEnvironment(TestContext).AddTestParameter(key: "stageDriver", stageDriver);

            // Create test options for a non-UI test scenario using the configured environment
            var testOptions = new NonUiTestOptions(environment);

            // Invoke the test case C0002 with the specified test options and retrieve the response
            var response = Invoke<C0002>(testOptions);

            // Extract driver session IDs from the performance points in the flat request
            var driverSessions = response.ResponseData.PerformancePoints.Select(i => i.DriverSession);

            // Extract the automation driver session ID from the main performance point
            var automationDriverSession = response.PerformancePoint.DriverSession;

            // Combine both driver session IDs and ensure they are distinct
            var sessions = driverSessions.Concat([automationDriverSession]).Distinct().ToArray();

            // Assert that exactly two unique driver sessions were created
            Assert.IsTrue(
                condition: sessions.Length == 2,
                message: "Expected two distinct WebDriver sessions to be initiated."
            );

            // Assert that the environment's session parameters count is exactly two
            Assert.IsTrue(
                condition: response.Environment.SessionParameters.Count == 2,
                message: "Expected exactly two session parameters to be present."
            );

            // Assert that all session parameters have the expected decoded value "Foo Bar"
            Assert.IsTrue(
                condition: response.Environment.SessionParameters.All(i => $"{i.Value}".ConvertFromBase64() == "Foo Bar"),
                message: "All session parameters should have the decoded value 'Foo Bar'."
            );
        }

        [TestMethod(displayName: "Verify that the automation driver is correctly retrieved " +
            "and no exceptions occur during GetStateDriver invocation.")]
        public void GetStateDriver()
        {
            // Initialize the automation environment with the current test context.
            var environment = new AutomationEnvironment(TestContext);

            // Create test options using the initialized environment.
            var testOptions = new NonUiTestOptions(environment);

            // Invoke the plugin with test options and capture the response.
            var response = Invoke<C0003>(testOptions);

            // Extract the automation driver session from the response's performance point.
            var automationDriver = response.PerformancePoint.DriverSession;

            // Add the automation driver ID to the test parameters for use in subsequent invocations.
            environment.TestParameters.Add(key: "automationDriver", $"Id({automationDriver})");

            // Update test options with the modified environment containing the automation driver.
            testOptions = new NonUiTestOptions(environment);

            // Invoke a nested plugin action (C0003A) with the updated test options and capture the response.
            response = Invoke<C0003.C0003A>(testOptions);

            // Assert that no exceptions were captured in the response's response data.
            Assert.IsFalse(response.ResponseData.Exceptions.Any(), "Exceptions were found during the GetStateDriver invocation.");
        }
    }
}
