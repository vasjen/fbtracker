using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface IPriceService
    {
         Task GetPriceAsync (Card card, HttpClient client);
    }
}