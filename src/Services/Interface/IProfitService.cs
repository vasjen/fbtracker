using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    public interface IProfitService
    {
        public  Task FindingProfitAsync (IAsyncEnumerable<Card> Cards);
    }
}