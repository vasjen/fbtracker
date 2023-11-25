using System.ComponentModel.DataAnnotations;

namespace fbtracker.Models
{
    public class Card
    {   
        [Key]
        public int CardId {get;set;}
        public int FbId { get; set; }
        public int FbDataId {get;set;}
        public string ShortName { get; set; } = string.Empty;
        public int Rating {get;set;}
        public string Version {get;set;} = string.Empty;
        public string Position {get;set;} = string.Empty;
        public string Price {get;set;} = string.Empty;
        public bool Tradable {get;set;} = false;
        public List<Profit> Profits {get;set;}
    }
}