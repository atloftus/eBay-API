namespace eBay_API.Models.eBay.Response
{


    #region PROPERTIES
    // No properties, this is a record type.
    #endregion



    #region CONSTRUCTORS
    // No explicit constructors, record type uses primary constructor.
    #endregion



    #region METHODS
    /// <summary>
    /// Represents summary information for an eBay offer.
    /// </summary>
    public sealed record OfferInfo(
        string? OfferId,
        string? Sku,
        string? Status,
        string? MarketplaceId);
    #endregion
}