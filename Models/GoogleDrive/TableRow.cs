using System.Reflection;
using System.Collections.Generic;
using System.Linq;



namespace eBay_API.Models.GoogleDrive
{
    public class TableRow
    {
        public IList<object> ToRow()
        {
            // Exclude properties marked with [System.Text.Json.Serialization.JsonIgnore]
            var props = this.GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null)
                .Select(p => new
                {
                    Property = p,
                    Order = p.GetCustomAttribute<ColumnOrderAttribute>()?.Order ?? int.MaxValue
                })
                .OrderBy(x => x.Order)
                .Select(x => x.Property)
                .ToList();

            return props.Select(p => p.GetValue(this)).ToList();
        }

        public IList<string> GetHeaderRow()
        {
            // Keep headers consistent with ToRow by also excluding JsonIgnore properties
            return this.GetType()
                .GetProperties()
                .Where(p => p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null)
                .Select(p => new
                {
                    Property = p,
                    Order = p.GetCustomAttribute<ColumnOrderAttribute>()?.Order ?? int.MaxValue
                })
                .OrderBy(x => x.Order)
                .Select(x => x.Property.Name)
                .ToList();
        }
    }
}
