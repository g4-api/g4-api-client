using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace G4.Api.Models
{
    /// <summary>
    /// Represents a cache model for storing templates in the G4 API.
    /// </summary>
    public class TemplateCacheModel
    {
        /// <summary>
        /// Gets or sets the unique identifier of the template cache model.
        /// This property is ignored during JSON serialization.
        /// </summary>
        [JsonIgnore]
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the collection of templates.
        /// The dictionary uses template names as keys and their content as values.
        /// </summary>
        public IDictionary<string, string> Templates { get; set; }
    }
}
