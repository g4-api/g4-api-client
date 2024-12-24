using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Threading;

namespace G4.UnitTests.Attributes
{
    /// <summary>
    /// A custom test method attribute that allows for retry logic in unit tests.
    /// </summary>
    /// <param name="numberOfAttempts">The number of times to attempt the test.</param>
    /// <param name="displayName">The display name of the test method.</param>
    [AttributeUsage(AttributeTargets.Method)]
    public class RetryableTestMethodAttribute(int numberOfAttempts, string displayName) : TestMethodAttribute(displayName)
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RetryableTestMethodAttribute"/> class with a single attempt.
        /// </summary>
        public RetryableTestMethodAttribute()
            : this(numberOfAttempts: 1, displayName: null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryableTestMethodAttribute"/> class with a specified number of attempts.
        /// </summary>
        /// <param name="numberOfAttempts">The number of times to attempt the test.</param>
        public RetryableTestMethodAttribute(int numberOfAttempts)
            : this(numberOfAttempts, displayName: null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RetryableTestMethodAttribute"/> class with a specified display name.
        /// </summary>
        /// <param name="displayName">The display name of the test method.</param>
        public RetryableTestMethodAttribute(string displayName)
            : this(numberOfAttempts: 1, displayName)
        { }

        /// <summary>
        /// Gets or sets the delay between test attempts in milliseconds.
        /// </summary>
        public int DelayBetweenAttempts { get; set; } = 1000;

        /// <summary>
        /// Gets the number of times the test will be attempted.
        /// </summary>
        public int NumberOfAttempts { get; } = numberOfAttempts;

        /// <summary>
        /// Executes the test method, retrying up to <see cref="NumberOfAttempts"/> times if it fails.
        /// </summary>
        /// <param name="testMethod">The test method to be executed.</param>
        /// <returns>An array of <see cref="TestResult"/> objects representing the outcome of each test attempt.</returns>
        public override TestResult[] Execute(ITestMethod testMethod)
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
                    results = base.Execute(testMethod);

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
