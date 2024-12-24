using G4.Extensions;
using G4.IntegrationTests.Extensions;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace G4.IntegrationTests.Framework
{
    /// <summary>
    /// Base class for test classes in the test suite.
    /// </summary>
    [TestClass]
    [DeploymentItem("Binaries/", "Binaries")]
    [DeploymentItem("Pages/", "Pages")]
    [DeploymentItem("Resources/", "Resources")]
    [DeploymentItem("appsettings.json")]
    public abstract class TestBase
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the context of the TestClass.
        /// </summary>
        public TestContext TestContext { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Initializes the test assembly before running the tests.
        /// This method is executed once before any tests in the assembly are run.
        /// </summary>
        /// <param name="context">The TestContext object that provides information about the current test run.</param>
        [AssemblyInitialize]
        public static void OneTimeSetup(TestContext context)
        {
            // Start local static files server
            WebServer.RemoveWebHost(); // Clear any existing web host
            WebServer.NewWebHost();    // Create a new web host
            WebServer.StartWebHost();  // Start the web host

            // Start BrowserStack local agent if the grid endpoint contains "browserstack"
            var remoteEndpoint = context.Properties.Contains(key: "Grid.Endpoint")
                ? $"{context.Properties["Grid.Endpoint"]}"
                : string.Empty;

            var isLocal = $"{context.Properties["Integration.Local"]}".Equals("true", StringComparison.OrdinalIgnoreCase);

            if (remoteEndpoint.Contains("browserstack", StringComparison.OrdinalIgnoreCase) && !isLocal)
            {
                // Start the BrowserStack local agent process
                var info = new ProcessStartInfo
                {
                    FileName = Path.Combine(Directory.GetCurrentDirectory(), "Binaries", "BrowserStackLocal.exe"),
                    Arguments = $"--force --key {context.Properties["BrowserStack.Password"]}"
                };
                Process.Start(info);
            }

            // Wait for the process to start (5 seconds)
            Thread.Sleep(TimeSpan.FromSeconds(5));
            context.WriteLine("New test server created successfully.");
        }

        /// <summary>
        /// Invokes a test case with the specified test options.
        /// </summary>
        /// <typeparam name="T">The type of the test case to invoke.</typeparam>
        /// <param name="testOptions">The options for configuring the test.</param>
        /// <returns>A response model containing the result of the test invocation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the type T cannot be instantiated or if there are no responses available.</exception>
        public static G4ResponseModel Invoke<T>(TestOptions testOptions) where T : TestCaseBase
        {
            // Set the test data using the provided test options
            SetData(testOptions);

            // Create an instance of the specified test case type using reflection
            var instance = (T)Activator.CreateInstance(type: typeof(T), args: testOptions.Environment.TestContext);

            // Set flags indicating if the test is negative and if it is a web test
            instance.IsNegativeTest = testOptions.Negative;
            instance.IsWebTest = testOptions.WebTest;

            // Invoke the test case and get the actual result
            var actual = instance.AddEnvironment(testOptions.Environment).Invoke();

            // Get the response from the environment
            var response = testOptions.Environment.GetResponses().First();

            // Get any exceptions from the response
            var exceptions = response.ResponseData.Exceptions;

            // Assert that the actual result is true (test passed)
            Assert.IsTrue(condition: actual, message: "Test case did not pass.");

            // Assert that no exceptions occurred during the test invocation, if FailOnException is set to true
            Assert.IsTrue(
                condition: !testOptions.FailOnException || !exceptions.Any(),
                message: "Exceptions occurred during test invocation.");

            // Return the response model
            return response;
        }

        /// <summary>
        /// Sets data in the automation environment based on the provided test options.
        /// </summary>
        /// <param name="testOptions">The test options containing the data to be set.</param>
        public static void SetData(TestOptions testOptions)
        {
            // Set browser name in test parameters
            testOptions.Environment.TestParameters["browserName"] = testOptions.Driver;

            // Set operating system in test parameters
            testOptions.Environment.TestParameters["os"] = testOptions.OperatingSystem;

            // Set operating system version in test parameters
            testOptions.Environment.TestParameters["osVersion"] = testOptions.OperatingSystemVersion;

            // Set browser version in test parameters
            testOptions.Environment.TestParameters["browserVersion"] = testOptions.BrowserVersion;

            // Set resolution in test parameters
            testOptions.Environment.TestParameters["resolution"] = testOptions.Resolution;

            // Set driver name in test parameters
            testOptions.Environment.TestParameters["driver"] = testOptions.Driver.EndsWith("driver", StringComparison.OrdinalIgnoreCase)
                ? testOptions.Driver
                : $"{testOptions.Driver}Driver";

            // Set page in test parameters, using default value if page is not provided
            testOptions.Environment.TestParameters["page"] = string.IsNullOrEmpty(testOptions.SystemUnderTest)
                ? testOptions.Environment.TestParameters.Get("page", string.Empty)
                : testOptions.SystemUnderTest;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents options for configuring a test.
        /// </summary>
        /// <param name="environment">The automation environment for the test.</param>
        /// <param name="driver">The driver to be used for the test.</param>
        /// <param name="sut">The page or application to be tested.</param>
        public abstract class TestOptions(AutomationEnvironment environment, string driver, string sut)
        {
            /// <summary>
            /// Gets or sets the browser version for the test.
            /// </summary>
            public string BrowserVersion { get; set; } = "latest";

            /// <summary>
            /// Gets or sets the driver to be used for the test.
            /// </summary>
            public string Driver { get; set; } = driver;

            /// <summary>
            /// Gets or sets a value indicating whether the test should fail on exception.
            /// </summary>
            public bool FailOnException { get; set; } = true;

            /// <summary>
            /// Gets or sets the automation environment for the test.
            /// </summary>
            public AutomationEnvironment Environment { get; } = environment;

            /// <summary>
            /// Gets or sets the operating system for the test.
            /// </summary>
            public string OperatingSystem { get; set; } = "Windows";

            /// <summary>
            /// Gets or sets the version of the operating system for the test.
            /// </summary>
            public string OperatingSystemVersion { get; set; } = "11";

            /// <summary>
            /// Gets or sets the page or application to be tested.
            /// </summary>
            public string SystemUnderTest { get; } = sut;

            /// <summary>
            /// Gets or sets the resolution for the test.
            /// </summary>
            public string Resolution { get; set; } = "1920x1080";

            /// <summary>
            /// Gets or sets a value indicating whether the test is negative.
            /// </summary>
            public bool Negative { get; set; }

            /// <summary>
            /// Gets or sets a value indicating whether the test is a web test.
            /// </summary>
            public virtual bool WebTest { get; set; } = !string.IsNullOrEmpty(sut);
        }

        /// <summary>
        /// Represents options for Android tests.
        /// </summary>
        public class AndroidTestOptions : TestOptions
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AndroidTestOptions"/> class.
            /// </summary>
            /// <param name="environment">The automation environment.</param>
            public AndroidTestOptions(AutomationEnvironment environment)
                : this(environment, sut: "bs://c700ce60cf13ae8ed97705a55b8e022f13c5827c")
            { }

            /// <summary>
            /// Initializes a new instance of the <see cref="AndroidTestOptions"/> class.
            /// </summary>
            /// <param name="environment">The automation environment.</param>
            /// <param name="sut">The system under test.</param>
            public AndroidTestOptions(AutomationEnvironment environment, string sut)
                : this(environment, appiumVersion: "2.0.1", sut)
            { }

            /// <summary>
            /// Initializes a new instance of the <see cref="AndroidTestOptions"/> class.
            /// </summary>
            /// <param name="environment">The automation environment.</param>
            /// <param name="appiumVersion">The Appium version.</param>
            /// <param name="sut">The system under test.</param>
            public AndroidTestOptions(AutomationEnvironment environment, string appiumVersion, string sut)
                : base(environment, driver: "AndroidDriver", sut)
            {
                // Set the Appium version in the test parameters
                environment.TestParameters["appiumVersion"] = appiumVersion;

                // Determine if it's a web test or an app test
                WebTest = Regex.IsMatch(input: sut.Trim(), "^http(s)?|^file:///");

                // If it's not a web test, set the app in the test parameters
                if (!WebTest)
                {
                    environment.TestParameters["app"] = sut;
                }
            }

            /// <inheritdoc />
            public override bool WebTest { get; set; }
        }

        /// <summary>
        /// Represents options for configuring a test using the Microsoft Edge browser.
        /// </summary>
        /// <param name="environment">The automation environment for the test.</param>
        /// <param name="sut">The page to be tested.</param>
        public class EdgeTestOptions(AutomationEnvironment environment, string sut)
            : TestOptions(environment, driver: "MicrosoftEdge", sut)
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="EdgeTestOptions"/> class with the
            /// specified environment, using the default Edge page.
            /// </summary>
            /// <param name="environment">The automation environment for the test.</param>
            public EdgeTestOptions(AutomationEnvironment environment)
                : this(environment, sut: "ElementInteractions.html")
            { }
        }

        /// <summary>
        /// Represents options for non-UI tests.
        /// </summary>
        /// <param name="environment">The automation environment.</param>
        public class NonUiTestOptions(AutomationEnvironment environment)
            : TestOptions(environment, driver: "SimulatorDriver", sut: string.Empty)
        {
            /// <inheritdoc />
            public override bool WebTest { get; set; }
        }
        #endregion
    }
}
