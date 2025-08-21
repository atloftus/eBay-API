namespace eBay_API.Models.Controller.Order
{
    public class SyncOrdersResponse
    {
        public int LookbackDays { get; init; }
        public int Purchases { get; init; }
        public int InventoryUpserted { get; init; }
        public int DraftOffers { get; init; }
        public int Published { get; init; }
        public List<string> CreatedSkus { get; init; } = new();
        public List<OfferPair> Offers { get; init; } = new();
        public string? CsvBase64 { get; init; } // when IncludeCsv=true
    }
}
