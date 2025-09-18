using System.Reflection;



namespace eBay_API.Models.GoogleDrive
{
    public class TableRow
    {
        public IList<object> ToRow()
        {
            var props = this.GetType()
                .GetProperties()
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
            return this.GetType()
                .GetProperties()
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
