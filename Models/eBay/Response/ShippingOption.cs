using System;
using System.Text.Json.Serialization;

namespace eBay_API.Models.eBay.Response;
public class ShippingOption
{
    [JsonPropertyName("shippingCostType")]
    public string ShippingCostType { get; set; }

    [JsonPropertyName("shippingCost")]
    public Price ShippingCost { get; set; }

    [JsonPropertyName("minEstimatedDeliveryDate")]
    public DateTime MinEstimatedDeliveryDate { get; set; }

    [JsonPropertyName("maxEstimatedDeliveryDate")]
    public DateTime MaxEstimatedDeliveryDate { get; set; }
}