using fbtracker.Models;
using Telegram.Bot.Requests;

namespace fbtracker.Services.Interfaces;

public interface INotificationService
{
    Task SendMessage(Profit profitPlayer, int avgPrice, IEnumerable<SalesHistory> lastTenSales, Card card);
}