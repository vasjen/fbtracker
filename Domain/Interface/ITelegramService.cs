namespace fbtracker
{
    public interface ITelegramService
    {
         Task SendInfo (Profit ProfitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales);
 
    }
}