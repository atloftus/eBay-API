using System.Text.Json.Serialization;



namespace eBay_API.Models.eBay.Response 
{
    public class EbayBrowseResponse
    {
        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("itemSummaries")]
        public List<ItemSummary> ItemSummaries { get; set; }
    }
}