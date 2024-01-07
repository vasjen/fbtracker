using fbtracker.Models;
using fbtracker.Services.Interfaces;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;

namespace fbtracker.Services {
public class SalesHistoryService : ISalesHistoryService{
    private readonly ILogger<SalesHistoryService> _logger;

    public SalesHistoryService(ILogger<SalesHistoryService> logger)
    {
        _logger = logger;
    }
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
           _logger.LogInformation(ex.Message);
           _logger.LogError("Can't deserialize sales history for player with id {0}",fbDataId);
         }
         return this.Histories;
    }
      
        

}
 
 
       
       

}