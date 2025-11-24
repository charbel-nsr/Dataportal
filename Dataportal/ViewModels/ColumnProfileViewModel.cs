using System.Text.Json.Serialization;

namespace Dataportal.ViewModels
{
    public class ColumnProfileViewModel
    {
        [JsonPropertyName("columnName")]
        public string ColumnName { get; set; }

        [JsonPropertyName("uniqueValues")]
        public long UniqueValues { get; set; }

        [JsonPropertyName("emptyValues")]
        public long EmptyValues { get; set; }

        [JsonPropertyName("minimum")]
        public string? Minimum { get; set; }

        [JsonPropertyName("maximum")]
        public string? Maximum { get; set; }
    }
}