using eBay_API.Models.GoogleDrive;

public static class OrderItemUtil
{
    /// <summary>
    /// Converts a Google Sheet row to an OrderItem object.
    /// </summary>
    /// <param name="row">Row from Google Sheet.</param>
    /// <returns>OrderItem object or null if row is invalid.</returns>
    public static OrderItem? ToOrderItem(IList<object> row)
    {
        if (row == null || row.Count < 11) return null;

        return new OrderItem
        {
            Created = row[0]?.ToString() ?? "",
            Title = row[1]?.ToString() ?? "",
            Price = row[6]?.ToString() ?? "",
            TaxAmount = row[7]?.ToString() ?? "",
            ShippingAmount = row[8]?.ToString() ?? "",
            ItemId = row[10]?.ToString() ?? ""
        };
    }
}