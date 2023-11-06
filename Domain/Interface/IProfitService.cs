namespace fbtracker
{
    internal interface IProfitService
    {
        public  Task FindingProfitAsync (IAsyncEnumerable<Card> Cards);
    }
}