using fbtracker.Models;
using Telegram.Bot.Requests;

namespace fbtracker.Services.Interfaces;

public interface INotificationService
{
    Task SendMessageAsync(ProfitCard profitCardPlayer);
}