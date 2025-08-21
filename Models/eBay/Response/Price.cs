using System.Text.Json.Serialization;

namespace eBay_API.Models.eBay.Response;
public class Price
{
    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }
}