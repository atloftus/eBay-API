using System.Text.Json.Serialization;



namespace eBay_API.Models.eBay.Response
{
    public class InventoryItemPayload
    {
        #region PROPERTIES
        public Availability availability { get; init; } = default!;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? condition { get; init; }
        public Product product { get; init; } = default!;
        #endregion



        #region CONSTRUCTORS
        // No explicit constructors.
        #endregion



        #region METHODS
        // No additional methods.    
        #endregion
    }
}