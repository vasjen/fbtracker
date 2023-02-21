namespace fbtracker
{
    public interface IUpdateService
    {
        public Task<Card> GetNewCardAsync(int FbId);
        public Task<IEnumerable<Card>> GetNewCardsRange(IEnumerable<int> FbIds);

        public  Task ExistNewCardsAsync();
    }
}