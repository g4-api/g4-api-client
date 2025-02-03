using G4.Api;
using G4.Attributes;
using G4.Extensions;
using G4.Models;
using G4.Plugins.Ui.Actions;
using G4.UnitTests.Attributes;
using G4.UnitTests.Framework;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Engine")]
    [TestCategory("G4Client")]
    [TestCategory("UnitTest")]
    public class G4ClientTests : TestBase
    {
        [TestMethod("Verify that parameters are correctly applied in the template rules")]
        public void TemplateTest()
        {
            // Read the JSON template file and remove any extra whitespace
            var json = File.ReadAllText("Resources/LoginTemplate.txt").Trim();

            // Deserialize the JSON into a G4PluginAttribute object using your predefined JsonOptions
            var manifest = JsonSerializer.Deserialize<G4PluginAttribute>(json, JsonOptions);

            // Instantiate a new G4Client which manages your templates
            var client = new G4Client();

            // Retrieve the current templates; expecting no templates initially
            var templates = client.Templates.GetTemplates();
            Assert.AreEqual(0, templates.Count(), "Expected no templates before adding a new one.");

            // Add a new template based on the manifest loaded from the JSON file
            client.Templates.AddTemplate(manifest);

            // Retrieve the templates again after adding the new template; expecting one template now
            templates = client.Templates.GetTemplates();
            Assert.AreEqual(1, templates.Count(), "Expected one template after adding a new one.");

            // Construct the JSON string for an action rule, including the $type field and the plugin parameters
            var ruleJson = "{" +
                "\"$type\":\"Action\"," +
                "\"pluginName\":\"" + manifest.Key + "\"," +
                "\"argument\":\"{{$ --Username:Foo --Password:Bar}}\"}";

            // Invoke the action rule (this method processes the rule JSON as needed)
            Invoke(ruleJson);
        }

        [TestMethod(displayName: "Verify the data provider when data is provided")]
        public void DataProviderWithDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve data providers from the sessions and flatten them into a single dictionary
            var dataProviders = client
                .Invoke(automation)                       // Invoke the automation and get the response
                .SelectMany(i => i.Value.Sessions)        // Flatten the sessions
                .SelectMany(i => i.Value.DataProvider)    // Flatten the data providers within sessions
                .ToDictionary(k => $"{k["Id"]}", v => v); // Convert the flattened data providers into a dictionary

            Assert.AreEqual(
                expected: 1L,
                actual: dataProviders["1"]["Id"],
                message: "Id of data provider 1 does not match.");

            Assert.AreEqual(
                expected: "John Doe",
                actual: dataProviders["1"]["Name"],
                message: "Name of data provider 1 does not match.");

            Assert.AreEqual(
                expected: 30L,
                actual: dataProviders["1"]["Age"],
                message: "Age of data provider 1 does not match.");

            Assert.AreEqual(
                expected: "john.doe@example.com",
                actual: dataProviders["1"]["Email"],
                message: "Email of data provider 1 does not match.");

            Assert.AreEqual(
                expected: "New York",
                actual: dataProviders["1"]["City"],
                message: "City of data provider 1 does not match.");

            Assert.AreEqual(
                expected: 2L,
                actual: dataProviders["2"]["Id"],
                message: "Id of data provider 2 does not match.");

            Assert.AreEqual(
                expected: "Jane Smith",
                actual: dataProviders["2"]["Name"],
                message: "Name of data provider 2 does not match.");

            Assert.AreEqual(
                expected: 25L,
                actual: dataProviders["2"]["Age"],
                message: "Age of data provider 2 does not match.");

            Assert.AreEqual(
                expected: "jane.smith@example.com",
                actual: dataProviders["2"]["Email"],
                message: "Email of data provider 2 does not match.");

            Assert.AreEqual(
                expected: "Los Angeles",
                actual: dataProviders["2"]["City"],
                message: "City of data provider 2 does not match.");

            Assert.AreEqual(
                expected: 3L,
                actual: dataProviders["3"]["Id"],
                message: "Id of data provider 3 does not match.");

            Assert.AreEqual(
                expected: "Bob Johnson",
                actual: dataProviders["3"]["Name"],
                message: "Name of data provider 3 does not match.");

            Assert.AreEqual(
                expected: 35L,
                actual: dataProviders["3"]["Age"],
                message: "Age of data provider 3 does not match.");

            Assert.AreEqual(
                expected: "bob.johnson@example.com",
                actual: dataProviders["3"]["Email"],
                message: "Email of data provider 3 does not match.");

            Assert.AreEqual(
                expected: "Chicago",
                actual: dataProviders["3"]["City"],
                message: "City of data provider 3 does not match.");
        }

        [TestMethod(displayName: "Verify the data provider when data is not provided")]
        public void DataProviderWithoutDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Check if there are any data providers
            var hasData = client
                .Automation
                .Invoke(automation)                    // Invoke the automation and get the response
                .SelectMany(i => i.Value.Sessions)     // Flatten the sessions
                .SelectMany(i => i.Value.DataProvider) // Flatten the data providers within sessions
                .SelectMany(i => i.Keys)               // Select only the keys of each data provider
                .Any();                                // Check if there are any data providers

            // Assert that there are no data providers
            Assert.IsFalse(hasData, "Data providers must be empty when automation is created without using data.");
        }

        [RetryableTestMethod(
            numberOfAttempts: 5,
            displayName: "Verify the count of exceptions in the flat request when data is provided",
            DelayBetweenAttempts = 3000)]
        public void FlatRequestExceptionCountTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve all exceptions from the sessions and flatten them into a single collection
            var flatRequest = client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .Exceptions;

            // Assert that the count of exceptions in the flat request is 4
            Assert.AreEqual(4, flatRequest.Count());
        }

        [TestMethod(displayName: "Verify the count of performance points in the flat request when data is provided")]
        public void FlatRequestPerformancePointCountTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve all performance points from the sessions and flatten them into a single collection
            var flatRequest = client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints;

            // Assert that the count of performance points in the flat request is 48
            Assert.AreEqual(48, flatRequest.Count());
        }

        [TestMethod(displayName: "Verify the response size of the flat request when data is provided")]
        public void FlatRequestResponseSizeTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve the response size from the flat request of the first
            // session in the response of the automation invocation
            var responseSize = client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .ResponseSize;

            // Assert that the response size is greater than zero
            Assert.IsTrue(responseSize > 0);
        }

        [TestMethod(displayName: "Verify the performance point automation reference details " +
            "in the flat request when data is provided")]
        public void PerformancePointAutomationReferenceDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve the AutomationReference from the performance point reference in
            // the flat request of the first session in the response of the automation
            // invocation and cast it to PluginPerformancePointModel instance for assertions to be made on it
            var reference = ((G4PluginPerformancePointModel)client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints
                .First())
                .Reference
                .JobReference
                .StageReference
                .AutomationReference;

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the reference is not null
            Assert.IsNotNull(value: reference, message: "Reference must not be null.");

            // Assert that the reference's Description is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Description), message: "Reference description must not be null or empty.");

            // Assert that the reference's Name is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Name), message: "Reference name must not be null or empty.");

            // Assert that the reference's Id matches the default guid format (00000000-0000-0000-0000-000000000000)
            Assert.IsTrue(condition: Regex.IsMatch(reference.Id, guidPattern), message: "Reference id must match the default guid format.");
        }

        [RetryableTestMethod(
            numberOfAttempts: 5,
            displayName: "Verify the performance point details in the flat request when data is provided",
            DelayBetweenAttempts = 3000)]
        public void PerformancePointDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve all performance points from the sessions and flatten them into a single collection
            var performancePoints = client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints;

            // Assert that the performance point details in the flat request are correct
            foreach (var point in performancePoints)
            {
                // Cast the performance point to PluginPerformancePointModel instance for assertions to be made on it
                var performancePoint = (G4PluginPerformancePointModel)point;

                // Assert that the start date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.Start.Day, actual: DateTime.UtcNow.Day, "Start day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.Start.Month, actual: DateTime.UtcNow.Month, "Start month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.Start.Year, actual: DateTime.UtcNow.Year, "Start year must match the current UTC year.");

                // Assert that the end date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.End.Day, actual: DateTime.UtcNow.Day, "End day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.End.Month, actual: DateTime.UtcNow.Month, "End month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.End.Year, actual: DateTime.UtcNow.Year, "End year must match the current UTC year.");

                // Assert that the run time is greater than zero
                Assert.IsTrue(condition: performancePoint.RunTime > 0, "Run time must be greater than zero.");

                // Assert that the setup delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupDelegationTime > 0, "Setup delegation time must be greater than zero.");

                // Assert that the setup time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupTime > 0, "Setup time must be greater than zero.");

                // Assert that the teardown delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownDelegationTime > 0, "Teardown delegation time must be greater than zero.");

                // Assert that the teardown time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownTime > 0, "Teardown time must be greater than zero.");

                // Assert that the reference is not null
                Assert.IsNotNull(performancePoint.Reference, "Reference must not be null.");
            }

            // Assert that the timeouts time is greater than zero
            Assert.IsTrue(condition: performancePoints.Sum(i => i.Timeouts) > 0, "Timeouts time must be greater than zero.");
        }

        [TestMethod(displayName: "Verify the performance point job reference details in the " +
            "flat request when data is provided")]
        public void PerformancePointJobReferenceDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve the JobReference from the performance point reference in
            // the flat request of the first session in the response of the automation
            // invocation and cast it to PluginPerformancePointModel instance for assertions to be made on it
            var reference = ((G4PluginPerformancePointModel)client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints
                .First())
                .Reference
                .JobReference;

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the reference is not null
            Assert.IsNotNull(value: reference, message: "Reference must not be null.");

            // Assert that the reference's Description is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Description), message: "Reference description must not be null or empty.");

            // Assert that the reference's Name is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Name), message: "Reference name must not be null or empty.");

            // Assert that the reference's Id matches the default guid format (00000000-0000-0000-0000-000000000000)
            Assert.IsTrue(condition: Regex.IsMatch(reference.Id, guidPattern), message: "Reference id must match the default guid format.");

            // Assert that the reference's StageReference is not null
            Assert.IsNotNull(value: reference.StageReference, message: "Reference stage reference must not be null.");
        }

        [TestMethod(displayName: "Verify the performance point reference details in the " +
            "flat request when data is provided")]
        public void PerformancePointReferenceDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: true);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the performance point reference from the flat request of the first session in
            // the response of the automation invocation and cast it to PluginPerformancePointModel
            // instance for assertions to be made on it
            var reference = ((G4PluginPerformancePointModel)client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .Last()
                .Value
                .ResponseData
                .PerformancePoints
                .First())
                .Reference;

            // Get the summary from the SendKeys plugin manifest and join it with a
            // newline character to form a string summary of the plugin manifest for
            // comparison with the reference's description property later on in the test method
            var summary = string
                .Join('\n', new SendKeys(pluginSetup: default).GetManifest().Summary)
                .Replace("\n", string.Empty)
                .Replace("  ", " ");

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the reference is not null
            Assert.IsNotNull(value: reference, message: "Reference must not be null.");

            // Assert the reference's Alias is 'SendKeys'
            Assert.AreEqual(expected: "SendKeys", actual: reference.Alias, "Reference alias must be 'SendKeys'.");

            // Assert that the reference's description match the manifest summary
            Assert.AreEqual(
                expected: summary,
                actual: reference.Description.Replace("  ", " "),
                message: "Reference description must match the manifest summary.");

            // Assert that the reference's name is 'SendKeys'
            Assert.AreEqual(expected: "SendKeys", actual: reference.Name, message: "Reference name must be 'SendKeys'.");

            // Assert that the reference's id is not null & matches default guid format (00000000-0000-0000-0000-000000000000)
            Assert.IsNotNull(value: reference.Id, message: "Reference id must not be null.");

            // Assert that the reference's id matches the default guid format
            Assert.IsTrue(condition: Regex.IsMatch(reference.Id, guidPattern), message: "Reference id must match the default guid format.");

            // Assert that the reference's JobReference is not null
            Assert.IsNotNull(value: reference.JobReference, message: "Reference job reference must not be null.");
        }

        [TestMethod(displayName: "Verify the performance point stage reference details in " +
            "the flat request when data is provided")]
        public void PerformancePointStageReferenceDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client().Automation;

            // Retrieve the stage reference from the performance point reference in
            // the flat request of the first session in the response of the automation
            // invocation and cast it to PluginPerformancePointModel instance for assertions to be made on it
            var reference = ((G4PluginPerformancePointModel)client
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseData
                .PerformancePoints
                .First())
                .Reference
                .JobReference
                .StageReference;

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the reference is not null
            Assert.IsNotNull(value: reference, message: "Reference must not be null.");

            // Asseret that AutomationReference is not null
            Assert.IsNotNull(value: reference.AutomationReference, message: "Automation reference must not be null.");

            // Assert that the reference's Description is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Description), message: "Reference description must not be null or empty.");

            // Assert that the reference's Name is not null or empty
            Assert.IsFalse(condition: string.IsNullOrEmpty(reference.Name), message: "Reference name must not be null or empty.");

            // Assert that the reference's Id matches the default guid format (00000000-0000-0000-0000-000000000000)
            Assert.IsTrue(condition: Regex.IsMatch(reference.Id, guidPattern), message: "Reference id must match the default guid format.");
        }

        [TestMethod(displayName: "Verify the request size of the session is greater than " +
            "116000 when no data is provided")]
        public void RequestSizeTest()
        {
            // Create automation model without using data
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Invoke the automation and get the first session
            var session = client.Automation.Invoke(automation).First().Value.Sessions.First().Value;

            // Assert that the request size of the session is greater than 116000
            Assert.IsNotNull(session.RequestSize > 116000);
        }

        [TestMethod(displayName: "Verify the count of sessions in the response when data is provided")]
        public void SessionCountWithDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Invoke the automation and get the response
            var response = client.Automation.Invoke(automation).First().Value;

            // Assert that the sessions count in the response is not null and equals 3
            Assert.IsNotNull(response.Sessions.Count == 3);
        }

        [TestMethod(displayName: "Verify the count of sessions in the response when no data is provided")]
        public void SessionCountWithoutDataTest()
        {
            // Create automation model without using data
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Invoke the automation and get the response
            var response = client.Automation.Invoke(automation).First().Value;

            // Assert that the sessions count in the response is not null and equals 1
            Assert.IsNotNull(response.Sessions.Count == 1);
        }

        [RetryableTestMethod(
            numberOfAttempts: 5,
            displayName: "Verify the count of exceptions in the sessions when data is provided",
            DelayBetweenAttempts = 3000)]
        public void SessionExceptionsCountWithDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve all exceptions from the sessions and flatten them into a single collection
            var exceptions = client
                .Automation
                .Invoke(automation)
                .Values
                .SelectMany(i => i.Sessions.Values)
                .SelectMany(i => i.ResponseData.Exceptions);

            // Assert that the count of exceptions in the flattened collection is 12
            Assert.AreEqual(12, exceptions.Count());
        }

        [RetryableTestMethod(
            numberOfAttempts: 5,
            displayName: "Verify the count of exceptions in the session when no data is provided",
            DelayBetweenAttempts = 3000)]
        public void SessionExceptionsCountWithoutDataTest()
        {
            // Create automation model without using data
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Invoke the automation and get the session
            var session = client.Automation.Invoke(automation).First().Value.Sessions.First().Value;

            // Assert that the count of exceptions in the session's flat request is 4
            Assert.AreEqual(4, session.ResponseData.Exceptions.Count());
        }

        [TestMethod(displayName: "Verify the count of performance points in the sessions when data is provided")]
        public void SessionPerformancePointCountWithDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve all performance points from the sessions and flatten them into a single collection
            var performancePoints = client
                .Automation
                .Invoke(automation)
                .Values
                .SelectMany(i => i.Sessions.Values)
                .SelectMany(i => i.ResponseData.PerformancePoints);

            // Assert that the count of performance points in the flattened collection is 144
            Assert.AreEqual(144, performancePoints.Count());
        }

        [TestMethod(displayName: "Verify the count of performance points in the sessions when no data is provided")]
        public void SessionPerformancePointCountWithoutDataTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve all performance points from the sessions and flatten them into a single collection
            var performancePoints = client
                .Automation
                .Invoke(automation)
                .Values
                .SelectMany(i => i.Sessions.Values)
                .SelectMany(i => i.ResponseData.PerformancePoints);

            // Assert that the count of performance points in the flattened collection is 48
            Assert.AreEqual(48, performancePoints.Count());
        }

        [RetryableTestMethod(
            numberOfAttempts: 5,
            displayName: "Verify the performance point details of a session when no data is provided",
            DelayBetweenAttempts = 3000)]
        public void SessionPerformancePointTest()
        {
            // Create automation model without using data
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: false);

            // Create a new G4Client instance
            var client = new G4Client();

            // Invoke the automation and get the response
            var responses = client.Automation.Invoke(automation);

            // Iterate over each response in the collection of responses and assert the
            // performance point details of each session in the response collection
            foreach (var response in responses.Values)
            {
                // Cast the response's performance point to G4PerformancePointModel
                var performancePoint = (G4AutomationPerformancePointModel)response.PerformancePoint;

                // Assert that the session time is not zero
                Assert.AreNotEqual(notExpected: 0, actual: performancePoint.SessionTime, "Session time must not be zero.");

                // Assert that the start date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.Start.Day, actual: DateTime.UtcNow.Day, "Start day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.Start.Month, actual: DateTime.UtcNow.Month, "Start month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.Start.Year, actual: DateTime.UtcNow.Year, "Start year must match the current UTC year.");

                // Assert that the end date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.End.Day, actual: DateTime.UtcNow.Day, "End day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.End.Month, actual: DateTime.UtcNow.Month, "End month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.End.Year, actual: DateTime.UtcNow.Year, "End year must match the current UTC year.");

                // Assert that the run time is greater than zero
                Assert.IsTrue(condition: performancePoint.RunTime > 0, "Run time must be greater than zero.");

                // Assert that the setup delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupDelegationTime > 0, "Setup delegation time must be greater than zero.");

                // Assert that the setup time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupTime > 0, "Setup time must be greater than zero.");

                // Assert that the teardown delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownDelegationTime > 0, "Teardown delegation time must be greater than zero.");

                // Assert that the teardown time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownTime > 0, "Teardown time must be greater than zero.");

                // Assert that the timeouts time is greater than zero
                Assert.IsTrue(condition: performancePoint.Timeouts > 0, "Timeouts time must be greater than zero.");
            }
        }

        [TestMethod(displayName: "Verify the count of jobs in each stage of the structured " +
            "request when data is provided")]
        public void StructuredRequestJobsCountTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Assert that each stage in the structured request has 2 jobs
            foreach (var stage in stages)
            {
                Assert.AreEqual(2, stage.Jobs.Count());
            }
        }

        [TestMethod(displayName: "Verify the response size of the structured request is " +
            "greater than the flat request when data is provided")]
        public void StructuredRequestResponseSizeTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext, numberOfStages: 4, useData: true);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve sessions from the response of the automation
            // invocation and get the first session
            var response = client.Automation.Invoke(automation);

            // Get the first session from the response of the automation invocation
            var session = response.First().Value.Sessions.First().Value;

            // Retrieve the structured response size from the structured request of the first
            // session in the response of the automation invocation
            var structuredResponseSize = session
                .ResponseTree
                .ResponseSize;

            // Retrieve the flat response size from the flat request of the first
            // session in the response of the automation invocation
            var flatResponseSize = session
                .ResponseData
                .ResponseSize;

            // Assert that the response size of the structured request is greater than the flat request
            Assert.IsTrue(structuredResponseSize > flatResponseSize);
        }

        [TestMethod(displayName: "Verify the automation reference details of a stage in the structured request when data is provided")]
        public void StructuredRequestAutomationReferenceDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the automation reference details of each stage in the structured request are correct
            foreach (var stage in stages)
            {
                // Cast the stage's automation reference to G4AutomationReferenceModel
                var reference = stage.AutomationReference;

                // Assert that the reference is not null
                Assert.IsNotNull(reference, "Reference must not be null.");

                // Assert that the reference's Description is not null or empty
                Assert.IsFalse(string.IsNullOrEmpty(reference.Description), "Reference description must not be null or empty.");

                // Assert that the reference's Name is not null or empty
                Assert.IsFalse(string.IsNullOrEmpty(reference.Name), "Reference name must not be null or empty.");

                // Assert that the reference's Id matches the default guid format (00000000-0000-0000-0000-000000000000)
                Assert.IsTrue(Regex.IsMatch(reference.Id, guidPattern), "Reference id must match the default guid format.");
            }
        }

        [TestMethod(displayName: "Verify the count of stages in the structured request when data is provided")]
        public void StructuredRequestStagesCountTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Assert that the count of stages in the structured request is 4
            Assert.AreEqual(4, stages.Count());
        }

        [TestMethod(displayName: "Verify the stage details of a stage in the structured request when data is provided")]
        public void StructuredRequestStageDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Default guid pattern for matching the reference's id property with the default guid format
            const string guidPattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

            // Assert that the stage details of each stage in the structured request are correct
            foreach (var stage in stages)
            {
                // Assert that the stage's name is not null or empty
                Assert.IsFalse(string.IsNullOrEmpty(stage.Name), "Stage name must not be null or empty.");

                // Assert that the stage's description is not null or empty
                Assert.IsFalse(string.IsNullOrEmpty(stage.Description), "Stage description must not be null or empty.");

                // Assert that the reference's Id matches the default guid format (00000000-0000-0000-0000-000000000000)
                Assert.IsTrue(condition: Regex.IsMatch(stage.Id, guidPattern), message: "Reference id must match the default guid format.");

                // Assert that the stage's jobs count is 2
                Assert.AreEqual(2, stage.Jobs.Count());
            }
        }

        [TestMethod(displayName: "Verify the job details of a stage in the structured request when data is provided")]
        public void StructuredRequestStageJobsDetailsTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Assert that the job details of each stage in the structured request are correct
            foreach (var stage in stages)
            {
                // Assert that the stage's jobs count is 2
               Assert.AreEqual(
                   expected: 2,
                   actual: stage.Jobs.Count(),
                   message: "The count of jobs in the stage is not equal to 2.");

                // Assert that the job details of each job in the stage are correct
                foreach (var job in stage.Jobs)
                {
                    // Assert that the job's description is not null or empty
                    Assert.IsFalse(
                        condition: string.IsNullOrEmpty(job.Description),
                        message: "Job description must not be null or empty.");

                    // Assert that the job's id is not null or empty
                    Assert.IsFalse(condition: string.IsNullOrEmpty(job.Id), message: "Job id must not be null or empty.");

                    // Assert that the job's name is not null or empty
                    Assert.IsFalse(condition: string.IsNullOrEmpty(job.Name), message: "Job name must not be null or empty.");

                    // Assert that the job's performance point is not null
                    Assert.IsNotNull(value: job.PerformancePoint, message: "Job performance point must not be null.");

                    // Assert that the job's stage reference is not null
                    Assert.IsNotNull(value: job.StageReference, message: "Job stage reference must not be null.");
                }
            }
        }

        [TestMethod(displayName: "Verify the performance point details of a stage in the structured request when data is provided")]
        public void StructuredRequestStagePerformancePointTest()
        {
            // Create automation model using test context
            var automation = NewAutomation(TestContext);

            // Create a new G4Client instance
            var client = new G4Client();

            // Retrieve the stages from the structured request of the first
            // session in the response of the automation invocation
            var stages = client
                .Automation
                .Invoke(automation)
                .First()
                .Value
                .Sessions
                .First()
                .Value
                .ResponseTree
                .Stages;

            // Assert that the performance point details of each stage in the structured request are correct
            foreach (var stage in stages)
            {
                // Cast the stage's performance point to G4PerformancePointModel
                var performancePoint = (G4PerformancePointModel)stage.PerformancePoint;

                // Assert that the session time is not zero
                Assert.AreNotEqual(notExpected: 0, actual: performancePoint.SessionTime, "Session time must not be zero.");

                // Assert that the start date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.Start.Day, actual: DateTime.UtcNow.Day, "Start day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.Start.Month, actual: DateTime.UtcNow.Month, "Start month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.Start.Year, actual: DateTime.UtcNow.Year, "Start year must match the current UTC year.");

                // Assert that the end date's day, month, and year match the current UTC date
                Assert.AreEqual(expected: performancePoint.End.Day, actual: DateTime.UtcNow.Day, "End day must match the current UTC day.");
                Assert.AreEqual(expected: performancePoint.End.Month, actual: DateTime.UtcNow.Month, "End month must match the current UTC month.");
                Assert.AreEqual(expected: performancePoint.End.Year, actual: DateTime.UtcNow.Year, "End year must match the current UTC year.");

                // Assert that the run time is greater than zero
                Assert.IsTrue(condition: performancePoint.RunTime > 0, "Run time must be greater than zero.");

                // Assert that the setup delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupDelegationTime > 0, "Setup delegation time must be greater than zero.");

                // Assert that the setup time is greater than zero
                Assert.IsTrue(condition: performancePoint.SetupTime > 0, "Setup time must be greater than zero.");

                // Assert that the teardown delegation time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownDelegationTime > 0, "Teardown delegation time must be greater than zero.");

                // Assert that the teardown time is greater than zero
                Assert.IsTrue(condition: performancePoint.TeardownTime > 0, "Teardown time must be greater than zero.");

                // Assert that the timeouts time is greater than zero
                Assert.IsTrue(condition: performancePoint.Timeouts > 0, "Timeouts time must be greater than zero.");
            }
        }

        [TestMethod(displayName: "Verify that all automation events are triggered and counted correctly")]
        public void AutomationEventsTest()
        {
            // Dictionary to track the invocation status and count of various automation events
            var events = new Dictionary<string, (bool Invoked, int Count)>
            {
                ["AutomationCallback"] = (false, 0),
                ["AutomationInvoked"] = (false, 0),
                ["AutomationInvoking"] = (false, 0),
                ["AutomationRequestInitialized"] = (false, 0),
                ["AutomationStatusChanged"] = (false, 0),
                ["JobInvoked"] = (false, 0),
                ["JobInvoking"] = (false, 0),
                ["JobStatusChanged"] = (false, 0),
                ["LogCreated"] = (false, 0),
                ["LogCreating"] = (false, 0),
                ["OnRuleError"] = (false, 0),
                ["PluginCreated"] = (false, 0),
                ["RuleCallback"] = (false, 0),
                ["RuleInvoked"] = (false, 0),
                ["RuleInvoking"] = (false, 0),
                ["RuleStatusChanged"] = (false, 0),
                ["StageInvoked"] = (false, 0),
                ["StageInvoking"] = (false, 0),
                ["StageStatusChanged"] = (false, 0)
            };

            // Create a new instance of G4Client to work with automation events
            var client = new G4Client();

            // Subscribe to the Automation events and update the events dictionary on invocation
            client.Automation.AutomationCallback += (_, _)
                => events["AutomationCallback"] = (true, events["AutomationCallback"].Count + 1);

            client.Automation.AutomationInvoked += (_, _)
                => events["AutomationInvoked"] = (true, events["AutomationInvoked"].Count + 1);

            client.Automation.AutomationInvoking += (_, _)
                => events["AutomationInvoking"] = (true, events["AutomationInvoking"].Count + 1);

            client.Automation.AutomationRequestInitialized += (_, _)
                => events["AutomationRequestInitialized"] = (true, events["AutomationRequestInitialized"].Count + 1);

            client.Automation.AutomationStatusChanged += (_, _)
                => events["AutomationStatusChanged"] = (true, events["AutomationStatusChanged"].Count + 1);

            client.Automation.JobInvoked += (_, _)
                => events["JobInvoked"] = (true, events["JobInvoked"].Count + 1);

            client.Automation.JobInvoking += (_, _)
                => events["JobInvoking"] = (true, events["JobInvoking"].Count + 1);

            client.Automation.JobStatusChanged += (_, _)
                => events["JobStatusChanged"] = (true, events["JobStatusChanged"].Count + 1);

            client.Automation.LogCreated += (_, _)
                => events["LogCreated"] = (true, events["LogCreated"].Count + 1);

            client.Automation.LogCreating += (_, _)
                => events["LogCreating"] = (true, events["LogCreating"].Count + 1);

            client.Automation.OnRuleError += (_, _)
                => events["OnRuleError"] = (true, events["OnRuleError"].Count + 1);

            client.Automation.PluginCreated += (_, _)
                => events["PluginCreated"] = (true, events["PluginCreated"].Count + 1);

            client.Automation.RuleCallback += (_, _)
                => events["RuleCallback"] = (true, events["RuleCallback"].Count + 1);

            client.Automation.RuleInvoked += (_, _)
                => events["RuleInvoked"] = (true, events["RuleInvoked"].Count + 1);

            client.Automation.RuleInvoking += (_, _)
                => events["RuleInvoking"] = (true, events["RuleInvoking"].Count + 1);

            client.Automation.RuleStatusChanged += (_, _)
                => events["RuleStatusChanged"] = (true, events["RuleStatusChanged"].Count + 1);

            client.Automation.StageInvoked += (_, _)
                => events["StageInvoked"] = (true, events["StageInvoked"].Count + 1);

            client.Automation.StageInvoking += (_, _)
                => events["StageInvoking"] = (true, events["StageInvoking"].Count + 1);

            client.Automation.StageStatusChanged += (_, _)
                => events["StageStatusChanged"] = (true, events["StageStatusChanged"].Count + 1);

            // Create an automation model using the provided test context
            var automation = NewAutomation(TestContext);

            // Invoke the automation, which should trigger the events
            client.Automation.Invoke(automation);

            // Assert that all events have been invoked and their invocation count is greater than zero
            Assert.IsTrue(events.Values.All(i => i.Invoked && i.Count > 0));
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
