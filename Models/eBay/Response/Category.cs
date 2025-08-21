using System.Text.Json.Serialization;


namespace eBay_API.Models.eBay.Response;

public class Category
{
    [JsonPropertyName("categoryId")]
    public string CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string CategoryName { get; set; }
}