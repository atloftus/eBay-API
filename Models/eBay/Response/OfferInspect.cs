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
    /// Represents details for inspecting an eBay offer.
    /// </summary>
    public class OfferInspect(
        string Sku,
        string Status,
        string CategoryId,
        string Marketplace,
        string Price);
    #endregion
}