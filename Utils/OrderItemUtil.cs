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
        if (row == null || row.Count < 8) return null;

        return new OrderItem
        {
            Title = row[0]?.ToString() ?? "",
            Price = row[1]?.ToString() ?? "",
            TaxAmount = row[2]?.ToString() ?? "",
            ShippingAmount = row[3]?.ToString() ?? "",
            // TotalAmount is a calculated property, so it's not set here
            Created = row[5]?.ToString() ?? "",
            ItemId = row[7]?.ToString() ?? ""
        };
    }
}