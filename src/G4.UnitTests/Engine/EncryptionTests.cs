using G4.Api;
using G4.Extensions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;
using System.Security.Cryptography;

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
            Assert.AreNotEqual(notExpected: StringUnderTest, actual: encrypted);

            // Decrypt the encrypted string
            var decrypted = encrypted.Decrypt(Key);

            // Assert that the decrypted string is equal to the original string
            Assert.AreEqual(expected: StringUnderTest, actual: decrypted);
        }

        [TestMethod(displayName: "Verify that parameter value is correctly encrypted, " +
            "encoded, updated, and retrieved when environment is set and parameters are accessed")]
        public void ApplicationEnvironmentTest()
        {
            // Create an instance of the client to interact with environments.
            var client = new G4Client().Environments;

            // Define a dictionary containing the test parameter.
            var parameters = new Dictionary<string, string>
            {
                ["TestParameter"] = "Foo Bar"
            };

            // First Creation:
            // Set the environment "TestEnvironment" with the test parameters,
            // enabling Base64 encoding and encrypting values with key "g4".
            client.SetEnvironment(
                name: "TestEnvironment",
                parameters,
                encode: true,
                encryptionKey: "g4");

            // Retrieve the parameter value from the environment without decoding.
            var parameter = client.GetParameter(
                environment: "TestEnvironment",
                parameter: "TestParameter",
                decode: false,
                encryptionKey: default);

            // Assert that the retrieved raw parameter value is not equal to "Foo Bar"
            // because it should be encrypted and encoded.
            Assert.AreNotEqual(notExpected: "Foo Bar", actual: parameter);

            // Update the same parameter by setting the environment again with the same values.
            client.SetEnvironment(
                name: "TestEnvironment",
                parameters,
                encode: true,
                encryptionKey: "g4");

            // Retrieve the parameter value again without decoding.
            parameter = client.GetParameter(
                environment: "TestEnvironment",
                parameter: "TestParameter",
                decode: false,
                encryptionKey: default);

            // Assert that even after update, the raw parameter value remains different from "Foo Bar".
            Assert.AreNotEqual(notExpected: "Foo Bar", actual: parameter);

            // Convert the parameter value from Base64 and then decrypt it using key "g4"
            // to obtain the original value.
            var actual = parameter.ConvertFromBase64().Decrypt("g4");

            // Assert that the decrypted and decoded value matches the original value "Foo Bar".
            Assert.AreEqual(expected: "Foo Bar", actual);

            // Retrieve the parameter value with decoding enabled and using the correct encryption key.
            parameter = client.GetParameter(
                environment: "TestEnvironment",
                parameter: "TestParameter",
                decode: true,
                encryptionKey: "g4");

            // Assert that the final retrieved value is equal to "Foo Bar".
            Assert.AreEqual(expected: "Foo Bar", actual: parameter);
        }

        [TestMethod(displayName: "Verify that CryptographicException is thrown when decryption " +
            "fails due to an incorrect encryption key or improper decoding")]
        public void ApplicationEnvironmentExceptionTest()
        {
            // Create an instance of the client to interact with environments.
            var client = new G4Client().Environments;

            // Define a dictionary containing the test parameter.
            var parameters = new Dictionary<string, string>
            {
                ["TestParameter"] = "Foo Bar"
            };

            // First Creation:
            // Set the environment "TestEnvironment" with the test parameters,
            // enabling Base64 encoding and encrypting values with key "g4".
            client.SetEnvironment(
                name: "TestEnvironment",
                parameters,
                encode: true,
                encryptionKey: "g4");

            // Retrieve the parameter value from the environment without decoding.
            var parameter = client.GetParameter(
                environment: "TestEnvironment",
                parameter: "TestParameter",
                decode: false,
                encryptionKey: default);

            // Assert that the retrieved raw parameter value is not equal to "Foo Bar"
            // because it should be encrypted and encoded.
            Assert.AreNotEqual(notExpected: "Foo Bar", actual: parameter);

            // Verify that providing an incorrect encryption key ("g5") with decoding enabled throws a CryptographicException.
            Assert.ThrowsExactly<CryptographicException>(() =>
            {
                client.GetParameter(
                    environment: "TestEnvironment",
                    parameter: "TestParameter",
                    decode: true,
                    encryptionKey: "g5");
            });

            // Verify that attempting to decrypt a non-decoded parameter with the correct encryption key ("g4")
            // throws a CryptographicException, as the value is not properly decoded before decryption.
            Assert.ThrowsExactly<CryptographicException>(() =>
            {
                client.GetParameter(
                    environment: "TestEnvironment",
                    parameter: "TestParameter",
                    decode: false,
                    encryptionKey: "g4");
            });
        }
    }
}
