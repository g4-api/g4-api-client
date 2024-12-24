using G4.Cache;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Linq;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("AssembliesLoader")]
    [TestCategory("UnitTest")]
    public class AssembliesLoaderTests
    {
        [TestMethod(displayName: "Verify that the number of imported assemblies and types " +
            "meets certain criteria.")]
        public void AssembliesNumberTest()
        {
            // Instantiate an AssembliesLoader
            var loader = new AssembliesLoader();

            // Import assemblies and their types
            var actual = loader.ImportAssemblies();

            // Assert that the count of imported assemblies is not zero
            Assert.IsTrue(actual.Count != 0);

            // Assert that the count of unique assemblies is greater than 5
            Assert.IsTrue(actual.Select(i => i.Value.Assembly).Count() > 5);

            // Assert that the total count of types across all assemblies is greater than 50
            Assert.IsTrue(actual.Sum(i => i.Value.Types.Count()) > 50);
        }

        [TestMethod(displayName: "Verify that the SetupCompleted event is raised with the " +
            "correct event arguments after importing assemblies.")]
        public void SetupCompletedEventTest()
        {
            // Instantiate an AssembliesLoader
            var loader = new AssembliesLoader();

            // Initialize the event arguments variable
            var actual = new AssembliesLoaderSetupEventArgs();

            // Subscribe to the SetupCompleted event and capture the event arguments
            loader.SetupCompleted += (s, e) => actual = e;

            // Import assemblies
            loader.ImportAssemblies();

            // Assert that the event arguments contain information about loaded files, directories, and the base directory
            Assert.IsTrue(actual.Files.Any());
            Assert.IsTrue(!actual.Directories.Any());
            Assert.IsTrue(!string.IsNullOrEmpty(actual.Directory));
        }
    }
}
