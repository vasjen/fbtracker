namespace fbtracker
{
    public interface IInitialService
    {
        Task<IEnumerable<Card>> GetCardsRangeAsync(HttpClient client);
            Task<Card> GetNewCardAsync(int FbId);
            public IAsyncEnumerable<Card> GetCards(string url, HttpClient client);
            Task<int> GetMaxNumberPage(string Url);
            int GetDataId(Card Card, HttpClient client);



    }
}