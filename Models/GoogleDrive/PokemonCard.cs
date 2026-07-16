using System.Diagnostics.Eventing.Reader;

namespace eBay_API.Models.GoogleDrive
{
    public class PokemonCard
    {
        public PokemonSet Set { get; set; }
        public string SetNumber { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int QualityNormal { get; set; }
        public int QualityReverse { get; set; }
        public int QualityHolo { get; set; }
        public bool Have { get { return (QualityNormal > 0) || (QualityReverse > 0) || (QualityHolo > 0); } }
    }
}