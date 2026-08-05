using System.Text.Json.Serialization;

namespace TreeDataGridDemo.Models
{
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(OnThisDay))]
    internal partial class OnThisDayJsonSerializerContext : JsonSerializerContext
    {
    }
}
