using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;

namespace fbtracker.Domain {
public class SalesHistoryService : ISalesHistoryService{
      
     
   private IEnumerable<SalesHistory>? Histories { get; set; }
   private const string URL = "https://www.futbin.com/24/getPlayerSales?platform=ps&resourceId=";
    public async Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId, HttpClient client) 
    {
         string sales = await client.GetStringAsync(URL+fbDataId);
         try 
         {
             this.Histories = JsonConvert.DeserializeObject<IEnumerable<SalesHistory>>(sales);
         }
         catch (JsonException ex)
         {
             System.Console.WriteLine(ex.Message);
             Console.WriteLine("Catch for {0}",fbDataId);
         }
         return this.Histories;
    }
      
        

}
 
 
       
       

}