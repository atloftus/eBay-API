namespace eBay_API.Models.Controller.Order
{
    public class SyncOrdersRequest
    {
        public int? LookbackDays { get; init; }
        public string? ConditionInput { get; init; } // e.g., "USED_EXCELLENT"
        public bool? CreateOffers { get; init; } = true;
        public bool? PublishFirst { get; init; } = true;
        public decimal? FallbackPrice { get; init; } // overrides config fallback
        public string? CategoryFallback { get; init; }
        public int? LimitForTesting { get; init; } // e.g., 1
        public bool? IncludeCsv { get; init; } = false;
    }
}
