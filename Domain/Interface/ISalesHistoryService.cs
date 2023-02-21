namespace fbtracker
{
    public interface ISalesHistoryService
    {
       public Task<IEnumerable<SalesHistory>?> GetSalesHistoryAsync (int fbDataId);
    }
}