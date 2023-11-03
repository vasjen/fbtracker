namespace fbtracker
{
    public interface IInitialService
    {   
            Task<IEnumerable<Card>> GetCardsRangeAsync();
            Task<Card> GetNewCardAsync(int FbId);
            IAsyncEnumerable<Card> GetCards(string url);
            Task<int> GetMaxNumberPage(string Url);



    }
}