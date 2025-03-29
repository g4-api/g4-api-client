using G4.Models;

using System.Collections.Generic;

namespace G4.Api.Abstractions
{
    /// <summary>
    /// Repository interface for managing environment parameters.
    /// </summary>
    public interface IEnvironmentsClient
    {
        /// <summary>
        /// Clears all environments by dropping their corresponding collections from the database.
        /// </summary>
        void ClearEnvironments();

        /// <summary>
        /// Retrieves all parameters for a specified environment.
        /// </summary>
        /// <param name="name">The name of the environment from which to retrieve parameters.</param>
        /// <returns>
        /// A dictionary containing parameter names and their corresponding values for the specified environment.
        /// Returns <c>null</c> if the environment does not exist.
        /// </returns>
        IDictionary<string, object> GetEnvironment(string name);

        /// <summary>
        /// Retrieves all parameters for a specified environment.
        /// </summary>
        /// <param name="name">The name of the environment from which to retrieve parameters.</param>
        /// <param name="decode">A boolean value indicating whether to decode the parameter value from base64.</param>
        /// <returns>
        /// A dictionary containing parameter names and their corresponding values for the specified environment.
        /// Returns <c>null</c> if the environment does not exist.
        /// </returns>
        IDictionary<string, object> GetEnvironment(string name, bool decode);

        /// <summary>
        /// Retrieves all environment parameters across all environments.
        /// </summary>
        /// <returns>A dictionary where each key is the environment name and the value is another dictionary containing parameter names and their corresponding values.</returns>
        Dictionary<string, IDictionary<string, object>> GetEnvironments();

        /// <summary>
        /// Retrieves the value of a specified environment parameter without any additional processing.
        /// </summary>
        /// <param name="environment">The name of the environment from which to retrieve the parameter.</param>
        /// <param name="parameter">The name of the parameter to retrieve.</param>
        /// <returns>The raw value of the specified parameter if it exists; otherwise, the default value.</returns>
        string GetParameter(string environment, string parameter);

        /// <summary>
        /// Retrieves the value of a specified environment parameter with an option to decode it from Base64.
        /// </summary>
        /// <param name="environment">The name of the environment from which to retrieve the parameter.</param>
        /// <param name="parameter">The name of the parameter to retrieve.</param>
        /// <param name="decode">A boolean value indicating whether to decode the parameter value from Base64.</param>
        /// <returns>The value of the specified parameter if it exists; otherwise, the default value.</returns>
        string GetParameter(string environment, string parameter, bool decode);

        /// <summary>
        /// Retrieves the value of a specified environment parameter with options to decode it from Base64
        /// and decrypt it using the provided encryption key.
        /// </summary>
        /// <param name="environment">The name of the environment from which to retrieve the parameter.</param>
        /// <param name="parameter">The name of the parameter to retrieve.</param>
        /// <param name="decode">A boolean value indicating whether to decode the parameter value from Base64.</param>
        /// <param name="encryptionKey">An optional encryption key used to decrypt the parameter value. If <c>null</c> or empty, no decryption is performed.</param>
        /// <returns>The processed parameter value as a string if it exists; otherwise, the default value.</returns>
        string GetParameter(string environment, string parameter, bool decode, string encryptionKey);

        /// <summary>
        /// Removes the specified environment from the database.
        /// </summary>
        /// <param name="name">The name of the environment to remove.</param>
        /// <returns>An integer representing the HTTP status code:
        /// <c>404</c> if the environment is not found,
        /// <c>204</c> if the environment was successfully removed.
        /// </returns>
        int RemoveEnvironment(string name);

        /// <summary>
        /// Deletes a specified environment parameter from the given environment.
        /// </summary>
        /// <param name="environment">The name of the environment from which to delete the parameter.</param>
        /// <param name="parameter">The name of the parameter to delete.</param>
        /// <returns>An integer representing the HTTP status code:
        /// - <c>404</c> if the parameter is not found,
        /// - <c>204</c> if the parameter was successfully deleted.
        /// </returns>
        int RemoveParameter(string environment, string parameter);

        /// <summary>
        /// Updates an existing environment with the specified name and parameters.
        /// </summary>
        /// <param name="name">The name of the environment to update.</param>
        /// </param>
        /// <returns>An integer representing the HTTP status code:
        /// <c>201</c> if the environment was created successfully,
        /// <c>204</c> if the environment was updated successfully.
        /// </returns>
        int SetEnvironment(string name);

        /// <summary>
        /// Updates an existing environment with the specified name and parameters.
        /// </summary>
        /// <param name="name">The name of the environment to update.</param>
        /// <param name="parameters">A dictionary of parameters to update in the environment. If <c>null</c>, an empty dictionary is used.
        /// </param>
        /// <returns>An integer representing the HTTP status code:
        /// <c>201</c> if the environment was created successfully,
        /// <c>204</c> if the environment was updated successfully.
        /// </returns>
        int SetEnvironment(string name, IDictionary<string, string> parameters);

        /// <summary>
        /// Updates an existing environment with the specified name and parameters.
        /// </summary>
        /// <param name="name">The name of the environment to update.</param>
        /// <param name="parameters">A dictionary of parameters to update in the environment. If <c>null</c>, an empty dictionary is used.
        /// <param name="encode">Indicates whether the parameters should be converted to Base64.</param>
        /// </param>
        /// <returns>An integer representing the HTTP status code:
        /// <c>201</c> if the environment was created successfully,
        /// <c>204</c> if the environment was updated successfully.
        /// </returns>
        int SetEnvironment(string name, IDictionary<string, string> parameters, bool encode);

        /// <summary>
        /// Updates an existing environment with the specified name and parameters,
        /// optionally encoding and encrypting the parameter values.
        /// </summary>
        /// <param name="name">The name of the environment to update.</param>
        /// <param name="parameters">A dictionary of parameters to update in the environment. If <c>null</c>, an empty dictionary is used.</param>
        /// <param name="encode">Indicates whether the parameter values should be converted to Base64.</param>
        /// <param name="encryptionKey">An optional encryption key used to encrypt the parameter values. If <c>null</c> or empty, no encryption is applied.</param>
        /// <returns>An integer representing the HTTP status code:
        /// <c>201</c> if the environment was created successfully,
        /// <c>204</c> if the environment was updated successfully.
        /// </returns>
        int SetEnvironment(string name, IDictionary<string, string> parameters, bool encode, string encryptionKey);


        /// <summary>
        /// Sets an environment parameter in the specified environment collection.
        /// </summary>
        /// <param name="parameter">The environment parameter model containing the parameter details.</param>
        /// <returns>
        /// A tuple containing:
        /// - StatusCode: An integer representing the HTTP status code (201 if created, 204 if updated).
        /// - Value: A string representing the parameter value.
        /// </returns>
        (int StatusCode, string Value) SetParameter(EnvironmentParameterModel parameter);
    }
}
