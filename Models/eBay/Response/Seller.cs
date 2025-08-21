using System.Text.Json.Serialization;

namespace eBay_API.Models.eBay.Response;
public class Seller
{
    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("feedbackPercentage")]
    public string FeedbackPercentage { get; set; }

    [JsonPropertyName("feedbackScore")]
    public int FeedbackScore { get; set; }
}