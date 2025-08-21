using System.Text.Json.Serialization;

namespace eBay_API.Models.eBay.Response;
public class Image
{
    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; }
}