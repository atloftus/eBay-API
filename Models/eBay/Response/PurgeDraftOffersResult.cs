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
    /// Represents the result of purging draft offers.
    /// </summary>
    public sealed record PurgeDraftOffersResult(
        int totalOffers,
        int candidates,
        int deletedCount,
        List<OfferInfo> candidatesList,
        List<OfferInfo> deleted,
        int failed = 0);
    #endregion
}