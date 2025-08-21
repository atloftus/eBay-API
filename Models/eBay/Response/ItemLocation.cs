using System.Text.Json.Serialization;


namespace eBay_API.Models.eBay.Response;
public class ItemLocation
{
    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; }
}