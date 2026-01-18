using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Discovery")]
    [TestCategory("UnitTest")]
    public class DiscoveryTests
    {
        /// <summary>
        /// Tests retrieval of test methods and classes based on the specified SQL query.
        /// </summary>
        /// <param name="sqlQuery">The SQL query used to filter the test methods and classes.</param>
        [TestMethod(DisplayName = "Verify that test methods and classes are retrieved based on the specified SQL query.")]
        [DataRow("Category <> ''")]
        public void GetTestsBySqlQueryTest(string sqlQuery)
        {
            // Get all test classes in the current assembly
            var testClasses = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(i => i.GetCustomAttribute<TestClassAttribute>() != null);

            // Get all test methods from the test classes
            var testMethods = testClasses
                .SelectMany(i => i.GetMethods())
                .Where(i => i.GetCustomAttribute<TestMethodAttribute>() != null)
                .ToList();

            // Create DataTable records for the test methods
            var methodRecords = NewTestRecords(testMethods);

            // Create DataTable records for the test classes
            var classRecords = NewTestRecords(testClasses);

            // Assert that there are more than 3 records in either method or class records
            Assert.IsTrue(methodRecords.Rows.Count > 3 || classRecords.Rows.Count > 3);

            // Select rows based on the SQL query from both method and class records
            var rows = methodRecords.Select(sqlQuery).Concat(classRecords.Select(sqlQuery)).ToArray();

            // Assert that there are at least 2 rows matching the SQL query
            Assert.IsGreaterThanOrEqualTo(2, rows.Length);
        }

        // Creates a new DataTable containing test records for the specified test methods.
        private static DataTable NewTestRecords(IEnumerable<MethodInfo> testMethods)
        {
            // Adds a test record to the specified DataTable.
            static void AddRecord(DataTable table, string @class, string name, string category)
            {
                var row = table.NewRow();
                row["Class"] = @class;
                row["Name"] = name;
                row["Category"] = category;
                table.Rows.Add(row);
            }

            // Create a new DataTable and define its columns
            var testRecords = new DataTable();
            testRecords.Columns.Add("Class", typeof(string));
            testRecords.Columns.Add("Name", typeof(string));
            testRecords.Columns.Add("Category", typeof(string));

            // Iterate over each test method
            foreach (var testMethod in testMethods)
            {
                // Get the TestCategory attributes of the test method
                var categories = testMethod.GetCustomAttributes<TestCategoryAttribute>();

                // If no categories are defined, add a record with an empty category
                if (!categories.Any())
                {
                    AddRecord(testRecords, testMethod.DeclaringType.FullName, testMethod.Name, string.Empty);
                    continue;
                }

                // Iterate over each category and add a record for each test category
                foreach (var category in categories)
                {
                    foreach (var testCategory in category.TestCategories)
                    {
                        AddRecord(testRecords, testMethod.DeclaringType.FullName, testMethod.Name, testCategory);
                    }
                }
            }

            // Return the populated DataTable
            return testRecords;
        }

        // Creates a new DataTable containing test records for the specified test classes.
        private static DataTable NewTestRecords(IEnumerable<Type> testClasses)
        {
            // Adds a test record to the specified DataTable.
            static void AddRecord(DataTable table, string @class, string name, string category)
            {
                var row = table.NewRow();
                row["Class"] = @class;
                row["Name"] = name;
                row["Category"] = category;
                table.Rows.Add(row);
            }

            // Create a new DataTable and define its columns
            var testRecords = new DataTable();
            testRecords.Columns.Add("Class", typeof(string));
            testRecords.Columns.Add("Name", typeof(string));
            testRecords.Columns.Add("Category", typeof(string));

            // Iterate over each test class type
            foreach (Type testClass in testClasses)
            {
                // Get the TestCategory attributes of the test class
                var categories = testClass.GetCustomAttributes<TestCategoryAttribute>();

                // If no categories are defined, add a record with an empty category
                if (!categories.Any())
                {
                    AddRecord(testRecords, testClass.FullName, testClass.Name, string.Empty);
                    continue;
                }

                // Iterate over each category and add a record for each test category
                foreach (var category in categories)
                {
                    foreach (var testCategory in category.TestCategories)
                    {
                        AddRecord(testRecords, testClass.FullName, testClass.Name, testCategory);
                    }
                }
            }

            // Return the populated DataTable
            return testRecords;
        }
    }
}
