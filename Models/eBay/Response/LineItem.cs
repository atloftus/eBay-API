namespace eBay_API.Models.eBay.Response
{
    /// <summary>
    /// Represents a line item from an eBay order.
    /// </summary>
    public record LineItem(
        string ItemId,
        string Title,
        decimal? Price,
        DateTime? Created,
        decimal? TaxAmount,
        decimal? ShippingAmount);
}