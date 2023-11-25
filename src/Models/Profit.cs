using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fbtracker.Models 
{
    public class Profit 
    {
        [Key]
        public int Id {get;set;}
        public DateTime Date {get;set;} = DateTime.Now;
        [ForeignKey("Card")]
        public int CardId {get;set;}
        public int Price {get;set;}
        public int SellPrice {get;set;}
        public int ProfitValue {get;set;}
        public decimal Percentage {get;set;} 
        public Card Card {get;set;}

    }
}