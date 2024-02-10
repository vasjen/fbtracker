using System.ComponentModel.DataAnnotations;

namespace fbtracker.Models
{
    public class Card
    {   
       
        public int FbId { get; set; }
        public int FbDataId {get;set;}
        public string ShortName { get; set; } = string.Empty;
        public int Rating {get;set;}
        public string Version {get;set;} = string.Empty;
        public string Position {get;set;} = string.Empty;
        
        public Prices Prices {get;set;}
        public string PromoUrlFile {get;set;} = string.Empty;
        
        public string PromoUrl {get;set;} = string.Empty;
        public string ImageUrl {get;set;} = string.Empty;
        public override string? ToString()
        {
            return $"{this.ShortName}: {this.Version} {this.Rating} {this.Position} with FbId: {this.FbId} and FbDataId: {this.FbDataId}";
        }
        
    }
    
}