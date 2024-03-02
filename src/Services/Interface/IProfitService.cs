using fbtracker.Models;

namespace fbtracker.Services.Interfaces;

public interface IProfitService
{
    public Task FindProfitCards (IAsyncEnumerable<Card> cards);
}