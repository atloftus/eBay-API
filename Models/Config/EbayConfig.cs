namespace eBay_API.Models.Config;



public class EbayConfig
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public string Scope { get; init; } = "";
    public string Environment { get; init; } = "";
    public string MarketplaceId { get; init; } = "";
    public string Locale { get; init; } = "";
    public string PaymentPolicyId { get; init; } = "";
    public string FulfillmentPolicyId { get; init; } = "";
    public string ReturnPolicyId { get; init; } = "";
    public decimal? FallbackPrice { get; init; }
    public string CategoryFallback { get; init; } = "";
    public string DefaultCondition { get; init; } = "";
    public bool OmitCondition { get; init; } = false;
}