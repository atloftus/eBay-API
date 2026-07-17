using Google.Apis.Sheets.v4.Data;

namespace eBay_API.Models.GoogleDrive
{
    public class PokemonSet
    {
        public string Name { get; set; }
        public string SheetID { get; set; }
        public List<PokemonCard> Cards { get; set; } = new List<PokemonCard>();
        public int TotalCards { get { return Cards.Count; } }
        public int HaveCards { get { return Cards.Where(c => c.Have).Count(); } }
        public int MissingCards { get { return TotalCards - HaveCards; } }


        public PokemonSet(Sheet sheet) {
            Name = sheet.Properties.Title.ToLower();
            SheetID = sheet.Properties.SheetId.ToString();
        }
    }
}