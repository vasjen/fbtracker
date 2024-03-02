using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface ITelegramService
    {
         Task SendInfo(ProfitCard profitCardPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card);
    }
}