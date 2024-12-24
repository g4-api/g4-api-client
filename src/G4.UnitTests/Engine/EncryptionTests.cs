using G4.Extensions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace G4.UnitTests.Engine
{
    [TestClass]
    [TestCategory("Encryption")]
    [TestCategory("UnitTest")]
    public class EncryptionTests
    {
        [TestMethod(displayName: "Verify basic encryption and decryption.")]
        public void EncryptionBasicTest()
        {
            // Constants for encryption
            const string Key = "TestKey";
            const string StringUnderTest = "{" +
                "\"created\":\"2024-03-17T11:24:37.9062748Z\"," +
                "\"expiration\":\"2024-06-15T11:24:36.1397882Z\"," +
                "\"lastUpdate\":\"2024-03-17T11:24:37.9063393Z\"," +
                "\"minutes\":-1," +
                "\"packages\":[\"all\"]," +
                "\"usage\":0" +
            "}";

            // Encrypt the string
            var encrypted = StringUnderTest.Encrypt(Key);

            // Assert that the encrypted string is not equal to the original string
            Assert.IsTrue(encrypted != StringUnderTest);

            // Decrypt the encrypted string
            var decrypted = encrypted.Decrypt(Key);

            // Assert that the decrypted string is equal to the original string
            Assert.IsTrue(decrypted == StringUnderTest);
        }
    }
}
