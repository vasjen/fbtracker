using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface ITelegramService
    {
         Task SendInfo(Profit ProfitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card);
    }
}