namespace fbtracker
{
    public interface IInitialService
    {   
            Task<IEnumerable<Card>> GetCardsRangeAsync();
            Task<Card> GetNewCardAsync(int FbId);
            

         
    }
}