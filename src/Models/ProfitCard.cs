using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fbtracker.Models 
{
    public class ProfitCard 
    {
        [Key]
        public int Price {get;}
        public int SellPrice {get; }
        public int ProfitValue {get; }
        public decimal Percentage {get;} 
        public Card Card {get;set;}
        public IEnumerable<SalesHistory> LastSales {get;}

        public ProfitCard(Card card, int profitValue, int avgPrice, IEnumerable<SalesHistory> salesHistories)
        {
            Card = card;
            Price = card.Prices.Ps.LCPrice;
            SellPrice = avgPrice;
            ProfitValue = profitValue;
            Percentage = (decimal)profitValue / Price;
            LastSales = salesHistories;
        }
            
        

    }
}