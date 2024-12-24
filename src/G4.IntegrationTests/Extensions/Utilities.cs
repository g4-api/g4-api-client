/*
 * CHANGE LOG - keep only last 5 threads
 * 
 * RESOURCES
 */
using System;
using System.Text;

namespace G4.IntegrationTests.Extensions
{
    /// <summary>
    /// Contains utility methods.
    /// </summary>
    internal static class Utilities
    {
        // Random number generator used for generating random strings
        private static readonly Random s_random = new();

        /// <summary>
        /// Generates a random string of the specified length.
        /// </summary>
        /// <param name="length">The length of the generated string.</param>
        /// <returns>A random string.</returns>
        public static string NewRandomString(int length)
        {
            // The character pool from which to pick random characters
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            StringBuilder stringBuilder = new(length);

            // Append random characters to the string
            for (int i = 0; i < length; i++)
            {
                stringBuilder.Append(chars[s_random.Next(chars.Length)]);
            }

            // Convert StringBuilder to string and return
            return stringBuilder.ToString();
        }
    }
}
