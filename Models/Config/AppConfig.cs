namespace eBay_API.Models.Config;

public class AppConfig
{
    public string[] sportsfilterwords { get; set; }
    public string[] pokemonfilterwords { get; set; }
    public RunConfig[] runs { get; set; }
    public List<string> sellers { get; set; }
    public string[] pokemon { get; set; }
    public string[] pokemonsets { get; set; }
}