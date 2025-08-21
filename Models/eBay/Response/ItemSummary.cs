using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace eBay_API.Models.eBay.Response;
public class ItemSummary
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("leafCategoryIds")]
    public List<string> LeafCategoryIds { get; set; }

    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; }

    [JsonPropertyName("image")]
    public Image Image { get; set; }

    [JsonPropertyName("itemHref")]
    public string ItemHref { get; set; }

    [JsonPropertyName("seller")]
    public Seller Seller { get; set; }

    [JsonPropertyName("condition")]
    public string Condition { get; set; }

    [JsonPropertyName("conditionId")]
    public string ConditionId { get; set; }

    [JsonPropertyName("thumbnailImages")]
    public List<Image> ThumbnailImages { get; set; }

    [JsonPropertyName("shippingOptions")]
    public List<ShippingOption> ShippingOptions { get; set; }

    [JsonPropertyName("buyingOptions")]
    public List<string> BuyingOptions { get; set; }

    [JsonPropertyName("bidCount")]
    public int? BidCount { get; set; }

    [JsonPropertyName("currentBidPrice")]
    public Price CurrentBidPrice { get; set; }

    [JsonPropertyName("epid")]
    public string Epid { get; set; }

    [JsonPropertyName("itemAffiliateWebUrl")]
    public string ItemAffiliateWebUrl { get; set; }

    [JsonPropertyName("itemWebUrl")]
    public string ItemWebUrl { get; set; }

    [JsonPropertyName("itemLocation")]
    public ItemLocation ItemLocation { get; set; }

    [JsonPropertyName("additionalImages")]
    public List<Image> AdditionalImages { get; set; }

    [JsonPropertyName("adultOnly")]
    public bool AdultOnly { get; set; }

    [JsonPropertyName("legacyItemId")]
    public string LegacyItemId { get; set; }

    [JsonPropertyName("availableCoupons")]
    public bool AvailableCoupons { get; set; }

    [JsonPropertyName("itemOriginDate")]
    public DateTime ItemOriginDate { get; set; }

    [JsonPropertyName("itemCreationDate")]
    public DateTime ItemCreationDate { get; set; }

    [JsonPropertyName("itemEndDate")]
    public DateTime ItemEndDate { get; set; }

    [JsonPropertyName("topRatedBuyingExperience")]
    public bool TopRatedBuyingExperience { get; set; }

    [JsonPropertyName("priorityListing")]
    public bool PriorityListing { get; set; }

    [JsonPropertyName("listingMarketplaceId")]
    public string ListingMarketplaceId { get; set; }
    [JsonPropertyName("price")]
    public Price? Price { get; set; }
}