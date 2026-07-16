using Google.Apis.Sheets.v4.Data;

namespace eBay_API.Models.GoogleDrive
{
    public class PokemonSet
    {
        public string Name { get; set; }
        public string SheetID { get; set; }
        public DateTime ReleaseDate { get; set; }
        List<PokemonCard> Cards = new List<PokemonCard>();
        public int TotalCards { get { return Cards.Count; } }
        public int HaveCards { get { return Cards.Where(c => c.Have).Count(); } }
        public int MissingCards { get { return TotalCards - HaveCards; } }


        public PokemonSet(Sheet sheet) {
            Name = sheet.Properties.Title;
            SheetID = sheet.Properties.SheetId.ToString();

            //TODO: Need to finish implementation here
        }
    }
}