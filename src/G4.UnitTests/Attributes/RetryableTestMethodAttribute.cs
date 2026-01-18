using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace G4.UnitTests.Attributes
{
    /// <summary>
    /// Marks a test method as retryable, allowing it to be executed multiple times
    /// before being considered failed.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RetryableTestMethodAttribute(
        int numberOfAttempts,
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = -1) : TestMethodAttribute(callerFilePath, callerLineNumber)
    {
        /// <summary>
        /// Creates a retryable test attribute with a default of 1 attempt.
        /// </summary>
        /// <param name="callerFilePath">Automatically populated with the source file path of the calling method.</param>
        /// <param name="callerLineNumber">Automatically populated with the source line number of the calling method.</param>
        public RetryableTestMethodAttribute(
            [CallerFilePath] string callerFilePath = "",
            [CallerLineNumber] int callerLineNumber = -1)
            : this(numberOfAttempts: 1, callerFilePath, callerLineNumber)
        { }

        /// <summary>
        /// Gets or sets the delay (in milliseconds) between retry attempts.
        /// </summary>
        /// <remarks>This delay is applied after a failed attempt and before retrying. Default is 1000 ms (1 second).</remarks>
        public int DelayBetweenAttempts { get; set; } = 1000;

        /// <summary>
        /// Gets the total number of attempts allowed for this test method.
        /// </summary>
        /// <remarks>This includes the initial attempt. For example, a value of 3 means: 1 initial run + 2 retries.</remarks>
        public int NumberOfAttempts { get; } = numberOfAttempts;

        /// <inheritdoc />
        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            // Array to hold the results of each test attempt
            TestResult[] results = [];

            // Loop through the number of attempts specified by the attribute and retry
            // the test if it fails each time (except the last attempt)
            for (int i = 0; i < NumberOfAttempts; i++)
            {
                try
                {
                    // Execute the test method
                    results = await base.ExecuteAsync(testMethod);

                    // If all test results are successful, return the results
                    if (Array.TrueForAll(results, r => r.Outcome == UnitTestOutcome.Passed))
                    {
                        return results;
                    }

                    // Wait for the specified delay before the next attempt
                    Thread.Sleep(DelayBetweenAttempts);
                }
                catch (Exception) when (i != NumberOfAttempts - 1)
                {
                    // Only rethrow the exception if it's the last attempt
                    throw;
                }
            }

            // Return the results of the last attempt if all attempts fail
            return results;
        }
    }
}
