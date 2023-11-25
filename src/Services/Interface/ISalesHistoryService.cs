using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface ISalesHistoryService
    {
       public Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId, HttpClient client);
    }
}