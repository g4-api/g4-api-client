using G4.Api.Abstractions;
using G4.Extensions;
using G4.Models;

using LiteDB;

using Microsoft.Extensions.Primitives;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;

namespace G4.Api.Clients
{
    /// <summary>
    /// Client class for managing environments and their parameters.
    /// </summary>
    internal class EnvironmentsClient : ClientBase, IEnvironmentsClient
    {
        /// <inheritdoc />
        public void ClearEnvironments()
        {
            // Retrieve the collection for ApplicationParametersModel from the LiteDatabase.
            var collection = LiteDatabase.GetCollection<ApplicationParametersModel>(name: G4Environment.CollectionName);

            // Return immediately if the collection does not exist.
            if (collection == default)
            {
                return;
            }

            // Retrieve the collection and associated documents for the specified environment.
            var documents = collection
                .Find(i => i.Type.Equals(nameof(ApplicationParametersModel), StringComparison.OrdinalIgnoreCase));

            // Select the Ids of the documents to be removed and convert them to BsonValue.
            var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

            // Delete all documents with the selected Ids from the collection.
            collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));
        }

        /// <inheritdoc />
        public string GetParameter(string environment, string parameter)
        {
            return GetParameter(environment, parameter, decode: true, encryptionKey: default);
        }

        /// <inheritdoc />
        public string GetParameter(string environment, string parameter, bool decode)
        {
            return GetParameter(environment, parameter, decode, encryptionKey: default);
        }

        /// <inheritdoc />
        public string GetParameter(string environment, string parameter, bool decode, string encryptionKey)
        {
            // Retrieve the environment parameters for the specified environment name
            var applicationEnviornment = G4Environment
                .ApplicationParameters
                .Get(key: environment, defaultValue: new ApplicationParametersModel());

            // Attempt to get the parameter value from the environment's Parameters dictionary
            var isParameter = applicationEnviornment.Parameters.TryGetValue(key: parameter, out object value);

            // Return null if the parameter does not exist
            if (!isParameter)
            {
                return null;
            }

            // Return the decoded value if requested, otherwise return the raw value as a string
            var decodedValue = decode
                ? $"{value}".ConvertFromBase64()
                : $"{value}";

            // Return the decrypted value if an encryption key is provided, otherwise return the decoded value
            return !string.IsNullOrEmpty(encryptionKey)
                ? decodedValue.Decrypt(encryptionKey)
                : decodedValue;
        }

        /// <inheritdoc />
        public IDictionary<string, object> GetEnvironment(string name)
        {
            // Retrieve the environment parameters for the specified environment name.
            // If the environment does not exist, use a new ApplicationParametersModel as the default.
            return G4Environment
                .ApplicationParameters
                .Get(key: name, defaultValue: new ApplicationParametersModel())?
                .Parameters;
        }

        /// <inheritdoc />
        public IDictionary<string, object> GetEnvironment(string name, bool decode)
        {
            // Retrieve the environment parameters for the specified environment name.
            // If the environment does not exist, use a new ApplicationParametersModel as the default.
            if (!decode)
            {
                return G4Environment
                    .ApplicationParameters
                    .Get(key: name, defaultValue: new ApplicationParametersModel())?
                    .Parameters;
            }

            // Retrieve and decode the environment parameters from Base64.
            // If the environment does not exist, use a new ApplicationParametersModel as the default.
            return G4Environment
                .ApplicationParameters
                .Get(key: name, defaultValue: new ApplicationParametersModel())?
                .Parameters?
                .ToDictionary(k => k.Key, v => (object)$"{v.Value}".ConvertFromBase64());
        }

        /// <inheritdoc />
        public Dictionary<string, IDictionary<string, object>> GetEnvironments()
        {
            // Convert the ApplicationParameters collection to a dictionary
            // with the environment name as the key and its parameters as the value
            return G4Environment.ApplicationParameters.ToDictionary(
                k => k.Key,
                v => v.Value.Parameters);
        }

        /// <inheritdoc />
        public int RemoveEnvironment(string name)
        {
            // Retrieve the collection for the specified environment name from LiteDatabase
            // Attempt to find the existing parameters model in the collection
            var (collection, documents) = GetDocuments(LiteDatabase, name);

            // Return 404 Not Found if the environment does not exist
            if (documents == default || documents.Length == 0)
            {
                return 404;
            }

            // Select the IDs of the documents to be removed from the collection
            var documentsToRemove = documents.Select(i => new BsonValue(i.Id));

            // Delete the parameters model from the collection using its ID
            collection.DeleteMany(predicate: "_id IN @0", args: new BsonArray(documentsToRemove));

            // Return 204 No Content to indicate successful deletion
            return 204;
        }

        /// <inheritdoc />
        public int RemoveParameter(string environment, string parameter)
        {
            // Determine the collection name based on the provided environment name
            var name = string.IsNullOrEmpty(environment)
                ? "SystemParameters"
                : environment;

            // Retrieve the collection from the LiteDatabase using the determined name
            var (collection, documents) = GetDocuments(LiteDatabase, name);

            // Return 404 Not Found if the environment does not exist
            if (documents.Length == 0)
            {
                return 404;
            }

            var counter404 = 0;

            // Iterate through each document in the collection
            foreach (var item in documents)
            {
                // Assign the current document to a variable for easier access
                var document = item;

                // Check if the parameters model exists and contains the specified parameter
                if (document?.Parameters.ContainsKey(key: parameter) != true)
                {
                    // Increment the counter if the parameter does not exist
                    counter404++;

                    // Skip to the next document if the parameter does not exist
                    continue;
                }

                // Remove the specified parameter from the Parameters dictionary
                document.Parameters.Remove(parameter);

                // Update the parameters model in the collection to persist the changes
                collection.Update(document);
            }

            // Return 404 Not Found if the parameter does not exist in any document in
            // the collection or 204 No Content if it was successfully removed
            return documents.Length == counter404 ? 404 : 204;
        }

        /// <inheritdoc />
        public (int StatusCode, string Value) SetParameter(EnvironmentParameterModel parameter)
        {
            // Determine the collection name based on the provided environment name
            var name = string.IsNullOrEmpty(parameter.EnvironmentName)
                ? "SystemParameters"
                : parameter.EnvironmentName;

            // Attempt to find the existing parameters model in the collection
            var (collection, documents) = GetDocuments(LiteDatabase, name);

            // Initialize the status code to 200 OK by default
            var statusCode = 200;

            // Initialize the parameter value to an empty string
            var parameterValue = string.Empty;

            // Iterate through each document in the collection
            foreach (var item in documents)
            {
                // Assign the current document to a variable for easier access
                var document = item;

                // Initialize the parameters model if it does not exist
                document ??= new ApplicationParametersModel
                {
                    Id = Guid.NewGuid(),
                    Name = name
                };

                // Encrypt the parameter value if an encryption key is provided
                parameterValue = !string.IsNullOrEmpty(parameter.EncryptionKey)
                    ? parameter.Value.Encrypt(parameter.EncryptionKey)
                    : parameter.Value;

                // Encode the parameter value if required
                parameterValue = parameter.Encode
                    ? parameter.Value.ConvertToBase64()
                    : parameter.Value;

                // Check if the Parameters dictionary is null (i.e., no parameters exist)
                if (document.Parameters == null)
                {
                    // Initialize the Parameters dictionary and add the new parameter
                    document.Parameters = new Dictionary<string, object>
                    {
                        [parameter.Name] = parameterValue
                    };

                    // Insert the new parameters model into the collection
                    collection.Insert(document);

                    // Return status code 201 (Created)
                    return (201, parameterValue);
                }

                // Determine the status code based on whether the parameter already exists
                statusCode = document.Parameters.ContainsKey(parameter.Name) ? 200 : 201;

                // Update the parameter value in the dictionary
                document.Parameters[parameter.Name] = parameterValue;

                // Update the existing parameters model in the collection
                collection.Update(document);
            }

            // Return the appropriate status code
            return (statusCode, parameterValue);
        }

        /// <inheritdoc />
        public int SetEnvironment(string name)
        {
            // Call the overloaded InitializeEnvironment method with default parameters
            return SetEnvironment(name, parameters: default, encode: true);
        }

        /// <inheritdoc />
        public int SetEnvironment(string name, IDictionary<string, string> parameters)
        {
            // Call the overloaded InitializeEnvironment method with default parameters
            return SetEnvironment(name, parameters, encode: true);
        }

        /// <inheritdoc />
        public int SetEnvironment(string name, IDictionary<string, string> parameters, bool encode)
        {
            return SetEnvironment(name, parameters, encode, encryptionKey: default);
        }

        /// <inheritdoc />
        public int SetEnvironment(string name, IDictionary<string, string> parameters, bool encode, string encryptionKey)
        {
            // Initializes a new environment with the specified name and parameters.
            static void InitializeEnvironment(
                ILiteCollection<ApplicationParametersModel> collection,
                string name, IDictionary<string, string> parameters,
                bool encode,
                string encryptionKey)
            {
                // Create a new ApplicationParametersModel with a unique ID, the specified name, and the provided parameters.
                var document = new ApplicationParametersModel
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Parameters = InitializeParameters(parameters, encode, encryptionKey)
                };

                // Insert the newly created parameters model into the collection.
                collection.Insert(document);
            }

            // Initializes a collection of parameters by optionally encrypting and encoding their values.
            static Dictionary<string, object> InitializeParameters(
                    IDictionary<string, string> parameters, bool encode, string encryptionKey)
            {
                // Create a dictionary to hold the processed parameters.
                var parametersCollection = new Dictionary<string, object>();

                // Iterate through each key-value pair in the provided parameters dictionary.
                foreach (var parameter in parameters)
                {
                    // Store the processed value in the collection using the original parameter key.
                    parametersCollection[parameter.Key] = InitializeParameterValue(value: parameter.Value, encode, encryptionKey);
                }

                // Return the collection of processed parameters.
                return parametersCollection;
            }

            // Initialize parameters to an empty dictionary if it is null
            parameters ??= new Dictionary<string, string>();

            // Attempt to find the existing parameters model in the collection
            var (collection, documents) = GetDocuments(LiteDatabase, name);

            // Check if the parameters model does not exist
            if (documents.Length == 0)
            {
                // Initialize the environment if the parameters model does not exist
                InitializeEnvironment(collection, name, parameters, encode, encryptionKey);

                // Return 201 Created to indicate successful initialization
                return 201;
            }

            // Iterate through each existing document in the collection
            foreach (var item in documents)
            {
                // Assign the current document to a variable for easier access
                var document = item;

                // Iterate through each parameter and update or add it to the Parameters dictionary
                foreach (var parameter in parameters)
                {
                    document.Parameters[parameter.Key] = InitializeParameterValue(value: parameter.Value, encode, encryptionKey);
                }

                // Update the parameters model in the collection to persist the changes
                collection.Update(document);
            }

            // Return 204 No Content to indicate successful update
            return 204;
        }

        // Initializes the parameter value by optionally encrypting and encoding it.
        private static string InitializeParameterValue(string value, bool encode, string encryptionKey)
        {
            // Encrypt the value if an encryption key is provided.
            value = !string.IsNullOrEmpty(encryptionKey)
                ? value.Encrypt(encryptionKey)
                : value;

            // Encode the value in Base64 if encoding is enabled, otherwise return the original value.
            return encode ? value.ConvertToBase64() : value;
        }

        // Retrieves a single ApplicationParametersModel document from the specified LiteDatabase collection based on the provided environment name.
        private static (ILiteCollection<ApplicationParametersModel> Collection, ApplicationParametersModel[] Documents) GetDocuments(ILiteDatabase liteDatabase, string name)
        {
            // Retrieve the collection for ApplicationParametersModel using the predefined collection name and GUID auto ID.
            var collection = liteDatabase.GetCollection<ApplicationParametersModel>(
                name: G4Environment.CollectionName,
                autoId: BsonAutoId.Guid);

            // Search for a document where the Name property matches the provided name, ignoring case.
            // Return the first matching document or null if no match is found.
            var documents = collection
                .Find(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Return the collection and document as a tuple
            return (collection, documents);
        }
    }
}
