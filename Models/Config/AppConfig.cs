namespace eBay_API.Models.Config;

public class AppConfig
{
    public string[] filterwords { get; set; }
    public RunConfig[] runs { get; set; }
    public List<string> sellers { get; set; }
}