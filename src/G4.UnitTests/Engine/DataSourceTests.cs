using G4.Api;
using G4.Exceptions;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Engine")]
    [TestCategory("DataSource")]
    [TestCategory("UnitTest")]
    public class DataSourceTests
    {
        // Rows whose Name values hold the characters that break naive placeholder replacement:
        // regex substitutions ('$1', '$$'), quotes, and backslashes.
        private static readonly string[] SampleNames = ["US$1", "Pa$$word", "say \"hi\"", @"C:\data\new"];

        /// <summary>
        /// Gets or sets the test context that supplies the licensed username used by the automations.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify that a Base64 source runs the automation once per row and resolves the columns.")]
        public void Base64SourceRunsOncePerRowTest()
        {
            // Arrange: the rows as Base64 of their JSON array, the shape Test Wright sends.
            var source = ConvertToBase64(text: NewRowsJson(names: SampleNames));
            var automation = NewAutomation(TestContext, dataSource: new G4DataProviderModel { Type = "Json", Source = source });

            // Act: invoke the automation and capture each initialized row automation.
            var automations = InvokeAndCapture(automation);

            // Assert: one automation per row, each with its own value exactly as written.
            AssertRows(expected: SampleNames, automations);
        }

        [TestMethod(DisplayName = "Verify that a Base64 source with a byte order mark is accepted.")]
        public void Base64SourceWithByteOrderMarkTest()
        {
            // Arrange: UTF-8 bytes with a leading byte order mark, as some editors write them.
            var json = NewRowsJson(names: ["a", "b"]);
            var bytes = Encoding.UTF8
                .GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(json))
                .ToArray();
            var source = Convert.ToBase64String(bytes);
            var automation = NewAutomation(TestContext, dataSource: new G4DataProviderModel { Type = "Json", Source = source });

            // Act: invoke the automation and capture each initialized row automation.
            var automations = InvokeAndCapture(automation);

            // Assert: both rows ran, so the byte order mark did not hide the JSON start.
            AssertRows(expected: ["a", "b"], automations);
        }

        [TestMethod(DisplayName = "Verify that an empty array runs the automation zero times.")]
        public void EmptyArrayRunsZeroTimesTest()
        {
            // Arrange: an empty array, as Base64.
            var dataSource = new G4DataProviderModel { Type = "Json", Source = ConvertToBase64(text: "[]") };
            var automation = NewAutomation(TestContext, dataSource);

            // Act: invoke the automation and capture each initialized row automation.
            var automations = InvokeAndCapture(automation);

            // Assert: nothing ran, and no error was raised for the empty array.
            Assert.IsEmpty(automations);
        }

        [TestMethod(DisplayName = "Verify that a file holding Base64 is accepted, and the data source is not changed.")]
        public void FileWithBase64SourceTest()
        {
            // Arrange: a temporary file that holds the Base64 of the rows, so the source is a file path.
            var path = Path.Combine(Path.GetTempPath(), $"g4-data-source-{Guid.NewGuid():N}.txt");
            File.WriteAllText(path, contents: ConvertToBase64(text: NewRowsJson(names: ["x", "y"])));
            var dataSource = new G4DataProviderModel { Type = "Json", Source = path };
            var automation = NewAutomation(TestContext, dataSource);

            try
            {
                // Act: invoke the automation and capture each initialized row automation.
                var automations = InvokeAndCapture(automation);

                // Assert: both rows ran from the decoded file contents.
                AssertRows(expected: ["x", "y"], automations);

                // Assert: the caller's data source still holds the path and no filter, so it was only read.
                Assert.AreEqual(expected: path, actual: dataSource.Source);
                Assert.IsNull(dataSource.Filter);
                Assert.AreSame(expected: dataSource, actual: automation.DataSource);
            }
            finally
            {
                // Cleanup: remove the temporary file, so repeated runs never read a stale source.
                File.Delete(path);
            }
        }

        [TestMethod(DisplayName = "Verify that the filter selects the rows that run.")]
        public void FilterSelectsRowsTest()
        {
            // Arrange: a Base64 source filtered to one row.
            var automation = NewAutomation(TestContext, dataSource: new G4DataProviderModel
            {
                Filter = "Name = 'b'",
                Source = ConvertToBase64(text: NewRowsJson(names: ["a", "b", "c"])),
                Type = "Json"
            });

            // Act: invoke the automation and capture each initialized row automation.
            var automations = InvokeAndCapture(automation);

            // Assert: only the filtered row ran.
            AssertRows(expected: ["b"], automations);
        }

        [TestMethod(DisplayName = "Verify that an invalid filter fails the invocation with a clear message.")]
        public void InvalidFilterFailsWithClearMessageTest()
        {
            // Arrange: a filter on a column the rows do not have.
            var automation = NewAutomation(TestContext, dataSource: new G4DataProviderModel
            {
                Filter = "Missing = 'a'",
                Source = NewRowsJson(names: ["a"]),
                Type = "Json"
            });

            // Act: invoke the automation, which fails before any row runs.
            var exception = Assert.ThrowsExactly<G4DataSourceException>(() => new G4Client().Automation.Invoke(automation));

            // Assert: the message names the filter.
            Assert.IsTrue(
                condition: exception.Message.Contains("filter 'Missing = 'a'' is not valid", StringComparison.Ordinal),
                message: $"Unexpected message: {exception.Message}");
        }

        [TestMethod(DisplayName = "Verify that a source that cannot be read fails the invocation with a clear message.")]
        #region *** Data Set ***
        [DataRow("Json", "not a json array!", "is not a file path, a JSON array, or the Base64 encoding of a JSON array")]
        [DataRow("Json", "bm90IGpzb24=", "is not valid JSON")]
        [DataRow("Json", "eyJOYW1lIjoiYSJ9", "must be a JSON array of rows, but it is a JSON Object")]
        [DataRow("Json", "{\"Name\":\"a\"}", "must be a JSON array of rows, but it is a JSON Object")]
        [DataRow("Json", "[{\"Name\":", "is not valid JSON")]
        [DataRow("Xml", "[{\"Name\":\"a\"}]", "The data source type 'Xml' is not supported")]
        #endregion
        public void InvalidSourceFailsWithClearMessageTest(string type, string source, string message)
        {
            // Arrange: 'bm90IGpzb24=' is Base64 of 'not json'; 'eyJOYW1lIjoiYSJ9' is Base64 of '{"Name":"a"}'.
            var automation = NewAutomation(TestContext, dataSource: new G4DataProviderModel { Type = type, Source = source });

            // Act: invoke the automation, which fails before any row runs.
            var exception = Assert.ThrowsExactly<G4DataSourceException>(() => new G4Client().Automation.Invoke(automation));

            // Assert: the message names the problem.
            Assert.IsTrue(
                condition: exception.Message.Contains(message, StringComparison.Ordinal),
                message: $"Unexpected message: {exception.Message}");
        }

        [TestMethod(DisplayName = "Verify that a plain JSON source and a missing type behave as before.")]
        public void PlainJsonSourceWithoutTypeTest()
        {
            // Arrange: plain JSON and no type, which means Json.
            var dataSource = new G4DataProviderModel { Source = NewRowsJson(names: SampleNames) };
            var automation = NewAutomation(TestContext, dataSource);

            // Act: invoke the automation and capture each initialized row automation.
            var automations = InvokeAndCapture(automation);

            // Assert: one automation per row.
            AssertRows(expected: SampleNames, automations);
        }

        // Asserts that each expected value ran in its own automation. Rows may run in parallel and in any order,
        // so both sides are sorted before the ordered comparison.
        private static void AssertRows(string[] expected, IReadOnlyCollection<G4AutomationModel> automations)
        {
            // Assert: one automation per expected row.
            Assert.HasCount(expected: expected.Length, collection: automations);

            // Assert: the resolved column values match the rows, regardless of run order.
            var arguments = automations
                .SelectMany(i => i.Stages)
                .SelectMany(i => i.Jobs)
                .SelectMany(i => i.Rules)
                .Select(i => i.Argument);
            var expectedValues = expected.Order(StringComparer.Ordinal);
            var actualValues = arguments.Order(StringComparer.Ordinal);
            Assert.AreSequenceEqual(expected: expectedValues, actual: actualValues);
        }

        // Encodes text as Base64 of its UTF-8 bytes, the way clients encode a data source.
        private static string ConvertToBase64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        // Invokes an automation and captures every row automation, as initialized just before invocation.
        // Rows run in parallel, so the automations are collected in a concurrent bag.
        private static List<G4AutomationModel> InvokeAndCapture(G4AutomationModel automation)
        {
            // Arrange: subscribe to the initialization event, which carries each row's resolved automation.
            var automations = new ConcurrentBag<G4AutomationModel>();
            var client = new G4Client().Automation;
            client.AutomationRequestInitialized += (_, queueModel) => automations.Add(queueModel.Status.Automation);

            // Act: run every row of the automation.
            client.Invoke(automation);

            return [.. automations];
        }

        // Creates a simulator automation with one NoAction rule that reads the Name column.
        private static G4AutomationModel NewAutomation(TestContext testContext, G4DataProviderModel dataSource)
        {
            return new G4AutomationModel
            {
                Authentication = new AuthenticationModel
                {
                    Username = $"{testContext.Properties["G4.Username"]}"
                },
                DataSource = dataSource,
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                },
                Stages =
                [
                    new G4StageModel
                    {
                        Jobs =
                        [
                            new G4JobModel
                            {
                                Rules =
                                [
                                    new ActionRuleModel
                                    {
                                        Argument = "{{$ columns.Name }}",
                                        PluginName = "NoAction"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            };
        }

        // Creates the JSON array of rows with one Name column.
        private static string NewRowsJson(string[] names)
        {
            return JsonSerializer.Serialize(names.Select(name => new { Name = name }));
        }
    }
}
