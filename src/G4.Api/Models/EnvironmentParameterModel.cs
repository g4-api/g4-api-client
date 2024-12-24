using System.ComponentModel.DataAnnotations;

namespace G4.Models
{
    /// <summary>
    /// Represents a model for environment parameters.
    /// </summary>
    public class EnvironmentParameterModel
    {
        /// <summary>
        /// Indicates whether the parameter value should be encoded to base64.
        /// </summary>
        /// <remarks>Optional field; defaults to true.</remarks>
        public bool Encode { get; set; } = true;

        /// <summary>
        /// The name of the environment.
        /// </summary>
        /// <remarks>Required field; must match the regular expression \w+</remarks>
        [RegularExpression(@"\w+")]
        public string EnvironmentName { get; set; } = "SystemParameters";

        /// <summary>
        /// The name of the parameter.
        /// </summary>
        /// <remarks>Required field; must match the regular expression \w+</remarks>
        [Required]
        [RegularExpression(@"\w+")]
        public string Name { get; set; }

        /// <summary>
        /// The value of the parameter.
        /// </summary>
        /// <remarks>Can be null or empty; represents the parameter's value.</remarks>
        public string Value { get; set; }
    }
}
