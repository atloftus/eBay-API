namespace eBay_API.Models.Controller.Buy
{
    public class RunResult
    { 
        public RunResult(string name, int itemCount) { Name = name; ItemCount = itemCount; } 
        public string Name { get; } public int ItemCount { get; } 
    }
}
