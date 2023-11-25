using fbtracker.Models;

namespace fbtracker.Services.Interfaces
{
    internal interface IProfitService
    {
        public  Task FindingProfitAsync (IAsyncEnumerable<Card> Cards);
    }
}