using G4.Abstraction.Cli;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Text.Json;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("CliFactory")]
    [TestCategory("UnitTest")]
    public class CliFactoryTests
    {
        [TestMethod(displayName: "Verify that the conversion of a CLI expression to a " +
            "dictionary of arguments is performed correctly")]
        public void ConvertToDictionaryBasicTest()
        {
            // Define a CLI expression with various arguments.
            const string expression = "{{$ " +
                "--header:content-type:application/json " +
                "--header:authorization:BearerToken123 " +
                "--arg1:input-file:example.txt " +
                "--arg2:output-folder:/path/to/output " +
                "--arg2:verbose:true " +
                "--arg3}}";

            // Convert the CLI expression to a dictionary of arguments.
            var arguments = new CliFactory().ConvertToDictionary(expression);

            // Deserialize headers from the arguments.
            var headers = JsonSerializer.Deserialize<string[]>(arguments["header"]);

            // Assert that there are two headers and a total of four arguments in the dictionary.
            Assert.IsTrue(headers.Length == 2);
            Assert.IsTrue(arguments.Count == 4);
        }

        [TestMethod(displayName: "Verify that the conversion of a nested CLI expression " +
            "to a dictionary of arguments is performed correctly.")]
        public void ConvertToDictionaryNestedExpressionTest()
        {
            // Define a CLI expression with various arguments, including a nested expression.
            const string expression = "{{$ " +
                "--header:content-type:application/json " +
                "--arg1:input-file:example.txt " +
                "--nested:{{$ --nested-arg1:value1 --nested-arg2:value2}}}}";

            // Convert the CLI expression to a dictionary of arguments.
            var arguments = new CliFactory().ConvertToDictionary(expression);

            // Define the expected nested expression.
            const string expected = "{{$ --nested-arg1:value1 --nested-arg2:value2}}";

            // Assert that the nested expression matches the expected value.
            Assert.AreEqual(expected, arguments["nested"]);

            // Assert that there are a total of three arguments in the dictionary.
            Assert.IsTrue(arguments.Count == 3);
        }
    }
}
